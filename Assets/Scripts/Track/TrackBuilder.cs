using UnityEngine;

/// <summary>
/// Editor helper for building a track over a snapping grid.
/// - Draws a blue grid (for snapping).
/// - Generates a line of red track nodes as children on demand.
/// - Nodes can be snapped to the grid via a context menu.
/// - At runtime, draws a visible track using LineRenderers between nodes.
/// - Automatically adds TrackBranch components to each node (branches default to off).
/// </summary>
public class TrackBuilder : MonoBehaviour
{
    [Header("Grid")]
    public int gridWidth = 20;        // cells in X
    public int gridHeight = 20;       // cells in Z
    public float cellSize = 1f;       // grid spacing
    public Color gridColor = Color.blue;
    public float gridY = 0f;          // height of the grid

    [Header("Track Nodes")]
    [Min(2)] public int nodeCount = 10;

    [Min(1)]
    public int cellsBetweenNodes = 1; // spacing in grid cells between nodes

    public Color trackColor = Color.red;
    public float nodeSphereRadius = 0.1f;

    [Tooltip("Child transforms used as track nodes (auto managed).")]
    public Transform[] nodes;

    [Header("Branches")]
    [Tooltip("Color used to draw branch tracks in the scene view.")]
    public Color branchColor = Color.green;

    [Tooltip("Automatically add a TrackBranch component to every node when regenerating.")]
    public bool autoAddBranchComponents = true;

    [Header("Track Visual (runtime)")]
    public float trackLineWidth = 0.05f;
    public float trackYOffset = 0.02f;      // lift track slightly above grid
    public Material trackLineMaterial;      // assign simple unlit material

    Transform trackLinesRoot; // parent object for runtime line renderers

    // ----------------- VALIDATION -----------------

    void OnValidate()
    {
        // Clamp values, but DO NOT regenerate nodes automatically.
        gridWidth = Mathf.Max(1, gridWidth);
        gridHeight = Mathf.Max(1, gridHeight);
        cellSize = Mathf.Max(0.01f, cellSize);
        nodeCount = Mathf.Max(2, nodeCount);
        cellsBetweenNodes = Mathf.Max(1, cellsBetweenNodes);
    }

    // ----------------- NODE GENERATION -----------------
    // Call this ONLY when you actually want to reset the track layout.

