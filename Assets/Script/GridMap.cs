using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[ExecuteAlways]                 // để OnEnable chạy cả ở Edit mode
[DefaultExecutionOrder(-10000)]
public class GridMap : MonoBehaviour
{
    public static GridMap Instance { get; private set; }
    public float cellSize = 1f;
    public float yLevel = 0f;
    public Vector3 origin = Vector3.zero;

    // Ô đang bị chiếm (1 object/ô)
    private readonly Dictionary<Vector2Int, Transform> _occupancy = new();

    void OnEnable() => Instance = this;
    void OnDisable() { if (Instance == this) Instance = null; }
    public static GridMap GetOrFind()
    {
        if (Instance) return Instance;
#if UNITY_EDITOR
        // Tìm trong các scene đang mở (kể cả inactive)
        var grids = Object.FindObjectsOfType<GridMap>(true);
        return grids.Length > 0 ? grids[0] : null;
#else
        return null;
#endif
    }
    public Vector2Int WorldToCell(Vector3 world)
    {
        var local = world - origin;
        int cx = Mathf.FloorToInt(local.x / cellSize);
        int cy = Mathf.FloorToInt(local.z / cellSize);
        return new Vector2Int(cx, cy);
    }

    public Vector3 CellToWorldCenter(Vector2Int c)
    {
        return origin + new Vector3((c.x + 0.5f) * cellSize, yLevel, (c.y + 0.5f) * cellSize);
    }

    public bool IsOccupied(Vector2Int c, Transform ignore = null)
    {
        if (!_occupancy.TryGetValue(c, out var t) || t == null) return false;
        return t != ignore;
    }

    public void Occupy(Vector2Int c, Transform who) => _occupancy[c] = who;
    public void Vacate(Vector2Int c, Transform who) { if (IsOccupied(c, who)) _occupancy.Remove(c); }

    // ==== Căn theo đáy object (không chôn xuống đất) ====
    public static Bounds GetWorldBounds(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>(true);
        if (rs.Length > 0)
        {
            var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); return b;
        }
        var cs = t.GetComponentsInChildren<Collider>(true);
        if (cs.Length > 0)
        {
            var b = cs[0].bounds; for (int i = 1; i < cs.Length; i++) b.Encapsulate(cs[i].bounds); return b;
        }
        return new Bounds(t.position, Vector3.one * 0.001f);
    }
    public static float PivotToBottom(Transform t)
    {
        var b = GetWorldBounds(t);
        return t.position.y - b.min.y;
    }
    public Vector3 CellToWorldBottomAligned(Vector2Int cell, Transform obj, float extraOffset = 0f)
    {
        var p = CellToWorldCenter(cell);                 // XZ ở tâm ô
        p.y = yLevel + PivotToBottom(obj) + extraOffset; // Y = đáy sát mặt
        return p;
    }

    // ==== Linecast trên lưới: từ from -> to, dừng ngay trước ô đầu tiên bị chiếm ====
    public Vector2Int FurthestFreeCellOnPath(Vector2Int from, Vector2Int to, Transform ignore = null, bool forbidCornerCut = true)
    {
        if (IsOccupied(from, ignore)) return from;

        int x0 = from.x, y0 = from.y;
        int x1 = to.x, y1 = to.y;

        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        var c = from;
        var lastFree = from;

        while (true)
        {
            if (c != from && IsOccupied(c, ignore)) return lastFree;
            if (c == to) return c;

            int e2 = 2 * err;
            bool stepX = e2 >= dy;
            bool stepY = e2 <= dx;

            var prev = c;
            if (stepX) { err += dy; c.x += sx; }
            if (stepY) { err += dx; c.y += sy; }

            // Cấm cắt góc: nếu đi chéo, 1 trong 2 ô cạnh bị chiếm -> chặn
            if (forbidCornerCut && stepX && stepY)
            {
                var sideA = new Vector2Int(prev.x + sx, prev.y);
                var sideB = new Vector2Int(prev.x, prev.y + sy);
                if (IsOccupied(sideA, ignore) || IsOccupied(sideB, ignore))
                    return lastFree;
            }

            lastFree = c;
        }
    }

    // ==== API kéo: trả về vị trí thế giới hợp lệ (đặt theo đáy) ====
    public Vector3 ConstrainedDrag(Vector3 currentWorld, Vector3 targetWorld, Transform obj, bool forbidCornerCut = true, float extraOffsetY = 0f)
    {
        var from = WorldToCell(currentWorld);
        var to = WorldToCell(targetWorld);
        var allowedCell = FurthestFreeCellOnPath(from, to, obj, forbidCornerCut);
        return CellToWorldBottomAligned(allowedCell, obj, extraOffsetY);
    }
    // ===== Editor-time helpers =====
