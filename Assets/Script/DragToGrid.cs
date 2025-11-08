using UnityEngine;
using static UnityEngine.Rendering.GPUPrefixSum;

[RequireComponent(typeof(Collider))]
public class DragToGrid : MonoBehaviour
{
    public GridMap grid;                 // kéo vào, hoặc để trống sẽ tự lấy Instance
    public Transform owner;              // đại diện của cube này (thường là chính transform)
    public Vector3 extraOffset = Vector3.zero;

    private bool _dragging;
    private Vector2Int _lastCell;
    private Vector2Int _startCell;
    const string ObstacleTag = "Obstacle";

    void Awake()
    {
        if (!grid) grid = GridMap.Instance ? GridMap.Instance : FindObjectOfType<GridMap>(true);
        if (!owner) owner = transform;
        _lastCell = grid.WorldToCell(transform.position);
        grid.Occupy(_lastCell, owner);
    }
    void OnDisable()
    {
        if (grid) grid.Vacate(_lastCell, owner);
    }

    public void OnMouseDown()
    {
        _dragging = true;
        _startCell = grid.WorldToCell(transform.position);
        grid.Vacate(_lastCell, owner); // bỏ chiếm ô hiện tại để kéo qua chính mình
    }

    void OnMouseDrag()
    {
        if (!_dragging || grid == null || GridMap.Instance == null) return;

        // 1) Lấy cell trỏ chuột trên mặt phẳng lưới (giả sử lưới XZ)
        if (!TryGetMouseOnGrid(out var mouseWorld)) return;
        Vector2Int target = grid.WorldToCell(mouseWorld);

        // 2) Cell hiện tại của object
        Vector2Int current = grid.WorldToCell(transform.position);
        if(GridMap.Instance.IsObstacleCell(target, owner)) return;

        // 3) Tìm ô “cuối cùng còn trống” dọc đường current → target, BỎ QUA chính mình
        Vector2Int reachable = GridMap.Instance.LastFreeBeforeBlocked(current, target, ignoreOwner: owner);

        // 4) Snap về tâm ô reachable
        transform.position = grid.CellToWorldCenter(reachable);
    }

    bool TryGetMouseOnGrid(out Vector3 world)
    {
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        // mặt phẳng y = grid.yLevel
        float t = (grid.yLevel - ray.origin.y) / ray.direction.y;
        if (t > 0f)
        {
            world = ray.origin + ray.direction * t;
            return true;
        }
        world = default;
        return false;
    }

    public void OnMouseUp()
    {
        if (!_dragging) return;
        _dragging = false;
        grid.Occupy(_lastCell, owner); // chiếm lại ô cuối
        RopeCleaner.instance.Check();
    }

}
