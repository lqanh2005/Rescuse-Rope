using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GridPlane : MonoBehaviour
{
    public static GridPlane Instance { get; private set; }

    [Header("Grid Settings")]
    public float cellSize = 0.5f;
    public int width = 20;
    public int height = 20;
    public float yLevel = 0f;           // mặt phẳng ngang Y = yLevel
    public Vector3 origin = Vector3.zero;
    public bool showGizmos = true;

    [Header("Occupancy (chống trùng ô)")]
    public bool enforceUniqueAnchors = true;

    // Lưu chỗ đã có anchor
    private readonly Dictionary<Vector2Int, Transform> _occupancy = new();

    void OnEnable() => Instance = this;
    void OnDisable() { if (Instance == this) Instance = null; }

    public Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = world - origin;
        int cx = Mathf.RoundToInt(local.x / cellSize);
        int cz = Mathf.RoundToInt(local.z / cellSize);
        // clamp vào bounds
        cx = Mathf.Clamp(cx, 0, width - 1);
        cz = Mathf.Clamp(cz, 0, height - 1);
        return new Vector2Int(cx, cz);
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        float x = origin.x + cell.x * cellSize;
        float z = origin.z + cell.y * cellSize;
        return new Vector3(x, yLevel, z);
    }

    public Vector3 Snap(Vector3 world)
    {
        return CellToWorld(WorldToCell(world));
    }

    public bool IsCellFree(Vector2Int cell, Transform requester = null)
    {
        if (!enforceUniqueAnchors) return true;
        if (!_occupancy.TryGetValue(cell, out var t)) return true;
        return t == null || t == requester;
    }

    public bool TryReserveCell(Vector2Int cell, Transform who)
    {
        if (!enforceUniqueAnchors) return true;
        if (!IsCellFree(cell, who)) return false;
        _occupancy[cell] = who;
        return true;
    }

    public void ReleaseCell(Vector2Int cell, Transform who)
    {
        if (!enforceUniqueAnchors) return;
        if (_occupancy.TryGetValue(cell, out var t) && t == who)
            _occupancy.Remove(cell);
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        Gizmos.color = new Color(1f, 1f, 1f, 0.2f);

        Vector3 start = new Vector3(origin.x, yLevel, origin.z);
        // vẽ ô
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector3 c = start + new Vector3(x * cellSize, 0, z * cellSize);
                Vector3 a = c;
                Vector3 b = c + new Vector3(cellSize, 0, 0);
                Vector3 d = c + new Vector3(0, 0, cellSize);
                Gizmos.DrawLine(a, b);
                Gizmos.DrawLine(a, d);
            }
        }
        // viền ngoài
        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Vector3 p0 = start;
        Vector3 p1 = start + new Vector3(width * cellSize, 0, 0);
        Vector3 p2 = start + new Vector3(width * cellSize, 0, height * cellSize);
        Vector3 p3 = start + new Vector3(0, 0, height * cellSize);
        Gizmos.DrawLine(p0, p1); Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3); Gizmos.DrawLine(p3, p0);
    }
}
