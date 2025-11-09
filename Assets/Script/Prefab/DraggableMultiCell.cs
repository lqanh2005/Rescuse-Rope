using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MultiCubeFootprintOccupier))]
public class DraggableMultiCell : MonoBehaviour
{
    public GridMap grid;
    private MultiCubeFootprintOccupier _occupier;
    public List<CubeBase> cubes = new();
    public Transform ropeOwner;
    public bool blockMoveIfAnySideBusy = true;

    private bool _dragging;
    private Plane _dragPlane;
    private Vector3 _grabOffsetWS; // giữ khoảng lệch grab để kéo mượt
    public Vector2Int _origin;

    void Reset()
    {
        if (!grid) grid = GridMap.Instance;
    }

    void Awake()
    {
        _occupier = GetComponent<MultiCubeFootprintOccupier>();
        if (!grid) grid = GridMap.Instance;
    }

    public void BeginDrag(Camera cam)
    {
        if (grid == null || cam == null) return;

        _dragPlane = new Plane(Vector3.up, new Vector3(0, grid.yLevel, 0));
        if (RayToPlane(cam, out Vector3 hit))
        {
            _grabOffsetWS = transform.position - hit;
            _dragging = true;
        }
    }
    public List<Vector2Int> directions = new()
    {
        new Vector2Int(-1, 0), // Left
        new Vector2Int(1, 0),  // Right
        new Vector2Int(0, 1),  // Up
        new Vector2Int(0, -1)  // Down
    };
    public bool CheckNeighBor(Vector2Int stepDir, List<CubeBase> items)
    {
        var ownCells = new HashSet<Vector2Int>();
        foreach (var cell in items) ownCells.Add(GridMap.Instance.WorldToCell(cell.transform.position));
        foreach (var c in items)
        {
            var baseCell = GridMap.Instance.WorldToCell(c.transform.position);
            var neighbor = baseCell + stepDir;

            // nếu neighbor là 1 ô của chính cụm -> bỏ qua
            if (ownCells.Contains(neighbor)) continue;

            // nếu bị obstacle hoặc bị owner khác chiếm -> bước này KHÔNG đi được
            if (GridMap.Instance.IsObstacleCell(neighbor, ropeOwner)) return true;
            if (GridMap.Instance.IsOccupied(neighbor, ropeOwner)) return true;
        }
        return false;
    }
    public void Drag(Camera cam)
    {
        if (!_dragging || grid == null || cam == null) return;

        if (RayToPlane(cam, out Vector3 hit))
        {
            Vector3 target = hit + _grabOffsetWS;
            Vector2Int targetOrigin = grid.WorldToCell(target);
            var d = targetOrigin - _origin;
            Vector2Int stepDir = Vector2Int.zero;
            if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
                stepDir = new Vector2Int(Mathf.Clamp(d.x, -1, 1), 0);
            else
                stepDir = new Vector2Int(0, Mathf.Clamp(d.y, -1, 1));
            if (stepDir == Vector2Int.zero) return;
            if (CheckNeighBor(stepDir, cubes)) return;

            // Cho phép di chuyển đến origin mới
            _origin = targetOrigin;
            transform.position = GridMap.Instance.CellToWorldCenter(_origin);

            // (tùy bạn) cập nhật vị trí world của từng cube theo relativeCell:
            foreach (var c in cubes)
            {
                var cell = _origin + c.relativeCell;
                c.transform.position = GridMap.Instance.CellToWorldCenter(cell);
            }
        }
    }

    public void EndDrag()
    {
        _dragging = false;
        RopeCleaner.instance.Check();
    }

    private bool RayToPlane(Camera cam, out Vector3 hit)
    {
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (_dragPlane.Raycast(ray, out float enter))
        {
            hit = ray.GetPoint(enter);
            return true;
        }
        hit = default;
        return false;
    }
}
