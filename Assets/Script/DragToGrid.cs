using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DragToGrid : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GridMap grid;
    public RopeHitTester ropeHitTester;      // gắn trên GO có ObiSolver
    public Transform ownerRoot;              // dùng để ignore self trong occupancy

    [Header("Options")]
    public Vector3 extraOffset = Vector3.zero;
    public bool forbidCornerCut = true;
    public float bottomExtraOffset = 0f;
    public bool projectToGridPlane = true;
    public bool clampBeforeCut = true;
    [Range(6, 16)] public int clampBinarySteps = 12;

    [Header("Obstacle (Unity colliders)")]
    public string obstacleTag = "Obstacle";
    public float obstacleProbeHeight = 5f;

    private bool _dragging;
    private Vector2Int _lastCell;

    void Awake()
    {
        if (!grid) grid = GridMap.Instance ? GridMap.Instance : FindObjectOfType<GridMap>(true);
        if (!ropeHitTester) ropeHitTester = FindObjectOfType<RopeHitTester>(true);
        if (!ownerRoot) ownerRoot = transform.root;

        if (grid == null) { enabled = false; Debug.LogError("[DragToGrid] Missing GridMap"); return; }

        _lastCell = grid.WorldToCell(transform.position);
        grid.Occupy(_lastCell, ownerRoot);
    }

    void OnDisable()
    {
        if (grid != null) grid.Vacate(_lastCell, ownerRoot);
    }

    public void OnMouseDown()
    {
        if (grid == null) return;
        _dragging = true;
        grid.Vacate(_lastCell, ownerRoot); // bỏ chiếm để kéo qua ô cũ
    }

    public void OnMouseDrag()
    {
        if (!_dragging || grid == null) return;
        if (!TryGetMouseWorld(out var mouseWorld)) return;
        if (IsBlockedAt(mouseWorld)) return; // chặn theo tag obstacle (Unity)

        var target = mouseWorld + extraOffset;
        Vector3 from = transform.position;

        if (projectToGridPlane)
        {
            from.y = grid.yLevel;
            target.y = grid.yLevel;
        }

        // 1) CHẶN Ô GRID BỊ CHIẾM (mọi vật), bỏ qua chính mình
        var targetCell = grid.WorldToCell(target);
        if (grid.IsBlocked(targetCell, ownerRoot))
            return;

        // 2) CHẶN DÂY BẰNG OBI RAYCAST
        if (ropeHitTester != null && ropeHitTester.DragSegmentHitsAnyRope(from, target))
        {
            if (!clampBeforeCut) return;

            Vector3 lo = from, hi = target;
            for (int i = 0; i < clampBinarySteps; i++)
            {
                Vector3 mid = Vector3.Lerp(lo, hi, 0.5f);
                if (ropeHitTester.DragSegmentHitsAnyRope(from, mid)) hi = mid; else lo = mid;
            }
            target = lo;
        }

        // 3) Kéo theo quy tắc grid (snap/bottom)
        var nextPos = grid.ConstrainedDrag(
            transform.position, target, transform, forbidCornerCut, bottomExtraOffset);

        transform.position = nextPos;
        _lastCell = grid.WorldToCell(nextPos);
    }

    public void OnMouseUp()
    {
        if (!_dragging || grid == null) return;
        _dragging = false;

        var snap = grid.CellToWorldBottomAligned(_lastCell, transform, bottomExtraOffset);
        transform.position = snap;
        grid.Occupy(_lastCell, ownerRoot);
    }

    // ---------- helpers ----------
    private bool TryGetMouseWorld(out Vector3 world)
    {
        world = Vector3.zero;
        var cam = Camera.main; if (!cam) return false;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
        { world = hit.point; return true; }

        var plane = new Plane(Vector3.up, new Vector3(0f, grid.yLevel, 0f));
        if (plane.Raycast(ray, out float t)) { world = ray.origin + ray.direction * t; return true; }

        return false;
    }

    private bool IsBlockedAt(Vector3 probePoint)
    {
        Vector3 origin = probePoint + Vector3.up * obstacleProbeHeight;
        float dist = obstacleProbeHeight * 2f;

        var hits = Physics.RaycastAll(origin, Vector3.down, dist, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.collider && (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)))
                continue;

            if (h.collider != null && h.collider.CompareTag(obstacleTag))
                return true;
        }
        return false;
    }
}
