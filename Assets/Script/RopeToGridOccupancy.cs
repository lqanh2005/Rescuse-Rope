using System.Collections.Generic;
using UnityEngine;
using Obi;
using System;

[ExecuteAlways]
public class RopeToGridOccupancy : MonoBehaviour
{
    [Header("Refs")]
    public ObiRope rope;
    public GridMap grid;

    [Header("Projection & Raster")]
    [Tooltip("0 = lấy toàn bộ; >0 chỉ nhận đoạn có ít nhất 1 đầu cách yLevel <= ngưỡng")]
    public float acceptHeight = 0f;
    [Tooltip("Bán kính để raster vào grid (≈ bán kính dây)")]
    public float ropeRadius = 0.05f;

    [Header("Owner & Update")]
    public Transform owner;      // đại diện cho rope (để ignore self)
    public bool liveUpdate = true;

    // buffers
    private readonly List<Vector3> _polyWS = new();
    private readonly List<Vector3> _projWS = new();
    private readonly HashSet<Vector2Int> _current = new();
    private readonly HashSet<Vector2Int> _next = new();

    void Reset()
    {
        if (!rope) rope = GetComponent<ObiRope>();
        if (!grid) grid = FindObjectOfType<GridMap>(true);
        if (!owner) owner = transform;
    }

    void OnEnable()
    {
        if (!grid) grid = FindObjectOfType<GridMap>(true);
        if (!owner) owner = transform;
        // clear để tránh rác khi re-enable
        VacateAll();
    }

    void OnDisable() => VacateAll();

    void LateUpdate()
    {
        if (!liveUpdate) return;
        UpdateOccupancyNow();
    }

    public void UpdateOccupancyNow()
    {
        if (!rope || !grid || !owner) return;

        BuildPolylineFromRope(rope, _polyWS, grid.yLevel, acceptHeight);
        ProjectPolylineToPlane(_polyWS, _projWS, grid.yLevel);
        RasterizePolylineToCells(_projWS, _next, grid, ropeRadius);

        grid.UpdateOccupiedCellsDiff(_current, _next, owner);
    }

    public void VacateAll()
    {
        if (grid && owner)
            foreach (var c in _current) grid.Vacate(c, owner);
        _current.Clear();
        _next.Clear();
    }

    // ---------- Steps ----------
    static void BuildPolylineFromRope(ObiRope rope, List<Vector3> outPts, float yLevel, float acceptHeight)
    {
        outPts.Clear();
        int n = rope.particleCount;
        if (n <= 0) return;

        Vector3 prev = rope.GetParticlePosition(0);
        bool started = false;
        for (int i = 1; i < n; i++)
        {
            Vector3 cur = rope.GetParticlePosition(i);
            if (acceptHeight <= 0f ||
                Mathf.Min(Mathf.Abs(prev.y - yLevel), Mathf.Abs(cur.y - yLevel)) <= acceptHeight)
            {
                if (!started) { outPts.Add(prev); started = true; }
                if ((cur - outPts[outPts.Count - 1]).sqrMagnitude > 1e-12f)
                    outPts.Add(cur);
            }
            else started = false;

            prev = cur;
        }
    }

    static void ProjectPolylineToPlane(List<Vector3> src, List<Vector3> dst, float yLevel)
    {
        dst.Clear();
        for (int i = 0; i < src.Count; i++)
        {
            Vector3 p = src[i]; p.y = yLevel;
            if (dst.Count == 0 || (p - dst[dst.Count - 1]).sqrMagnitude > 1e-12f)
                dst.Add(p);
        }
    }

    static void RasterizePolylineToCells(List<Vector3> poly, HashSet<Vector2Int> cells, GridMap grid, float radius)
    {
        cells.Clear();
        if (poly.Count < 2) return;

        for (int i = 1; i < poly.Count; i++)
        {
            Vector2 a = new Vector2(poly[i - 1].x, poly[i - 1].z);
            Vector2 b = new Vector2(poly[i].x, poly[i].z);
            if (radius <= 0f) RasterizeSegmentDDA(a, b, cells, grid);
            else RasterizeCapsule(a, b, radius, cells, grid);
        }
    }

    static void RasterizeSegmentDDA(Vector2 a, Vector2 b, HashSet<Vector2Int> cells, GridMap grid)
    {
        float cs = grid.cellSize;
        Vector3 o = grid.origin;

        Vector2Int c0 = new Vector2Int(
            Mathf.FloorToInt((a.x - o.x) / cs),
            Mathf.FloorToInt((a.y - o.z) / cs));
        Vector2Int c1 = new Vector2Int(
            Mathf.FloorToInt((b.x - o.x) / cs),
            Mathf.FloorToInt((b.y - o.z) / cs));

        int x = c0.x, y = c0.y;
        int stepX = (b.x > a.x) ? 1 : -1;
        int stepY = (b.y > a.y) ? 1 : -1;

        float gx = o.x + (x + (stepX > 0 ? 1 : 0)) * cs;
        float gy = o.z + (y + (stepY > 0 ? 1 : 0)) * cs;

        Vector2 d = b - a; d += new Vector2(Mathf.Epsilon, Mathf.Epsilon);
        Vector2 invD = new Vector2(1f / Mathf.Abs(d.x), 1f / Mathf.Abs(d.y));

        float tMaxX = ((gx - a.x) * (stepX > 0 ? invD.x : -invD.x));
        float tMaxY = ((gy - a.y) * (stepY > 0 ? invD.y : -invD.y));
        float tDeltaX = cs * invD.x;
        float tDeltaY = cs * invD.y;

        cells.Add(new Vector2Int(x, y));
        int guard = 0;
        while (x != c1.x || y != c1.y)
        {
            if (++guard > 4096) break;
            if (tMaxX < tMaxY) { tMaxX += tDeltaX; x += stepX; }
            else { tMaxY += tDeltaY; y += stepY; }
            cells.Add(new Vector2Int(x, y));
        }
    }

