using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class GridPlane : MonoBehaviour
{
    [Header("Kích thước lưới (số ô)")]
    public int cellsX = 10;
    public int cellsZ = 10;

    [Header("Kích thước mỗi ô (m)")]
    public float cellSize = 0.25f;

    [Header("Hiển thị Gizmos")]
    public bool drawGizmos = true;
    public Color gridColor = new Color(0f, 0f, 0f, 0.25f);

    Dictionary<Vector2Int, DragHandleToGrid> occupied = new();
    // Trả về điểm snap gần nhất trên lưới (theo world space)
    public Vector3 SnapWorldPoint(Vector3 worldPoint)
    {
        // Đưa về local theo plane
        var local = transform.InverseTransformPoint(worldPoint);

        // Lưới nằm trên mặt phẳng local XZ của plane (Y = 0)
        // Tâm lưới đặt tại origin của plane (bạn có thể offset nếu muốn)
        float halfX = (cellsX * cellSize) * 0.5f;
        float halfZ = (cellsZ * cellSize) * 0.5f;

        // Kẹp trong phạm vi lưới (tuỳ bạn: có thể bỏ kẹp nếu muốn ngoài biên vẫn snap)
        float x = Mathf.Clamp(local.x, -halfX, halfX);
        float z = Mathf.Clamp(local.z, -halfZ, halfZ);

        // Snap về bội số cellSize
        x = Mathf.Round(x / cellSize) * cellSize;
        z = Mathf.Round(z / cellSize) * cellSize;

        // Y luôn = 0 trên plane-local
        var snappedLocal = new Vector3(x, 0f, z);
        return transform.TransformPoint(snappedLocal);
    }

    // Lấy hit point từ một ray (ScreenPointToRay chẳng hạn) lên plane này
    public bool RaycastToPlane(Ray ray, out Vector3 hitPoint)
    {
        // Plane: pháp tuyến là transform.up, đi qua transform.position
        Plane p = new Plane(transform.up, transform.position);
        if (p.Raycast(ray, out float enter))
        {
            hitPoint = ray.GetPoint(enter);
            return true;
        }
        hitPoint = Vector3.zero;
        return false;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = gridColor;

        float halfX = (cellsX * cellSize) * 0.5f;
        float halfZ = (cellsZ * cellSize) * 0.5f;

        // Vẽ lưới trong local, rồi transform ra world
        // các trục local trên mặt phẳng:
        Vector3 origin = transform.position;
        Vector3 right = transform.right;
        Vector3 fwd = transform.forward;

        // biên
        Vector3 min = origin - right * halfX - fwd * halfZ;
        Vector3 max = origin + right * halfX + fwd * halfZ;

        // đường dọc (theo forward)
        for (int ix = 0; ix <= cellsX; ix++)
        {
            float x = -halfX + ix * cellSize;
            Vector3 a = origin + right * x - fwd * halfZ;
            Vector3 b = origin + right * x + fwd * halfZ;
            Gizmos.DrawLine(a, b);
        }
        // đường ngang (theo right)
        for (int iz = 0; iz <= cellsZ; iz++)
        {
            float z = -halfZ + iz * cellSize;
            Vector3 a = origin - right * halfX + fwd * z;
            Vector3 b = origin + right * halfX + fwd * z;
            Gizmos.DrawLine(a, b);
        }
    }
    Vector2Int WorldToCell(Vector3 worldPoint)
    {
        var local = transform.InverseTransformPoint(worldPoint);
        int cx = Mathf.RoundToInt(local.x / cellSize);
        int cz = Mathf.RoundToInt(local.z / cellSize);
        return new Vector2Int(cx, cz);
    }
    Vector3 CellToWorld(Vector2Int cell)
    {
        var local = new Vector3(cell.x * cellSize, 0f, cell.y * cellSize);
        return transform.TransformPoint(local);
    }

    public bool IsOccupied(Vector3 worldSnapped, out DragHandleToGrid owner)
    {
        var c = WorldToCell(worldSnapped);
        return occupied.TryGetValue(c, out owner);
    }
    public void ReserveOccupancyAt(Vector3 worldSnapped, DragHandleToGrid who)
    {
        var c = WorldToCell(worldSnapped);
        occupied[c] = who;
    }
    public void ReleaseOccupancyAt(Vector3 worldPos, DragHandleToGrid who)
    {
        var c = WorldToCell(worldPos);
        if (occupied.TryGetValue(c, out var curr) && curr == who)
            occupied.Remove(c);
    }

    // tìm ô trống gần nhất quanh 1 vị trí snap (xoắn ốc nhỏ)
    public Vector3 FindNearestFree(Vector3 nearWorldSnapped, int searchRadius = 3)
    {
        var c0 = WorldToCell(nearWorldSnapped);
        if (!occupied.ContainsKey(c0)) return nearWorldSnapped;

        for (int r = 1; r <= searchRadius; r++)
        {
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    // chỉ lấy “biên” hình vuông bán kính r
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                    var c = new Vector2Int(c0.x + dx, c0.y + dz);
                    if (!occupied.ContainsKey(c))
                        return CellToWorld(c);
                }
        }
        // hết chỗ → trả về cũ
        return CellToWorld(c0);
    }
}
