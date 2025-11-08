using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;


#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class GridMap : MonoBehaviour
{
    public static GridMap Instance { get; private set; }

    [Header("Grid")]
    public float cellSize = 0.5f;
    public float yLevel = 0f;
    public Vector3 origin = Vector3.zero;
    const string ObstacleTag = "Obstacle";

    [Header("Gizmos (Editor)")]
    public bool showGridLines = true;
    public bool showOccupiedCells = true;
    public int gridDrawRadius = 20;                 // vẽ lưới trong phạm vi N ô quanh origin
    public Color gridLineColor = new Color(1, 1, 1, 0.1f);
    public Color occupiedFillColor = new Color(1f, 0.35f, 0.0f, 0.30f);
    public bool labelOwnerInCell = false;

    // ---------- Occupancy Data ----------
    public bool trackOwners = false;
    private readonly Dictionary<Vector2Int, int> counts = new();
    private readonly Dictionary<object, HashSet<Vector2Int>> byOwner = new();
    private readonly Dictionary<Vector2Int, HashSet<Transform>> _occ = new();
    private readonly Dictionary<Vector2Int, HashSet<object>> _ownersByCell = new();

    private readonly HashSet<Vector2Int> _all = new();

    //public void ApplyOwner(object owner, IReadOnlyCollection<Vector2Int> newCells)
    //{
    //    if (!byOwner.TryGetValue(owner, out var old))
    //    {
    //        old = new HashSet<Vector2Int>();
    //        byOwner[owner] = old;
    //    }

    //    // Tính delta: removed = old - new, added = new - old
    //    // (tránh cấp phát: có thể tái sử dụng bộ nhớ nếu muốn, ở đây viết gọn cho rõ)
    //    var removed = new List<Vector2Int>();
    //    foreach (var c in old) if (!newCells.Contains(c)) removed.Add(c);

    //    var added = new List<Vector2Int>();
    //    foreach (var c in newCells) if (!old.Contains(c)) added.Add(c);

    //    // Áp dụng removed
    //    foreach (var c in removed)
    //    {
    //        old.Remove(c);
    //        Decrement(c);
    //    }

    //    // Áp dụng added
    //    foreach (var c in added)
    //    {
    //        old.Add(c);
    //        Increment(c);
    //    }
    //}
    public void ApplyOwner(object owner, IReadOnlyCollection<Vector2Int> cells)
    {
        if (!byOwner.TryGetValue(owner, out var old))
            byOwner[owner] = old = new HashSet<Vector2Int>();
        else
            foreach (var c in old) _all.Remove(c);

        old.Clear();
        foreach (var c in cells) { old.Add(c); _all.Add(c); }
    }
    //public void RemoveOwner(object owner)
    //{
    //    if (!byOwner.TryGetValue(owner, out var set)) return;
    //    foreach (var c in set) Decrement(c);
    //    byOwner.Remove(owner);
    //}
    public void RemoveOwner(object owner)
    {
        if (byOwner.TryGetValue(owner, out var old))
        {
            foreach (var c in old) _all.Remove(c);
            byOwner.Remove(owner);
        }
    }

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
    public bool IsOccupied(Vector2Int cell, object ignoreOwner = null)
    {
        // Nhanh: nếu không có ignoreOwner thì tra _all
        if (ignoreOwner == null) return _all.Contains(cell);

        // Có bỏ qua chính mình: kiểm tra từng owner khác
        foreach (var kv in byOwner)
        {
            if (ReferenceEquals(kv.Key, ignoreOwner)) continue;
            if (kv.Value.Contains(cell)) return true;
        }
        return false;
    }
    //public bool IsOccupied(Vector2Int cell, object ignoreOwner = null)
    //{
    //    if (!counts.TryGetValue(cell, out int total) || total <= 0) return false;

    //    if (ignoreOwner == null) return true;
    //    // Nếu bỏ qua chính mình: xem owner này có đang nằm trong cell không
    //    if (byOwner.TryGetValue(ignoreOwner, out var mine) && mine.Contains(cell))
    //    {
    //        return (total - 1) > 0; // còn ai khác chiếm?
    //    }
    //    return true;
    //}
    public bool TryFirstBlockedOnLine(Vector2Int start, Vector2Int end, object ignoreOwner, out Vector2Int blocked)
    {
        foreach (var c in LineCells(start, end))
        {
            if (IsOccupied(c, ignoreOwner))
            {
                blocked = c;
                return true;
            }
        }
        blocked = default;
        return false;
    }
    public Vector2Int LastFreeBeforeBlocked(Vector2Int start, Vector2Int end, object ignoreOwner)
    {
        Vector2Int lastFree = start;
        foreach (var c in LineCells(start, end)) // dùng hàm DDA line đã có trong code bạn
        {
            if (c == start) continue;

            // Bị Obstacle? cấm qua (bỏ qua chính mình nếu có)
            Transform ignoreTransform = ignoreOwner as Transform;
            if (IsObstacleCell(c, ignoreTransform)) break;

            // Bị chiếm (khác owner của mình)? cấm qua
            if (IsOccupied(c, ignoreOwner)) break;

            lastFree = c;
            if (c == end) break;
        }
        return lastFree;
    }
    public static IEnumerable<Vector2Int> LineCells(Vector2Int a, Vector2Int b)
    {
        int x = a.x, y = a.y;
        int dx = Mathf.Abs(b.x - a.x), dy = Mathf.Abs(b.y - a.y);
        int sx = a.x < b.x ? 1 : -1;
        int sy = a.y < b.y ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            yield return new Vector2Int(x, y);
            if (x == b.x && y == b.y) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }
    public bool IsObstacleCell(Vector2Int cell, Transform ignoreOwner = null)
    {
        // Kiểm tra trong occupancy trước
        if (TryGetOwner(cell, out var owner, ignoreOwner))
        {
            if (owner != null && owner.CompareTag(ObstacleTag)) return true;
        }
        
        // Nếu không tìm thấy trong occupancy, tìm trực tiếp trong scene
        // (trường hợp obstacle chưa được đăng ký vào GridMap)
        Vector3 cellCenter = CellToWorldCenter(cell);
        Collider[] colliders = Physics.OverlapSphere(cellCenter, cellSize * 0.4f);
        foreach (var col in colliders)
        {
            if (col == null) continue;
            // Bỏ qua chính mình nếu có ignoreOwner
            if (ignoreOwner != null && col.transform == ignoreOwner) continue;
            if (col.CompareTag(ObstacleTag))
            {
                return true;
            }
        }
        
        return false;
    }
    public bool TryGetOwner(Vector2Int cell, out Transform owner, Transform ignoreOwner = null)
    {
        owner = null;

        if (_occ.TryGetValue(cell, out var set) && set != null)
        {
            foreach (var t in set)
            {
                if (t == null) continue;
                if (t == ignoreOwner) continue;   // BỎ QUA CHÍNH MÌNH
                owner = t;
                return true;
            }
        }

        return false;
    }


    public IEnumerable<object> GetOwnersAt(Vector2Int cell)
    {
        if (!trackOwners) yield break;
        if (_ownersByCell.TryGetValue(cell, out var owners))
            foreach (var o in owners) yield return o;
    }
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
        foreach (var c in current) if (!next.Contains(c)) Vacate(c, owner);
        foreach (var c in next) if (!current.Contains(c)) Occupy(c, owner);
        current.Clear(); foreach (var c in next) current.Add(c); next.Clear();
    }
    /// Add: đánh dấu nhiều ô là bị chiếm (không đụng ô cũ của owner)
    public void MarkCellsOccupied(IEnumerable<Vector2Int> cells, Transform owner)
    {
        if (owner == null || cells == null) return;
        foreach (var cell in cells) Occupy(cell, owner);
    }
    /// Set/Replace: xoá mọi ô hiện đang thuộc owner, sau đó đánh dấu ô mới
    public void ReplaceOccupiedCells(IEnumerable<Vector2Int> newCells, Transform owner)
    {
        if (owner == null) return;

        // tìm tất cả cell mà owner đang chiếm
        var toClear = ListCellsOwnedBy(owner);
        foreach (var cell in toClear) Vacate(cell, owner);

        // đánh dấu cell mới
        if (newCells != null)
            foreach (var cell in newCells) Occupy(cell, owner);
    }
    /// Lấy danh sách tất cả cell mà owner đang chiếm
    public List<Vector2Int> ListCellsOwnedBy(Transform owner)
    {
        var result = new List<Vector2Int>();
        if (owner == null) return result;

        foreach (var kv in _occ)
        {
            var set = kv.Value;
            if (set.Contains(owner)) result.Add(kv.Key);
        }
        return result;
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
    public Vector3 CellToWorldCenter(Vector2Int cell)
    {
        return new Vector3(
            origin.x + (cell.x + 0.5f) * cellSize,
            yLevel,
            origin.z + (cell.y + 0.5f) * cellSize);
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
    void OnDrawGizmos()
    {
        // vẽ đường lưới
        if (showGridLines)
        {
            Gizmos.color = gridLineColor;
            float cs = cellSize;
            Vector3 o = origin;
            float y = yLevel;

            int r = Mathf.Max(1, gridDrawRadius);
            int minX = -r, maxX = r;
            int minY = -r, maxY = r;

            // dọc X
            for (int x = minX; x <= maxX; x++)
            {
                Vector3 a = new Vector3(o.x + x * cs, y, o.z + minY * cs);
                Vector3 b = new Vector3(o.x + x * cs, y, o.z + maxY * cs);
                Gizmos.DrawLine(a, b);
            }
            // dọc Y (Z)
            for (int yidx = minY; yidx <= maxY; yidx++)
            {
                Vector3 a = new Vector3(o.x + minX * cs, y, o.z + yidx * cs);
                Vector3 b = new Vector3(o.x + maxX * cs, y, o.z + yidx * cs);
                Gizmos.DrawLine(a, b);
            }
        }

        // vẽ fill các cell đang bị chiếm
        if (showOccupiedCells && _occ != null)
        {
            float cs = cellSize;
            Vector3 o = origin;
            float y = yLevel;

            foreach (var kv in _occ)
            {
                var cell = kv.Key;
                // tâm cell
                Vector3 center = new Vector3(
                    o.x + (cell.x + 0.5f) * cs,
                    y,
                    o.z + (cell.y + 0.5f) * cs);

                // khối mỏng (fill) — cao 0.01
                Vector3 size = new Vector3(cs, 0.01f, cs);
                Gizmos.color = occupiedFillColor;
                Gizmos.DrawCube(center, size);

#if UNITY_EDITOR
                if (labelOwnerInCell)
                {
                    // ghép tên owners gọn gàng
                    if (kv.Value != null && kv.Value.Count > 0)
                    {
                        // tạo màu ổn định theo owner đầu tiên
                        var any = GetFirst(kv.Value);
                        var col = StableColor(any);
                        col.a = 0.9f;

                        Handles.color = col;
                        GUIStyle s = new GUIStyle(EditorStyles.boldLabel);
                        s.normal.textColor = col;

                        // tối đa 1–2 tên để không rối
                        string label = "";
                        int i = 0;
                        foreach (var t in kv.Value)
                        {
                            label += (i > 0 ? "\n" : "") + (t ? t.name : "(null)");
                            if (++i >= 2) break;
                        }
                        if (kv.Value.Count > 2) label += "\n…";

                        Handles.Label(center + Vector3.up * 0.02f, label, s);
                    }
                }
#endif
            }
        }
    }

    static Transform GetFirst(HashSet<Transform> set)
    {
        foreach (var t in set) return t;
        return null;
    }

#if UNITY_EDITOR
    // màu ổn định theo hash transform (để phân biệt owners)
    static Color StableColor(Transform t)
    {
        if (!t) return Color.white;
        int h = t.GetInstanceID();
        float r = ((h * 0.13f) % 1000) / 1000f;
        float g = ((h * 0.37f) % 1000) / 1000f;
        float b = ((h * 0.73f) % 1000) / 1000f;
        return new Color(r, g, b, 1f);
    }
#endif
}
