using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DragHandleToGrid : MonoBehaviour
{
    [Header("Dragging Plane")]
    public Vector3 planeNormal = Vector3.up; // kéo trên mặt phẳng ngang
    public float catchRadius = 0.001f;       // ổn định tính toán

    Plane _plane;
    bool _dragging;
    Vector3 _grabOffset;
    Vector2Int _currentCell;
    bool _hasCell;

    void Start()
    {
        var grid = GridPlane.Instance;
        if (grid == null)
        {
            Debug.LogError("Grid3D not found in scene. Add Grid3D first.");
            enabled = false; return;
        }
        // khởi tạo plane đi qua yLevel
        Vector3 planePoint = new Vector3(transform.position.x, grid.yLevel, transform.position.z);
        _plane = new Plane(planeNormal, planePoint);

        // Đặt anchor vào cell gần nhất lúc start
        SnapToNearestCell(force: true);
    }

    void OnMouseDown()
    {
        if (GridPlane.Instance == null) return;

        // plane update theo yLevel hiện tại
        var grid = GridPlane.Instance;
        Vector3 planePoint = new Vector3(transform.position.x, grid.yLevel, transform.position.z);
        _plane = new Plane(planeNormal, planePoint);

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (_plane.Raycast(ray, out float enter))
        {
            var hit = ray.GetPoint(enter);
            _grabOffset = transform.position - hit;
            _dragging = true;

            // giải phóng cell hiện tại (để có thể quét qua cell khác)
            if (_hasCell)
                grid.ReleaseCell(_currentCell, transform);
        }
    }

    void OnMouseUp()
    {
        _dragging = false;
        // thả: snap & reserve cell
        SnapToNearestCell(force: true);
    }

    void Update()
    {
        if (!_dragging || GridPlane.Instance == null) return;

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (_plane.Raycast(ray, out float enter))
        {
            var hit = ray.GetPoint(enter);
            Vector3 target = hit + _grabOffset;

            // giữ ở đúng yLevel
            target.y = GridPlane.Instance.yLevel;

            // Snap “thử” để xem cell có rảnh không
            Vector2Int cell = GridPlane.Instance.WorldToCell(target);
            if (GridPlane.Instance.IsCellFree(cell, transform))
            {
                Vector3 snapped = GridPlane.Instance.CellToWorld(cell);
                transform.position = snapped;
                _currentCell = cell;
                _hasCell = true;
            }
            else
            {
                // nếu cell đang bị chiếm, cứ hiển thị vị trí “gần đúng” (không snap),
                // player kéo thêm một chút sẽ rơi vào cell khác trống.
                transform.position = target;
                _hasCell = false;
            }
        }
    }

    void SnapToNearestCell(bool force)
    {
        var grid = GridPlane.Instance;
        if (grid == null) return;

        Vector2Int cell = grid.WorldToCell(transform.position);
        if (force)
        {
            // bắt buộc ghim vào cell hợp lệ gần nhất (nếu đang enforce unique thì thử trước)
            if (grid.IsCellFree(cell, transform) && grid.TryReserveCell(cell, transform))
            {
                transform.position = grid.CellToWorld(cell);
                _currentCell = cell; _hasCell = true;
            }
            else
            {
                // tìm cell lân cận trống
                if (FindNearestFreeCell(cell, out var free))
                {
                    grid.TryReserveCell(free, transform);
                    transform.position = grid.CellToWorld(free);
                    _currentCell = free; _hasCell = true;
                }
                else
                {
                    // không tìm thấy cell trống, giữ nguyên (trường hợp hiếm)
                    _hasCell = false;
                }
            }
        }
        else
        {
            transform.position = grid.CellToWorld(cell);
            _currentCell = cell; _hasCell = true;
        }
    }

    bool FindNearestFreeCell(Vector2Int from, out Vector2Int freeCell)
    {
        var grid = GridPlane.Instance;
        int maxR = Mathf.Max(grid.width, grid.height);
        for (int r = 0; r <= maxR; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                int dz = r - Mathf.Abs(dx);
                // 4 điểm trên kim cương Manhattan
                TryCell(from + new Vector2Int(dx, dz), out var found1);
                if (found1) { freeCell = from + new Vector2Int(dx, dz); return true; }
                if (dz != 0)
                {
                    TryCell(from + new Vector2Int(dx, -dz), out var found2);
                    if (found2) { freeCell = from + new Vector2Int(dx, -dz); return true; }
                }
            }
        }
        freeCell = default; return false;

        void TryCell(Vector2Int c, out bool ok)
        {
            ok = c.x >= 0 && c.x < grid.width && c.y >= 0 && c.y < grid.height && grid.IsCellFree(c, transform);
        }
    }

    void OnDisable()
    {
        if (_hasCell && GridPlane.Instance != null)
            GridPlane.Instance.ReleaseCell(_currentCell, transform);
    }
}
