using UnityEngine;

public class TrackFollower : MonoBehaviour
{
    [Header("Track")]
    public TrackBuilder track;      // drag your TrackBuilderRoot here

    [Header("Movement")]
    public float maxSpeed = 6f;
    public float acceleration = 10f;
    public float deceleration = 12f;
    public float startingSpeed = 2f;
    public float rotateSpeed = 360f;

    [Header("Input Friction")]
    [Tooltip("Extra deceleration when holding W plus A/D (trying to move diagonally into the wall).")]
    public float strafeFrictionDecel = 20f;   // tweak to taste (bigger = stronger friction)

    [Header("Corners")]
    [Tooltip("Angle (in degrees) above which we treat a change in direction as a corner.")]
    public float cornerAngleThreshold = 5f;

    float currentSpeed = 0f;

    // segment i is from nodes[i] -> nodes[i+1]
    int segmentIndex = 0;
    float segmentLength = 0f;
    float distanceOnSegment = 0f; // 0..segmentLength

    Transform[] nodes;

    // turning visuals (like GridMover)
    Quaternion targetRotation;
    bool isRotating = false;

    // corner state (only used when we stop at an unchosen corner)
    bool atCorner = false;
    int cornerIncomingIndex = -1;
    int cornerOutgoingIndex = -1;
    int cornerTurnSign = 0; // -1 = left, +1 = right

    // direction along the track: +1 forward, -1 backwards
    int travelSign = 1;

    void Start()
    {
        if (track == null || track.nodes == null || track.nodes.Length < 2)
        {
            Debug.LogError("TrackFollower: Track or nodes not set up correctly.");
            enabled = false;
            return;
        }

        // start on the main spine
        nodes = track.nodes;

        segmentIndex = 0;
        UpdateSegmentData();

        transform.position = nodes[0].position;
        targetRotation = Quaternion.LookRotation(GetCurrentDirection(), Vector3.up);
        transform.rotation = targetRotation;
    }

    void Update()
    {
        HandleFlipInput();          // S = flip 180 (still works, but only on current path)
        HandleMoveInput();          // W = move
        HandleCornerInputIfNeeded();
        ApplyMovement();
        RotateToTarget();
    }

    // ---------------- S flips you 180° on current path ----------------

    void HandleFlipInput()
    {
        if (!Input.GetKeyDown(KeyCode.S))
            return;

        if (atCorner)
        {
            // At a corner, flip back along the segment we came from.
            if (cornerIncomingIndex >= 0 && cornerIncomingIndex < nodes.Length - 1)
            {
                atCorner = false;
                segmentIndex = cornerIncomingIndex;

                UpdateSegmentData();
                distanceOnSegment = segmentLength;

                travelSign = -1;

                Vector3 dir = -GetCurrentDirection();
                targetRotation = Quaternion.LookRotation(dir, Vector3.up);
                isRotating = true;
            }
        }
        else
        {
            // Not at a corner: flip direction along the current segment.
            travelSign *= -1;

            Vector3 dir = (travelSign > 0)
                ? GetCurrentDirection()
                : -GetCurrentDirection();

            targetRotation = Quaternion.LookRotation(dir, Vector3.up);
            isRotating = true;
        }
    }

    // ---------------- W accelerates/decelerates ----------------

    void HandleMoveInput()
    {
        // If we're sitting at a corner and haven’t committed a turn yet,
        // ignore W for movement – you need to pick a direction first (A/D or S flip).
        if (atCorner)
        {
            currentSpeed = 0f;
            return;
        }

        bool forwardHeld = Input.GetKey(KeyCode.W);
        bool leftHeld = Input.GetKey(KeyCode.A);
        bool rightHeld = Input.GetKey(KeyCode.D);

        // ---- FRICTION: holding W + A/D slows you down toward a stop ----
        bool frictionActive = forwardHeld && (leftHeld || rightHeld);
        if (frictionActive)
        {
            // pull speed toward 0 faster than normal decel
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, strafeFrictionDecel * Time.deltaTime);
            return;    // skip normal accel/decel this frame
        }

        // ---- normal W-only movement (your previous logic) ----
        float desiredSpeed = forwardHeld ? maxSpeed : 0f;

        if (forwardHeld && Mathf.Abs(currentSpeed) < startingSpeed)
            currentSpeed = startingSpeed;

        float rate;
        if (Mathf.Approximately(desiredSpeed, 0f))
            rate = deceleration;
        else if (Mathf.Approximately(currentSpeed, 0f))
            rate = acceleration;
        else
            rate = (Mathf.Sign(desiredSpeed) == Mathf.Sign(currentSpeed)) ? acceleration : deceleration;

        currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, rate * Time.deltaTime);
    }

    // ----------- If we’re already stopped at a corner, wait for A/D -----------

    void HandleCornerInputIfNeeded()
    {
        if (!atCorner)
            return;

        bool leftHeld = Input.GetKey(KeyCode.A);
        bool rightHeld = Input.GetKey(KeyCode.D);

        bool correctLeft = leftHeld && cornerTurnSign == -1;
        bool correctRight = rightHeld && cornerTurnSign == +1;

        if (correctLeft || correctRight)
        {
            segmentIndex = cornerOutgoingIndex;
            distanceOnSegment = 0f;

            UpdateSegmentData();
            atCorner = false;
        }
    }

    // ---------------- core movement along segments ----------------

    void ApplyMovement()
    {
        if (Mathf.Approximately(currentSpeed, 0f))
        {
            if (atCorner && cornerIncomingIndex >= 0 && cornerIncomingIndex < nodes.Length)
                transform.position = nodes[cornerIncomingIndex + 1].position;
            return;
        }

        float moveDist = currentSpeed * Time.deltaTime * travelSign;

        int safety = 0;
        while (!Mathf.Approximately(moveDist, 0f) && safety < 20)
        {
            safety++;

            if (moveDist > 0f)   // forward along nodes[segmentIndex] -> nodes[segmentIndex+1]
            {
                float remaining = segmentLength - distanceOnSegment;

                if (moveDist <= remaining)
                {
                    distanceOnSegment += moveDist;
                    moveDist = 0f;
                }
                else
                {
                    // we arrive at the next node
                    distanceOnSegment = segmentLength;
                    moveDist -= remaining;

                    // node we just reached
                    Transform cornerNode = nodes[segmentIndex + 1];
                    Vector3 curDir = GetDirection(segmentIndex, segmentIndex + 1);
                    bool leftHeld = Input.GetKey(KeyCode.A);
                    bool rightHeld = Input.GetKey(KeyCode.D);

                    // ---------- 1) Check for branch turn (green) ----------
                    TrackBranch br = cornerNode.GetComponent<TrackBranch>();
                    if (br != null && br.branchCount > 0)
                    {
                        Transform[] chosenBranch = null;
                        Vector3 facing = transform.forward;   // what the player considers "forward"

                        // branch 1
                        if (br.branch1Nodes != null && br.branch1Nodes.Length > 0)
                        {
                            Vector3 d1 = (br.branch1Nodes[0].position - cornerNode.position).normalized;
                            int s1 = GetCornerTurnSign(facing, d1); // -1 left, +1 right, relative to facing
                            if ((leftHeld && s1 == -1) || (rightHeld && s1 == +1))
                                chosenBranch = br.branch1Nodes;
                        }

                        // branch 2
                        if (chosenBranch == null && br.branch2Nodes != null && br.branch2Nodes.Length > 0)
                        {
                            Vector3 d2 = (br.branch2Nodes[0].position - cornerNode.position).normalized;
                            int s2 = GetCornerTurnSign(facing, d2);
                            if ((leftHeld && s2 == -1) || (rightHeld && s2 == +1))
                                chosenBranch = br.branch2Nodes;
                        }

                        if (chosenBranch != null)
                        {
                            // build a new nodes[] starting at this junction then along the branch
                            Transform[] newNodes = new Transform[chosenBranch.Length + 1];
                            newNodes[0] = cornerNode;
                            for (int n = 0; n < chosenBranch.Length; n++)
                                newNodes[n + 1] = chosenBranch[n];

                            nodes = newNodes;
                            segmentIndex = 0;
                            distanceOnSegment = 0f;
                            travelSign = 1;

                            UpdateSegmentData();
                            Vector3 newDir = GetCurrentDirection();
                            targetRotation = Quaternion.LookRotation(newDir, Vector3.up);
                            isRotating = true;

                            // continue with leftover moveDist on the new path
                            continue;
                        }
                    }

                    // ---------- 2) No branch chosen – continue on main spine ----------

                    if (segmentIndex < nodes.Length - 2)
                    {
                        // main next segment
                        Vector3 nextDir = GetDirection(segmentIndex + 1, segmentIndex + 2);
                        float angle = Vector3.Angle(curDir, nextDir);

                        if (angle > cornerAngleThreshold)
                        {
                            Vector3 facing = transform.forward;
                            int turnSign = GetCornerTurnSign(facing, nextDir);

                            bool correctLeft = leftHeld && turnSign == -1;
                            bool correctRight = rightHeld && turnSign == +1;

                            if (correctLeft || correctRight)
                            {
                                // commit turn on main spine
                                segmentIndex++;
                                UpdateSegmentData();
                                distanceOnSegment = 0f;

                                targetRotation = Quaternion.LookRotation(nextDir, Vector3.up);
                                isRotating = true;
                            }
                            else
                            {
                                // stop at the corner
                                atCorner = true;
                                cornerIncomingIndex = segmentIndex;
                                cornerOutgoingIndex = segmentIndex + 1;
                                cornerTurnSign = turnSign;

                                distanceOnSegment = segmentLength;
                                moveDist = 0f;
                                currentSpeed = 0f;

                                targetRotation = Quaternion.LookRotation(curDir, Vector3.up);
                                isRotating = true;
                            }
                        }
                        else
                        {
                            // almost straight → auto-continue
                            segmentIndex++;
                            UpdateSegmentData();
                            distanceOnSegment = 0f;
                        }
                    }
                    else
                    {
                        // end of current path
                        distanceOnSegment = segmentLength;
                        moveDist = 0f;
                        currentSpeed = 0f;

                        Vector3 lastDir = GetDirection(segmentIndex, segmentIndex + 1);
                        targetRotation = Quaternion.LookRotation(lastDir, Vector3.up);
                        isRotating = true;
                    }
                }
            }
            else // moveDist < 0f (backwards along current path)
            {
                float remaining = distanceOnSegment;
                float step = -moveDist; // positive

                if (step <= remaining)
                {
                    // we stay on this segment
                    distanceOnSegment -= step;
                    moveDist = 0f;
                }
                else
                {
                    // we want to go past the start of this segment
                    distanceOnSegment = 0f;
                    moveDist += remaining; // still negative

                    if (segmentIndex > 0)
                    {
                        // just move to previous segment in this path
                        segmentIndex--;
                        UpdateSegmentData();
                        distanceOnSegment = segmentLength;

                        Vector3 backDir = -GetCurrentDirection();
                        targetRotation = Quaternion.LookRotation(backDir, Vector3.up);
                        isRotating = true;
                    }
                    else
                    {
                        // we are at the *first* segment of this path and trying to go past its start.
                        // If this path is a branch, snap back to the main TrackBuilder nodes.
                        Transform rootNode = nodes[0];

                        bool reattachedToMain = false;

                        if (track != null && track.nodes != null && nodes != track.nodes)
                        {
                            int mainIndex = System.Array.IndexOf(track.nodes, rootNode);
                            if (mainIndex >= 0)
                            {
                                // Reattach to the main spine at this junction node.
                                nodes = track.nodes;
                                segmentIndex = mainIndex;
                                UpdateSegmentData();

                                // Put us exactly at the junction and stop.
                                distanceOnSegment = 0f;
                                transform.position = rootNode.position;

                                currentSpeed = 0f;
                                moveDist = 0f;
                                atCorner = false;          // let normal corner logic kick in next time we move

                                // Face "backwards" along the main segment we just attached to, if there is one.
                                if (segmentIndex > 0)
                                {
                                    Vector3 backDir = (nodes[segmentIndex - 1].position - rootNode.position).normalized;
                                    targetRotation = Quaternion.LookRotation(backDir, Vector3.up);
                                    isRotating = true;
                                }

                                reattachedToMain = true;
                            }
                        }

                        if (!reattachedToMain)
                        {
                            // Fallback: behave like before (dead-end, just stop).
                            distanceOnSegment = 0f;
                            moveDist = 0f;
                            currentSpeed = 0f;

                            Vector3 dir = -GetCurrentDirection();
                            targetRotation = Quaternion.LookRotation(dir, Vector3.up);
                            isRotating = true;
                        }
                    }
                }
            }
        }

        // place player on the segment or at corner node
        if (atCorner && cornerIncomingIndex >= 0 && cornerIncomingIndex < nodes.Length - 1)
        {
            transform.position = nodes[cornerIncomingIndex + 1].position;
        }
        else
        {
            Vector3 start = nodes[segmentIndex].position;
            Vector3 dirSeg = GetCurrentDirection();
            transform.position = start + dirSeg * distanceOnSegment;
        }
    }

    // ---------------- rotation helper ----------------

    void RotateToTarget()
    {
        if (!isRotating) return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );

        if (Quaternion.Angle(transform.rotation, targetRotation) < 0.1f)
        {
            transform.rotation = targetRotation;
            isRotating = false;
        }
    }

    // ---------------- track helpers ----------------

    void UpdateSegmentData()
    {
        Vector3 start = nodes[segmentIndex].position;
        Vector3 end = nodes[segmentIndex + 1].position;
        segmentLength = Vector3.Distance(start, end);
        if (segmentLength < 0.0001f)
            segmentLength = 0.0001f;

        Vector3 dir = (end - start).normalized * travelSign;
        targetRotation = Quaternion.LookRotation(dir, Vector3.up);
        isRotating = true;
    }

    Vector3 GetCurrentDirection()
    {
        return GetDirection(segmentIndex, segmentIndex + 1);
    }

    Vector3 GetDirection(int startIndex, int endIndex)
    {
        Vector3 start = nodes[startIndex].position;
        Vector3 end = nodes[endIndex].position;
        return (end - start).normalized;
    }

    /// <summary>
    /// Returns -1 for a left turn, +1 for a right turn,
    /// based on the player's facing (fromDir) toward nextDir. Y-up.
    /// </summary>
    int GetCornerTurnSign(Vector3 fromDir, Vector3 nextDir)
    {
        // Work only in XZ plane
        fromDir.y = 0f;
        nextDir.y = 0f;

        if (fromDir.sqrMagnitude < 0.0001f || nextDir.sqrMagnitude < 0.0001f)
            return 0;

        fromDir.Normalize();
        nextDir.Normalize();

        Vector3 cross = Vector3.Cross(fromDir, nextDir);

        if (cross.y > 0f) return +1;   // to the right of where I'm facing
        if (cross.y < 0f) return -1;   // to the left
        return 0;
    }

}