#if UNITY_EDITOR
    // Tránh đè lên các ô đang bị "những GridOccupant khác" dùng (Editor-time, không dựa vào _occupancy runtime)
    public Vector2Int FindNearestFreeCellAvoid(Vector2Int prefer, HashSet<Vector2Int> taken, int maxRadius = 8)
    {
        if (!taken.Contains(prefer)) return prefer;

        for (int r = 1; r <= maxRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                // duyệt viền hình vuông bán kính r
                int dyTop = r, dyBottom = -r;
                var c1 = new Vector2Int(prefer.x + dx, prefer.y + dyTop);
                if (!taken.Contains(c1)) return c1;
                var c2 = new Vector2Int(prefer.x + dx, prefer.y + dyBottom);
                if (!taken.Contains(c2)) return c2;
            }
            for (int dy = -r + 1; dy <= r - 1; dy++)
            {
                int dxRight = r, dxLeft = -r;
                var c1 = new Vector2Int(prefer.x + dxRight, prefer.y + dy);
                if (!taken.Contains(c1)) return c1;
                var c2 = new Vector2Int(prefer.x + dxLeft, prefer.y + dy);
                if (!taken.Contains(c2)) return c2;
            }
        }
        return prefer;
    }

    // Snap 1 occupant ngay trong Editor (không Play), tránh đè ô đang có occupant khác
    public static void TrySnapUniqueInEditor(GridOccupant occ)
    {
        var grid = Instance; if (!grid) return;

        var all = Object.FindObjectsOfType<GridOccupant>(true);
        // Tập ô đã có người (ngoại trừ chính occ)
        var taken = new HashSet<Vector2Int>();
        foreach (var o in all)
        {
            if (o == occ) continue;
            var ocell = grid.WorldToCell(o.transform.position);
            taken.Add(ocell);
        }

        var prefer = grid.WorldToCell(occ.transform.position);
        var chosen = grid.FindNearestFreeCellAvoid(prefer, taken);
        var pos = grid.CellToWorldBottomAligned(chosen, occ.transform, occ.bottomExtraOffset);

        Undo.RecordObject(occ.transform, "Snap Unique");
        occ.transform.position = pos;

        EditorSceneManager.MarkSceneDirty(occ.gameObject.scene);
    }

    // Snap toàn bộ occupants và đảm bảo unique cell (nút Bake)
    [ContextMenu("Bake Level to Grid (snap bottom + unique cells)")]
    public void BakeLevelToGrid()
    {
        var all = FindObjectsOfType<GridOccupant>(true)
                  .OrderBy(o => WorldToCell(o.transform.position).x)   // sắp theo trục để kết quả ổn định
                  .ThenBy(o => WorldToCell(o.transform.position).y)
                  .ToList();

        var taken = new HashSet<Vector2Int>();
        Undo.IncrementCurrentGroup();
        int g = Undo.GetCurrentGroup();

        foreach (var o in all)
        {
            var prefer = WorldToCell(o.transform.position);
            var chosen = FindNearestFreeCellAvoid(prefer, taken);
            var pos = CellToWorldBottomAligned(chosen, o.transform, o.bottomExtraOffset);

            Undo.RecordObject(o.transform, "Bake Level to Grid");
            o.transform.position = pos;

            taken.Add(chosen);
        }

        Undo.CollapseUndoOperations(g);
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[GridPlane] Baked {all.Count} occupants (unique cells).");
    }
#endif
}
