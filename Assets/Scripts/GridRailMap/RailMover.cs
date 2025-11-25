using UnityEngine;

public class RailMover : MonoBehaviour
{
    [Header("Map")]
    public GridRailMap map;

    [Header("Start")]
    public Vector2Int startNode = new Vector2Int(0, 0);
    public Vector2Int startDirection = Vector2Int.up; // (0,1) = +Z

    [Header("Movement")]
    public float minSpeed = 2f;        // minimum speed while accelerating
    public float maxSpeed = 8f;        // top speed along the rail
    public float acceleration = 10f;   // units/sec^2 toward maxSpeed
    public float deceleration = 15f;   // units/sec^2 toward 0 when W released
    public float rotationSpeed = 720f; // deg/sec for visual rotation

    // Graph state
    Vector2Int currentNode;     // node we're logically "at"
    Vector2Int targetNode;      // node we're moving to (when stepping)
    Vector2Int facingDir;       // current facing direction (cardinal)

    bool isStepping = false;    // true = moving from currentNode -> targetNode
    float stepT = 0f;           // 0..1 progress along this step

    // queued turn: -1 = left, +1 = right, 2 = turn around, 0 = none
    int pendingTurn = 0;

    // movement speed along the current edge (world units / second)
    float currentSpeed = 0f;

    void Start()
    {
        if (map == null)
        {
            Debug.LogError("RailMover: Please assign a GridRailMap.");
            enabled = false;
            return;
        }

        currentNode = startNode;
        facingDir = startDirection == Vector2Int.zero ? Vector2Int.up : startDirection;

        // Snap exactly to start node
        transform.position = map.NodeToWorld(currentNode);
        FaceInstant(currentNode + facingDir);

        isStepping = false;
        stepT = 0f;
        pendingTurn = 0;
        currentSpeed = 0f;
    }

    void Update()
    {
        // If we're not moving along a segment, always snap to the current node
        if (!isStepping)
        {
            SnapToCurrentNode();
        }

        HandleTurnInput(); // A/D/S
        HandleMoveInput(); // W & stepping
        RotateVisual();    // smooth model rotation
    }

    // ---------------- INPUT ----------------

    void HandleTurnInput()
    {
        // If we're sliding along a segment, just queue the turn request.
        if (isStepping)
        {
            if (Input.GetKeyDown(KeyCode.A))
                pendingTurn = -1;   // left
            else if (Input.GetKeyDown(KeyCode.D))
                pendingTurn = +1;   // right
            else if (Input.GetKeyDown(KeyCode.S))
                pendingTurn = 2;    // turn around

            return;
        }

        // On a node: behave exactly like before (immediate turn).
        Vector2Int newDir = facingDir;
        bool wantTurn = false;

        if (Input.GetKeyDown(KeyCode.A))
        {
            newDir = RotateLeft(facingDir);
            wantTurn = true;
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            newDir = RotateRight(facingDir);
            wantTurn = true;
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            newDir = -facingDir;
            wantTurn = true;
        }

        // Only actually change facing if there's a rail that way
        if (wantTurn && map.HasEdge(currentNode, newDir))
        {
            facingDir = newDir;
        }
    }

    void HandleMoveInput()
    {
        bool wHeld = Input.GetKey(KeyCode.W);

        if (!isStepping)
        {
            // If we still have some speed and there's a rail, keep coasting from this node.
            if (currentSpeed > 0.01f && map.HasEdge(currentNode, facingDir))
            {
                TryStartStep();
            }
            else if (wHeld)
            {
                // Starting from rest or near-rest
                TryStartStep();
            }
            else
            {
                // fully stopped on node
                currentSpeed = 0f;
            }
        }
        else
        {
            // We're moving along an edge.
            StepAlongEdge(wHeld);
        }
    }

    // ---------------- STEPPING ----------------

