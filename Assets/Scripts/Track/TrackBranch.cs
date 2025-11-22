using UnityEngine;

/// <summary>
/// Attach this to an individual track node (a child of TrackBuilder).
/// It can spawn up to 2 branch tracks as child nodes,
/// using the parent TrackBuilder's grid + spacing.
/// Branches are visualized as green gizmo lines.
/// </summary>
[ExecuteAlways]
public class TrackBranch : MonoBehaviour
{
    [Header("Branch Setup")]
    [Tooltip("0 = no branch, 1 = one branch, 2 = two opposite branches.")]
    [Range(0, 2)]
    public int branchCount = 0;

    [Min(2)]
    public int branchNodeCount = 5;

    [Tooltip("Spacing in grid cells between branch nodes. If 0, uses parent TrackBuilder.cellsBetweenNodes.")]
    public int branchCellsBetweenNodes = 0;

    [Header("Visuals")]
    public Color branchColor = Color.green;
    public float nodeSphereRadius = 1f;

    [Tooltip("First branch nodes (e.g., +X direction in TrackBuilder local space).")]
    public Transform[] branch1Nodes;

    [Tooltip("Second branch nodes (e.g., -X direction in TrackBuilder local space).")]
    public Transform[] branch2Nodes;

    void OnValidate()
    {
        branchNodeCount = Mathf.Max(2, branchNodeCount);
        if (branchCellsBetweenNodes < 0) branchCellsBetweenNodes = 0;
    }

    // ---------- PUBLIC SNAP BUTTON ----------

    [ContextMenu("Snap Branch Nodes To Grid")]
    public void SnapBranchesToGridMenu()
    {
        var builder = GetComponentInParent<TrackBuilder>();
        if (builder == null)
        {
            Debug.LogWarning("TrackBranch: Snap failed, no TrackBuilder found in parents.");
            return;
        }

        SnapBranchesToGrid(builder);
    }

    [ContextMenu("Regenerate Branches")]
    public void RegenerateBranches()
    {
        // Destroy old branch children
        DestroyBranchChildren("Branch1_");
        DestroyBranchChildren("Branch2_");

        if (branchCount <= 0)
        {
            branch1Nodes = null;
            branch2Nodes = null;
            return;
        }

        // Find parent TrackBuilder for spacing info
        var builder = GetComponentInParent<TrackBuilder>();
        if (builder == null)
        {
            Debug.LogWarning("TrackBranch: No TrackBuilder found in parents.");
            return;
        }

        float cellSize = builder.cellSize;
        int cellsBetween = (branchCellsBetweenNodes > 0)
            ? branchCellsBetweenNodes
            : builder.cellsBetweenNodes;

        float spacing = cellsBetween * cellSize;
        float y = builder.gridY;

        // base position is THIS NODE
        Vector3 branchOrigin = transform.position;          // world position of the node
        Transform builderRoot = builder.transform;
        Vector3 dirRight = builderRoot.right;               // +X in TrackBuilder space
        Vector3 dirLeft = -builderRoot.right;              // -X in TrackBuilder space

        // ---------- BRANCH 1 (e.g. +X from this node) ----------
        branch1Nodes = null;
        if (branchCount >= 1)
        {
            branch1Nodes = new Transform[branchNodeCount];

            for (int i = 0; i < branchNodeCount; i++)
            {
                GameObject go = new GameObject($"Branch1_Node_{i}");
                go.transform.SetParent(transform, worldPositionStays: false);

                float stepIndex = i + 1; // first node is 1 * spacing away
                Vector3 worldPos = branchOrigin + dirRight * (spacing * stepIndex);
                worldPos.y = y;

                go.transform.position = worldPos;
                branch1Nodes[i] = go.transform;
            }
        }

        // ---------- BRANCH 2 (e.g. -X from this node) ----------
        branch2Nodes = null;
        if (branchCount >= 2)
        {
            branch2Nodes = new Transform[branchNodeCount];

            for (int i = 0; i < branchNodeCount; i++)
            {
                GameObject go = new GameObject($"Branch2_Node_{i}");
                go.transform.SetParent(transform, worldPositionStays: false);

                float stepIndex = i + 1;
                Vector3 worldPos = branchOrigin + dirLeft * (spacing * stepIndex);
                worldPos.y = y;

                go.transform.position = worldPos;
                branch2Nodes[i] = go.transform;
            }
        }

        // Snap branches to same grid as main track
        SnapBranchesToGrid(builder);
    }

    void DestroyBranchChildren(string prefix)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (!c.name.StartsWith(prefix)) continue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(c.gameObject);
            else
                Object.Destroy(c.gameObject);
#else
            Object.Destroy(c.gameObject);
#endif
        }
    }

    // now public so TrackBuilder and the context menu can call it
    public void SnapBranchesToGrid(TrackBuilder builder)
    {
        float cellSize = builder.cellSize;
        float gridY = builder.gridY;
        Vector3 origin = builder.transform.position + Vector3.up * gridY;

        void SnapArray(Transform[] arr)
        {
            if (arr == null) return;
            foreach (var t in arr)
            {
                if (t == null) continue;

                Vector3 worldPos = t.position;
                Vector3 rel = worldPos - origin;

                rel.x = Mathf.Round(rel.x / cellSize) * cellSize;
                rel.z = Mathf.Round(rel.z / cellSize) * cellSize;
                rel.y = 0f;

                t.position = origin + rel;
            }
        }

        SnapArray(branch1Nodes);
        SnapArray(branch2Nodes);
    }

    // ---------- GIZMOS ----------

    public void DrawGizmos()
    {
        Gizmos.color = branchColor;

        DrawArray(branch1Nodes);
        DrawArray(branch2Nodes);
    }

    void DrawArray(Transform[] arr)
    {
        if (arr == null || arr.Length == 0) return;

        Vector3 parentPos = transform.position;

        // parent -> first node
        if (arr[0] != null)
        {
            Vector3 pos0 = arr[0].position;
            Gizmos.DrawSphere(pos0, nodeSphereRadius);
            Gizmos.DrawLine(parentPos, pos0);
        }

        // rest of the branch
        for (int i = 1; i < arr.Length; i++)
        {
            if (arr[i] == null) continue;

            Vector3 pos = arr[i].position;
            Gizmos.DrawSphere(pos, nodeSphereRadius);

            if (arr[i - 1] != null)
            {
                Gizmos.DrawLine(arr[i - 1].position, pos);
            }
        }
    }
}
