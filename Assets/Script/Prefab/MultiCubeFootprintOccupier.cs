using System.Collections.Generic;
using UnityEngine;

public class MultiCubeFootprintOccupier : MonoBehaviour
{
    public GridMap grid;
    public Transform owner;
    public bool autoRebuildFromChildren = true;
    public float gizmoSizeFactor = 0.85f;

    private readonly HashSet<Vector2Int> _offsets = new();
    private readonly HashSet<Vector2Int> _occupied = new();

    public IReadOnlyCollection<Vector2Int> OccupiedCells => _occupied;
    /// <summary>
    /// Lấy danh sách offsets (relative to pivot) mà shape này chiếm
    /// </summary>
    public IReadOnlyCollection<Vector2Int> GetCellOffsets()
    {
        return _offsets;
    }
    private void Reset()
    {
        if (!grid) grid = GridMap.Instance;
        if (!owner) owner = transform;
    }
    void OnEnable()
    {
        if (autoRebuildFromChildren) RebuildOffsetsFromChildren();
        UpdateOccupancyNow();
    }

    void OnDisable()
    {
        if (GridMap.Instance != null && owner != null)
            GridMap.Instance.RemoveOwner(owner);
        _offsets.Clear();
        _occupied.Clear();
    }
    void LateUpdate()
    {
        // Có thể để DraggableMultiCell gọi trực tiếp UpdateOccupancyNow().
        // Ở đây đảm bảo lúc không kéo mà bạn tự thay đổi vị trí bằng code, occupancy vẫn bám theo.
        UpdateOccupancyNow();
    }
    public void RebuildOffsetsFromChildren()
    {
        _offsets.Clear();
        if (grid == null) { return; }

        float cs = grid.cellSize;
        foreach (var unit in GetComponentsInChildren<CubeBase>())
        {
            // Lấy local pos của cube con → quy về đơn vị cell → làm tròn → offset (x,z)
            Vector3 lp = unit.transform.localPosition;
            var pos = grid.WorldToCell(lp);
            _offsets.Add(pos);
        }
    }

    public void UpdateOccupancyNow()
    {
        if (grid == null || owner == null || GridMap.Instance == null) return;

        if (autoRebuildFromChildren && Application.isEditor && !Application.isPlaying)
        {
            // Trong editor khi bạn di chuyển con, tự rebuild cho trực quan.
            RebuildOffsetsFromChildren();
        }

        _occupied.Clear();
        Vector2Int pivot = grid.WorldToCell(transform.position);
        foreach (var off in _offsets)
        { 
            _occupied.Add(off);
        }

        GridMap.Instance.ApplyOwner(owner, _occupied);
    }

    void OnDrawGizmosSelected()
    {
        if (grid == null || _offsets.Count == 0) return;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        foreach (var off in _offsets)
        {
            Vector3 c = grid.CellToWorldCenter(grid.WorldToCell(transform.position) + off);
            Gizmos.DrawCube(c, Vector3.one * (grid.cellSize * gizmoSizeFactor));
        }
    }
}