    void TryStartStep()
    {
        // Only walk if there is a rail in facingDir
        if (!map.HasEdge(currentNode, facingDir))
            return;

        targetNode = currentNode + facingDir;
        isStepping = true;
        stepT = 0f;

        // Snap to exact start-of-edge position just in case
        transform.position = map.NodeToWorld(currentNode);

        // If starting from rest due to new input, kick up to at least minSpeed
        if (currentSpeed < 0.01f)
            currentSpeed = minSpeed;
    }

    void StepAlongEdge(bool wHeld)
    {
        Vector3 from = map.NodeToWorld(currentNode);
        Vector3 to = map.NodeToWorld(targetNode);

        float segmentLength = Vector3.Distance(from, to);
        if (segmentLength < 0.0001f)
        {
            // Degenerate, just snap to target node
            CompleteStep();
            return;
        }

        // --- speed update (accel vs decel) ---
        float targetSpeed = wHeld ? maxSpeed : 0f;
        float rate = wHeld ? acceleration : deceleration;

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.deltaTime);

        // While actively moving (W held), enforce a minSpeed floor
        if (wHeld && currentSpeed < minSpeed)
            currentSpeed = minSpeed;

        // If we've decelerated to (almost) zero, stop sliding further
        if (currentSpeed <= 0.01f && !wHeld)
        {
            currentSpeed = 0f;
            return;
        }

        // Convert world speed to 0..1 along the segment
        float deltaT = (currentSpeed * Time.deltaTime) / segmentLength;
        stepT += deltaT;
        if (stepT >= 1f)
        {
            stepT = 1f;
        }

        Vector3 pos = Vector3.Lerp(from, to, stepT);
        transform.position = pos;

        // When we reach the target node, decide whether to continue
        if (stepT >= 1f - Mathf.Epsilon)
        {
            CompleteStep();

            // Apply any queued turn now that we're exactly at the node
            ApplyPendingTurnAtNode();

            // If we still have speed (either from W or coasting) and there's a rail, keep going
            if (currentSpeed > 0.01f && map.HasEdge(currentNode, facingDir))
            {
                TryStartStep();
            }
            else
            {
                // No more forward motion / dead end
                currentSpeed = 0f;
            }
        }
    }

    void CompleteStep()
    {
        // We've finished moving from currentNode -> targetNode
        currentNode = targetNode;
        transform.position = map.NodeToWorld(currentNode);

        isStepping = false;
        stepT = 0f;
    }

    void ApplyPendingTurnAtNode()
    {
        if (pendingTurn == 0)
            return;

        Vector2Int newDir = facingDir;

        if (pendingTurn == -1)
            newDir = RotateLeft(facingDir);
        else if (pendingTurn == +1)
            newDir = RotateRight(facingDir);
        else if (pendingTurn == 2)
            newDir = -facingDir;

        // Only apply if there's actually a rail in that direction from this node
        if (map.HasEdge(currentNode, newDir))
        {
            facingDir = newDir;
        }

        pendingTurn = 0; // clear queued input
    }

    void SnapToCurrentNode()
    {
        transform.position = map.NodeToWorld(currentNode);
    }

    // ---------------- VISUAL ROTATION ----------------

    void RotateVisual()
    {
        // Face in the facingDir direction
        Vector3 facingWorld = new Vector3(facingDir.x, 0f, facingDir.y);
        if (facingWorld.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(facingWorld, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );
    }

    void FaceInstant(Vector2Int node)
    {
        Vector3 targetPos = map.NodeToWorld(node);
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    // ---------------- UTILS ----------------

    Vector2Int RotateLeft(Vector2Int dir)
    {
        if (dir == Vector2Int.up) return Vector2Int.left;
        if (dir == Vector2Int.left) return Vector2Int.down;
        if (dir == Vector2Int.down) return Vector2Int.right;
        if (dir == Vector2Int.right) return Vector2Int.up;
        return dir;
    }

    Vector2Int RotateRight(Vector2Int dir)
    {
        if (dir == Vector2Int.up) return Vector2Int.right;
        if (dir == Vector2Int.right) return Vector2Int.down;
        if (dir == Vector2Int.down) return Vector2Int.left;
        if (dir == Vector2Int.left) return Vector2Int.up;
        return dir;
    }
}
