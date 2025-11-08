using System.Collections.Generic;
using UnityEngine;

public static class GridNeighborScanner
{
    public static List<NeighborContact> ScanAround(
    Vector2Int targetOrigin,
    IReadOnlyList<Vector2Int> relativeCells,
    Transform ownerRef,
    System.Func<Vector2Int, bool> inBounds = null)
    {
        var gm = GridMap.Instance;
        var results = new List<NeighborContact>(relativeCells.Count);

        // >>> tập ô tuyệt đối của chính nhóm (để bỏ qua)
        var ownAbs = new HashSet<Vector2Int>();
        for (int i = 0; i < relativeCells.Count; i++)
            ownAbs.Add(targetOrigin + relativeCells[i]);

        foreach (var rel in relativeCells)
        {
            var cell = targetOrigin + rel;
            var info = new NeighborContact { cell = cell, sides = GridSide.None };

            CheckSide(cell + new Vector2Int(-1, 0), GridSide.Left, ref info.leftOwner, ref info.sides);
            CheckSide(cell + new Vector2Int(1, 0), GridSide.Right, ref info.rightOwner, ref info.sides);
            CheckSide(cell + new Vector2Int(0, 1), GridSide.Up, ref info.upOwner, ref info.sides);
            CheckSide(cell + new Vector2Int(0, -1), GridSide.Down, ref info.downOwner, ref info.sides);

            results.Add(info);
        }
        return results;

        void CheckSide(Vector2Int c, GridSide flag, ref Transform ownerSlot, ref GridSide mask)
        {
            // >>> BỎ QUA nếu là ô thuộc chính footprint (không tự chặn mình)
            if (ownAbs.Contains(c)) return;

            if (inBounds != null && !inBounds(c)) { mask |= flag; ownerSlot = null; return; }

            if (gm.IsObstacleCell(c, ownerRef)) { gm.TryGetOwner(c, out ownerSlot, ownerRef); mask |= flag; return; }
            if (gm.IsOccupied(c, ownerRef)) { gm.TryGetOwner(c, out ownerSlot, ownerRef); mask |= flag; }
        }
    }
}
public enum GridSide
{
    None = 0,
    Left = 1,   // -X
    Right = 2,   // +X
    Down = 4,   // -Y (trục Z world âm)
    Up = 8    // +Y (trục Z world dương)
}

public struct NeighborContact
{
    public Vector2Int cell;     // cell tuyệt đối của cube
    public GridSide sides;      // phía nào bị chặn
    public Transform leftOwner; // chủ thể chặn (nếu lấy được)
    public Transform rightOwner;
    public Transform upOwner;
    public Transform downOwner;

    public bool IsBlocked => sides != GridSide.None;
}
