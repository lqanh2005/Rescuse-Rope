using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GridMap : MonoBehaviour
{
    public static GridMap Instance { get; private set; }

    [Header("Grid")]
    public float cellSize = 0.5f;
    public float yLevel = 0f;
    public Vector3 origin = Vector3.zero;

    // cell -> set<owner>
    private readonly Dictionary<Vector2Int, HashSet<Transform>> _occ = new();

    void OnEnable() => Instance = this;
    void OnDisable() { if (Instance == this) Instance = null; }

    // ---------- Core Occupancy ----------
    public void Occupy(Vector2Int cell, Transform owner)
    {
        if (owner == null) return;
        if (!_occ.TryGetValue(cell, out var set)) _occ[cell] = set = new HashSet<Transform>();
        set.Add(owner);
    }

    public void Vacate(Vector2Int cell, Transform owner)
    {
        if (owner == null) return;
        if (_occ.TryGetValue(cell, out var set))
        {
            set.Remove(owner);
            if (set.Count == 0) _occ.Remove(cell);
        }
    }

    public bool IsOccupied(Vector2Int cell) => _occ.ContainsKey(cell);

    /// cell có bị chặn bởi ai KHÁC ownerIgnore không?
    public bool IsBlocked(Vector2Int cell, Transform ownerIgnore = null)
    {
        if (!_occ.TryGetValue(cell, out var set)) return false;
        if (ownerIgnore == null) return set.Count > 0;

        foreach (var o in set)
            if (!SameOwner(o, ownerIgnore)) return true; // có người khác
        return false; // chỉ chính mình
    }

    // Cập nhật theo diff (nhẹ, khuyến nghị)
    public void UpdateOccupiedCellsDiff(HashSet<Vector2Int> current, HashSet<Vector2Int> next, Transform owner)
    {
        // vacate những ô không còn
        foreach (var c in current)
            if (!next.Contains(c))
                Vacate(c, owner);

        // occupy ô mới
        foreach (var c in next)
            if (!current.Contains(c))
                Occupy(c, owner);

        current.Clear();
        foreach (var c in next) current.Add(c);
        next.Clear();
    }

    // ---------- Conversions ----------
    public Vector2Int WorldToCell(Vector3 world)
    {
        var local = world - origin;
        int cx = Mathf.FloorToInt(local.x / cellSize);
        int cy = Mathf.FloorToInt(local.z / cellSize);
        return new Vector2Int(cx, cy);
    }

    public Vector3 CellToWorldBottomAligned(Vector2Int cell, Transform obj, float bottomExtraOffset = 0f)
    {
        float x = origin.x + (cell.x + 0.5f) * cellSize;
        float z = origin.z + (cell.y + 0.5f) * cellSize;
        float y = yLevel + bottomExtraOffset;
        // nếu muốn căn theo đáy collider có thể cộng thêm halfHeight ở ngoài
        return new Vector3(x, y, z);
    }

    // ---------- Constrained drag (đi thẳng theo mouse nhưng tránh "cắt góc") ----------
    public Vector3 ConstrainedDrag(
        Vector3 from,
        Vector3 target,
        Transform mover,
        bool forbidCornerCut,
        float bottomExtraOffset = 0f)
    {
        // Ở đây mình giữ phiên bản tối giản: snap về tâm cell đích.
        // Nếu bạn đã có phiên bản đầy đủ, giữ lại bản của bạn.
        var cell = WorldToCell(target);
        var snap = CellToWorldBottomAligned(cell, mover, bottomExtraOffset);
        return snap;
    }

    // ---------- Utils ----------
    static bool SameOwner(Transform a, Transform b)
    {
        if (a == b) return true;
        // có thể chuẩn hoá theo root nếu bạn muốn group theo rope root
        return a.root == b.root;
    }
}