    static void RasterizeCapsule(Vector2 a, Vector2 b, float r, HashSet<Vector2Int> cells, GridMap grid)
    {
        float cs = grid.cellSize;
        Vector3 o = grid.origin;

        float minx = Mathf.Min(a.x, b.x) - r;
        float maxx = Mathf.Max(a.x, b.x) + r;
        float miny = Mathf.Min(a.y, b.y) - r;
        float maxy = Mathf.Max(a.y, b.y) + r;

        int cx0 = Mathf.FloorToInt((minx - o.x) / cs);
        int cx1 = Mathf.FloorToInt((maxx - o.x) / cs);
        int cy0 = Mathf.FloorToInt((miny - o.z) / cs);
        int cy1 = Mathf.FloorToInt((maxy - o.z) / cs);

        for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++)
            {
                float x0 = o.x + cx * cs, x1 = x0 + cs;
                float z0 = o.z + cy * cs, z1 = z0 + cs;

                if (CapsuleIntersectsRect(a, b, r, x0, z0, x1, z1))
                    cells.Add(new Vector2Int(cx, cy));
            }
    }

    // 2D geometry
    static bool CapsuleIntersectsRect(Vector2 a, Vector2 b, float r, float rx0, float ry0, float rx1, float ry1)
    {
        if (SegmentIntersectsRect(a, b, rx0, ry0, rx1, ry1)) return true;
        float d = DistanceSegmentRect(a, b, rx0, ry0, rx1, ry1);
        return d <= r;
    }

    static bool SegmentIntersectsRect(Vector2 p1, Vector2 p2, float rx0, float ry0, float rx1, float ry1)
    {
        if (PointInRect(p1, rx0, ry0, rx1, ry1) || PointInRect(p2, rx0, ry0, rx1, ry1)) return true;

        Vector2 r00 = new Vector2(rx0, ry0), r10 = new Vector2(rx1, ry0),
                r11 = new Vector2(rx1, ry1), r01 = new Vector2(rx0, ry1);

        return SegSegIntersect(p1, p2, r00, r10) || SegSegIntersect(p1, p2, r10, r11) ||
               SegSegIntersect(p1, p2, r11, r01) || SegSegIntersect(p1, p2, r01, r00);
    }

    static float DistanceSegmentRect(Vector2 p1, Vector2 p2, float rx0, float ry0, float rx1, float ry1)
    {
        if (SegmentIntersectsRect(p1, p2, rx0, ry0, rx1, ry1)) return 0f;

        Vector2 r00 = new Vector2(rx0, ry0), r10 = new Vector2(rx1, ry0),
                r11 = new Vector2(rx1, ry1), r01 = new Vector2(rx0, ry1);

        float d = Mathf.Min(
            SegmentSegmentDistance(p1, p2, r00, r10),
            Mathf.Min(
                SegmentSegmentDistance(p1, p2, r10, r11),
                Mathf.Min(
                    SegmentSegmentDistance(p1, p2, r11, r01),
                    SegmentSegmentDistance(p1, p2, r01, r00)
                )
            )
        );
        return d;
    }

    static float SegmentSegmentDistance(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        if (SegSegIntersect(p1, p2, q1, q2)) return 0f;
        float d1 = PointSegmentDistance(p1, q1, q2);
        float d2 = PointSegmentDistance(p2, q1, q2);
        float d3 = PointSegmentDistance(q1, p1, p2);
        float d4 = PointSegmentDistance(q2, p1, p2);
        return Mathf.Min(Mathf.Min(d1, d2), Mathf.Min(d3, d4));
    }


    // Returns the shortest distance from point p to the segment [a, b]
    static float PointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        Vector2 ap = p - a;
        float abSqr = ab.sqrMagnitude;
        if (abSqr == 0f) return (p - a).magnitude;
        float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / abSqr);
        Vector2 proj = a + t * ab;
        return (p - proj).magnitude;
    }

    static bool SegSegIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        Vector2 r = p2 - p1, s = q2 - q1;
        float rxs = r.x * s.y - r.y * s.x;
        float qpxr = (q1.x - p1.x) * r.y - (q1.y - p1.y) * r.x;

        if (Mathf.Abs(rxs) < 1e-8f && Mathf.Abs(qpxr) < 1e-8f) return true;
        if (Mathf.Abs(rxs) < 1e-8f) return false;

        float t = ((q1.x - p1.x) * s.y - (q1.y - p1.y) * s.x) / rxs;
        float u = ((q1.x - p1.x) * r.y - (q1.y - p1.y) * r.x) / rxs;
        return (t >= 0f && t <= 1f && u >= 0f && u <= 1f);
    }

    static bool PointInRect(Vector2 p, float rx0, float ry0, float rx1, float ry1)
        => p.x >= rx0 && p.x <= rx1 && p.y >= ry0 && p.y <= ry1;
}