    [ContextMenu("Regenerate Nodes (Reset Track)")]
    public void RegenerateNodes()
    {
        // 1) Delete all children that are *nodes*, but keep the runtime track lines root if it exists
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "TrackLines") continue; // keep visual container for runtime
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
                Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }

        // 2) Compute world spacing = N cells * cellSize
        float spacing = cellsBetweenNodes * cellSize;

        // 3) Allocate node array
        nodes = new Transform[nodeCount];

        // 4) Lay them out along +Z, centered around this object
        float totalLength = (nodeCount - 1) * spacing;
        float startOffset = -totalLength * 0.5f;

        for (int i = 0; i < nodeCount; i++)
        {
            GameObject go = new GameObject($"Node {i}");
            go.transform.SetParent(transform, worldPositionStays: false);

            Vector3 localPos = new Vector3(0f, gridY, startOffset + i * spacing);
            go.transform.localPosition = localPos;

            // Auto-attach TrackBranch so every node has the option to branch
            if (autoAddBranchComponents)
            {
                var branch = go.GetComponent<TrackBranch>();
                if (branch == null)
                    branch = go.AddComponent<TrackBranch>();

                // default branch visuals match TrackBuilder's branchColor
                branch.branchColor = branchColor;
                // branchCount starts at 0, so no branches until you toggle it
            }

            nodes[i] = go.transform;
        }

        SnapAllNodesToGrid();

        if (Application.isPlaying)
            BuildRuntimeTrackLines();
    }

    [ContextMenu("Snap Nodes To Grid")]
    public void SnapNodesToGridMenu()
    {
        if (nodes != null)
            SnapAllNodesToGrid();
    }

    void SnapAllNodesToGrid()
    {
        if (nodes == null) return;

        Vector3 origin = transform.position + Vector3.up * gridY;

        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] == null) continue;

            Vector3 worldPos = nodes[i].position;

            // Work in grid space relative to origin
            Vector3 rel = worldPos - origin;
            rel.x = Mathf.Round(rel.x / cellSize) * cellSize;
            rel.z = Mathf.Round(rel.z / cellSize) * cellSize;
            rel.y = 0f;

            nodes[i].position = origin + rel;
        }
    }

    // ----------------- RUNTIME TRACK VISUALS -----------------

    void Start()
    {
        if (Application.isPlaying)
        {
            // In case the nodes array lost references, rebuild it from children
            if (nodes == null || nodes.Length == 0)
                RebuildNodesArrayFromChildren();

            BuildRuntimeTrackLines();
        }
    }

    void RebuildNodesArrayFromChildren()
    {
        // Grab all children except "TrackLines" and use them as nodes (sorted by name)
        var nodeList = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform c = transform.GetChild(i);
            if (c.name == "TrackLines") continue;
            nodeList.Add(c);
        }

        nodeList.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        nodes = nodeList.ToArray();
        nodeCount = nodes.Length;
    }

    void BuildRuntimeTrackLines()
    {
        if (trackLineMaterial == null)
        {
            Debug.LogWarning("TrackBuilder: Please assign a trackLineMaterial to see the track in Game view.");
            return;
        }

        if (nodes == null || nodes.Length < 2)
            return;

        // create or find the track line root
        if (trackLinesRoot == null)
        {
            Transform existing = transform.Find("TrackLines");
            if (existing != null)
            {
                trackLinesRoot = existing;
            }
            else
            {
                GameObject rootObj = new GameObject("TrackLines");
                rootObj.transform.SetParent(transform, false);
                trackLinesRoot = rootObj.transform;
            }
        }

        // Clear existing line children
        for (int i = trackLinesRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(trackLinesRoot.GetChild(i).gameObject);
        }

        // ---- MAIN TRACK (red) ----
        for (int i = 0; i < nodes.Length - 1; i++)
        {
            Vector3 from = nodes[i].position + Vector3.up * trackYOffset;
            Vector3 to = nodes[i + 1].position + Vector3.up * trackYOffset;
            CreateTrackLine(from, to); // uses trackColor
        }

        // ---- BRANCHES (green) ----
        var branches = GetComponentsInChildren<TrackBranch>();
        foreach (var br in branches)
        {
            if (br == null) continue;

            // ensure branch uses our configured color
            Color color = br.branchColor;

            AddBranchLines(br.transform.position, br.branch1Nodes, color);
            AddBranchLines(br.transform.position, br.branch2Nodes, color);
        }
    }

    void AddBranchLines(Vector3 parentPos, Transform[] arr, Color color)
    {
        if (arr == null || arr.Length == 0) return;

        Vector3 parent = parentPos + Vector3.up * trackYOffset;

        // parent -> first node
        if (arr[0] != null)
        {
            Vector3 p0 = arr[0].position + Vector3.up * trackYOffset;
            CreateTrackLine(parent, p0, color);
        }

        // between branch nodes
        for (int i = 1; i < arr.Length; i++)
        {
            if (arr[i] == null || arr[i - 1] == null) continue;

            Vector3 pA = arr[i - 1].position + Vector3.up * trackYOffset;
            Vector3 pB = arr[i].position + Vector3.up * trackYOffset;
            CreateTrackLine(pA, pB, color);
        }
    }

    void CreateTrackLine(Vector3 from, Vector3 to)
    {
        CreateTrackLine(from, to, trackColor);
    }

    // new overload with explicit color
    void CreateTrackLine(Vector3 from, Vector3 to, Color color)
    {
        GameObject lineObj = new GameObject("TrackSegment");
        lineObj.transform.SetParent(trackLinesRoot, worldPositionStays: true);

        var lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);

        lr.startWidth = trackLineWidth;
        lr.endWidth = trackLineWidth;
        lr.material = trackLineMaterial;
        lr.startColor = color;
        lr.endColor = color;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
    }

    // ----------------- GIZMOS (SCENE VIEW) -----------------

    void OnDrawGizmos()
    {
        DrawGridGizmos();
        DrawTrackGizmos();
    }

    void DrawGridGizmos()
    {
        Gizmos.color = gridColor;

        float halfWidth = gridWidth * cellSize * 0.5f;
        float halfHeight = gridHeight * cellSize * 0.5f;
        Vector3 origin = transform.position + new Vector3(0f, gridY, 0f);

        // Vertical lines (along Z)
        for (int x = 0; x <= gridWidth; x++)
        {
            float worldX = -halfWidth + x * cellSize;
            Vector3 from = origin + new Vector3(worldX, 0f, -halfHeight);
            Vector3 to = origin + new Vector3(worldX, 0f, halfHeight);
            Gizmos.DrawLine(from, to);
        }

        // Horizontal lines (along X)
        for (int z = 0; z <= gridHeight; z++)
        {
            float worldZ = -halfHeight + z * cellSize;
            Vector3 from = origin + new Vector3(-halfWidth, 0f, worldZ);
            Vector3 to = origin + new Vector3(halfWidth, 0f, worldZ);
            Gizmos.DrawLine(from, to);
        }
    }

    void DrawTrackGizmos()
    {
        if (nodes == null || nodes.Length < 2) return;

        // ----- Main spine (red) -----
        Gizmos.color = trackColor;

        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] == null) continue;

            Vector3 pos = nodes[i].position;
            Gizmos.DrawSphere(pos, nodeSphereRadius);

            if (i > 0 && nodes[i - 1] != null)
            {
                Gizmos.DrawLine(nodes[i - 1].position, pos);
            }
        }

        // ----- Branches (green) -----
        var branches = GetComponentsInChildren<TrackBranch>();
        foreach (var br in branches)
        {
            if (br == null) continue;
            br.branchColor = branchColor;  // keep color consistent
            br.DrawGizmos();
        }
    }
}
