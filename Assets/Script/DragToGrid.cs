using Obi;
using UnityEngine;

[RequireComponent(typeof(Collider))] // cần để nhận OnMouse... events
public class DragToGrid : MonoBehaviour
{
    [Header("Grid ref (để trống sẽ tự tìm GridMap trong scene)")]
    [SerializeField] private GridMap grid;
    [Header("Rope cut blocker")]
    public RopeCutBlockerManager ropeCut;  // kéo vào manager ở trên (để trống sẽ tự tìm)
    public bool clampBeforeCut = false;    // nếu true: kẹp lại ngay trước giao điểm (xấp xỉ)
    public ObiRope ownerRope;

    [Header("Drag options")]
    public Vector3 extraOffset = Vector3.zero;   // offset nhỏ theo ý muốn
    public bool forbidCornerCut = true;          // chặn cắt góc khi đi chéo
    public float bottomExtraOffset = 0f;         // bù nếu sàn thực tế cao hơn yLevel

    [Header("Obstacle check")]
    public string obstacleTag = "Obstacle";      // nếu bạn đang dùng "obtacle", sửa ở đây
    public float obstacleProbeHeight = 5f;       // cao bao nhiêu để bắn ray từ trên xuống

    private bool _dragging;
    private Vector2Int _lastCell;

    void Awake()
    {
        grid = ResolveGrid();
        if (grid == null)
        {
            Debug.LogError("[DragToGrid] Không tìm thấy GridMap trong scene.");
            enabled = false;
            return;
        }
        if (!ownerRope) ownerRope = GetComponentInParent<ObiRope>();
        if (!ropeCut) ropeCut = FindObjectOfType<RopeCutBlockerManager>(true);
        // đăng ký chiếm ô ban đầu
        _lastCell = grid.WorldToCell(transform.position);
        grid.Occupy(_lastCell, transform);
    }

    void OnDisable()
    {
        if (grid != null) grid.Vacate(_lastCell, transform);
    }

    public void OnMouseDown()
    {
        if (grid == null) grid = ResolveGrid();
        if (grid == null) return;

        _dragging = true;

        // bỏ chiếm ô hiện tại để không tự chặn mình trong lúc kéo
        grid.Vacate(_lastCell, transform);
    }

    public void OnMouseDrag()
    {
        if (!_dragging || grid == null) return;
        if (!TryGetMouseWorld(out var mouseWorld) || IsBlockedAt(mouseWorld))
            return;

        var target = mouseWorld + extraOffset;
        var targetCell = grid.WorldToCell(target);
        if (ropeCut && ropeCut.IsCellBlocked(targetCell))
        {
            return;
        }

        // 1) CHẶN rope cut:
        if (ropeCut && ropeCut.SegmentCutsAnyRope(transform.position, target))
        {
            if (clampBeforeCut)
            {
                // Tiến dần theo tham số để đến sát trước khi cắt (binary search đơn giản)
                Vector3 p = transform.position;
                Vector3 q = target;
                Vector3 lo = p, hi = q;
                for (int i = 0; i < 12; i++) // 12 vòng là đủ mịn
                {
                    Vector3 mid = Vector3.Lerp(lo, hi, 0.5f);
                    if (ropeCut.SegmentCutsAnyRope(p, mid)) hi = mid; else lo = mid;
                }
                target = lo; // kẹp về điểm an toàn
            }
            else
            {
                return; // không cho di luôn
            }
        }

        // 2) Chạy qua logic grid (chặn lách ô + bottom align như bạn có)
        var nextPos = grid.ConstrainedDrag(
            transform.position,
            target,
            transform,
            forbidCornerCut,
            bottomExtraOffset
        );

        transform.position = nextPos;
        _lastCell = grid.WorldToCell(nextPos);
    }


    public void OnMouseUp()
    {
        if (!_dragging || grid == null) return;
        _dragging = false;

        // snap lần cuối theo đáy + chiếm ô
        var snap = grid.CellToWorldBottomAligned(_lastCell, transform, bottomExtraOffset);
        transform.position = snap;
        grid.Occupy(_lastCell, transform);
        RopeCleaner.instance.Check();
    }

    // ---------------- helpers ----------------

    private GridMap ResolveGrid()
    {
        if (grid != null) return grid;
        grid = GridMap.Instance;
        if (grid != null) return grid;
        var all = FindObjectsOfType<GridMap>(true);
        if (all.Length > 0) grid = all[0];
        return grid;
    }

    /// Lấy vị trí chuột trên mặt lưới (ưu tiên raycast collider sàn, fallback mặt phẳng yLevel).
    private bool TryGetMouseWorld(out Vector3 world)
    {
        world = Vector3.zero;
        var cam = Camera.main;
        if (!cam) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // 1) Nếu bắn trúng collider nào đó thì dùng luôn
        if (Physics.Raycast(ray, out RaycastHit hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
        {
            world = hit.point;
            return true;
        }

        // 2) Fallback: giao cắt tia với mặt phẳng y = grid.yLevel
        var plane = new Plane(Vector3.up, new Vector3(0f, grid.yLevel, 0f));
        if (plane.Raycast(ray, out float t))
        {
            world = ray.origin + ray.direction * t;
            return true;
        }

        return false;
    }

    /// Raycast xuống từ trên cao ở vị trí probePoint để xem dưới chân có obstacle không.
    private bool IsBlockedAt(Vector3 probePoint)
    {
        Vector3 origin = probePoint + Vector3.up * obstacleProbeHeight;
        float dist = obstacleProbeHeight * 2f;

        // RaycastAll để bỏ qua chính mình (self)
        var hits = Physics.RaycastAll(origin, Vector3.down, dist, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            // bỏ qua collider của bản thân (kể cả con)
            if (h.collider && (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)))
                continue;

            if (h.collider != null && h.collider.CompareTag(obstacleTag))
                return true;
        }
        return false;
    }
}
