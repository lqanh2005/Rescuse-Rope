using Obi;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(1000)] // chạy sau Obi
[ExecuteAlways]
public class RopeToGridOccupancy : MonoBehaviour
{
    [Header("Refs")]
    public ObiRope rope;
    public GridMap grid;
    [Tooltip("Transform đại diện để chiếm ô (nên là rope.transform)")]
    public Transform owner;
    public Transform anchorA, anchorB;

    [Header("Projection & Raster")]
    [Tooltip("0 = lấy toàn bộ; >0 chỉ nhận đoạn có ít nhất 1 đầu cách yLevel <= ngưỡng")]
    public float acceptHeight = 0f;
    [Tooltip("Bán kính khi raster vào grid (≈ bán kính dây)")]
    public float ropeRadius = 0.05f;

    [Header("Update")]
    public bool liveUpdate = true;

    // buffers
    private readonly List<Vector3> _polyWS = new();
    private readonly List<Vector3> _projWS = new();
    private readonly HashSet<Vector2Int> _current = new();
    private readonly HashSet<Vector2Int> _next = new();

    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    public IReadOnlyCollection<Vector2Int> OccupiedCells => occupiedCells;

    private void OnDisable()
    {
        if(GridMap.Instance != null) GridMap.Instance.RemoveOwner(transform);
    }
    private void LateUpdate()
    {
        StartCoroutine(UpdateRopeCellsRoutine());

    }
    IEnumerator UpdateRopeCellsRoutine()
    {
        while (true)
        {
            // 1) Chờ Physics + Obi Solver cập nhật xong frame này
            yield return new WaitForFixedUpdate();

            // 2) Bây giờ vị trí hạt đã đúng → tính cell
            MarkRopeCells();
            if (liveUpdate && GridMap.Instance != null)
            {
                GridMap.Instance.ApplyOwner(transform, occupiedCells);
            }
            // 3) (Tuỳ chọn) nếu bạn muốn update khớp hình luôn thì thêm
            yield return null; // chờ sang frame render
        }
    }
    void MarkRopeCells()
    {
        occupiedCells.Clear();
        if (!rope || !grid || rope.blueprint == null) return;

        var solver = rope.solver;
        var ids = rope.solverIndices;
        if (solver == null || ids == null || rope.activeParticleCount < 2 || solver.positions.count == 0) return;

        // A) nối từ anchor A → hạt đầu
        if (anchorA)
        {
            int first = ids[0];
            if (first >= 0 && first < solver.positions.count)
            {
                Vector3 pFirst = solver.transform.TransformPoint(solver.positions[first]);
                RasterizeSegment(anchorA.position, pFirst, occupiedCells);
            }
        }

        // B) các đoạn giữa hạt (solver → world ở cả hai đầu)
        for (int i = 0; i < rope.activeParticleCount - 1; i++)
        {
            int a = ids[i], b = ids[i + 1];
            if (a < 0 || b < 0 || a >= solver.positions.count || b >= solver.positions.count) continue;
            Vector3 p1 = solver.transform.TransformPoint(solver.positions[a]);
            Vector3 p2 = solver.transform.TransformPoint(solver.positions[b]);
            RasterizeSegment(p1, p2, occupiedCells);
        }

        // C) nối từ hạt cuối → anchor B
        if (anchorB)
        {
            int last = ids[rope.activeParticleCount - 1];
            if (last >= 0 && last < solver.positions.count)
            {
                Vector3 pLast = solver.transform.TransformPoint(solver.positions[last]);
                RasterizeSegment(pLast, anchorB.position, occupiedCells);
            }
        }
    }
    const float kMinSegLen2 = 1e-6f;
    static void RasterizeSegment(Vector3 w1, Vector3 w2, HashSet<Vector2Int> dst)
    {
        var gp = GridMap.Instance;
        if ((w2 - w1).sqrMagnitude < kMinSegLen2) return;
        var c1 = gp.WorldToCell(w1);
        var c2 = gp.WorldToCell(w2);

        int dx = Mathf.Abs(c2.x - c1.x);
        int dy = Mathf.Abs(c2.y - c1.y);
        int sx = c1.x < c2.x ? 1 : -1;
        int sy = c1.y < c2.y ? 1 : -1;
        int err = dx - dy;

        int x = c1.x, y = c1.y;
        while (true)
        {
            dst.Add(new Vector2Int(x, y));
            if (x == c2.x && y == c2.y) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }
    void OnDrawGizmos()
    {
        if (occupiedCells == null) return;

        Gizmos.color = Color.red;
        foreach (var cell in occupiedCells)
        {
            Vector3 center = grid.CellToWorldCenter(cell);
            Gizmos.DrawCube(center, Vector3.one * (grid.cellSize * 0.9f));
        }
    }


    public bool IsCellOccupied(Vector2Int cell)
    {
        return occupiedCells.Contains(cell);
    }

    //void Reset()
    //{
    //    if (!rope) rope = GetComponent<ObiRope>();
    //    if (!grid) grid = FindObjectOfType<GridMap>(true);
    //    if (!owner) owner = transform;
    //}

    ////void OnEnable()
    ////{
    ////    if (!grid) grid = FindObjectOfType<GridMap>(true);
    ////    if (!owner) owner = transform;
    ////    StartCoroutine(DelayFirstBuild()); // đợi solver update 1 frame
    ////}
    ////System.Collections.IEnumerator DelayFirstBuild()
    ////{
    ////    yield return new WaitForEndOfFrame();
    ////    UpdateOccupancyNow();
    ////}

    //void OnDisable()
    //{
    //    if (grid && owner) foreach (var c in _current) grid.Vacate(c, owner);
    //    _current.Clear(); _next.Clear();
    //}

    ////void LateUpdate()
    ////{
    ////    if (liveUpdate) UpdateOccupancyNow();
    ////}

    //private void UpdateOccupancyNow()
    //{
    //    if (!rope || !grid || !owner) return;

    //    BuildPolyline(rope, _polyWS, grid.yLevel, acceptHeight);
    //    ProjectToPlane(_polyWS, _projWS, grid.yLevel);
    //    RasterizePolyline(_projWS, _next, grid, ropeRadius);

    //    grid.UpdateOccupiedCellsDiff(_current, _next, owner);
    //}

    //// -- Steps --
    //private void BuildPolyline(ObiRope rope, List<Vector3> outPts, float yLevel, float acceptH)
    //{
    //    outPts.Clear();
    //    int n = rope.particleCount; if (n <= 0) return;
    //    Vector3 prev = rope.GetParticlePosition(0);
    //    bool started = false;
    //    for (int i = 1; i < n; i++)
    //    {
    //        Vector3 cur = rope.GetParticlePosition(i);
    //        if (acceptH <= 0f || Mathf.Min(Mathf.Abs(prev.y - yLevel), Mathf.Abs(cur.y - yLevel)) <= acceptH)
    //        {
    //            if (!started) { outPts.Add(prev); started = true; }
    //            if ((cur - outPts[outPts.Count - 1]).sqrMagnitude > 1e-12f) outPts.Add(cur);
    //        }
    //        else started = false;
    //        prev = cur;
    //    }
    //}
    //static void ProjectToPlane(List<Vector3> src, List<Vector3> dst, float yLevel)
    //{
    //    dst.Clear();
    //    foreach (var p0 in src)
    //    {
    //        var p = p0; p.y = yLevel;
    //        if (dst.Count == 0 || (p - dst[dst.Count - 1]).sqrMagnitude > 1e-12f) dst.Add(p);
    //    }
    //}
    //static void RasterizePolyline(List<Vector3> poly, HashSet<Vector2Int> cells, GridMap grid, float radius)
    //{
    //    cells.Clear();
    //    if (poly.Count < 2) return;
    //    for (int i = 1; i < poly.Count; i++)
    //        RasterizeCapsule(new Vector2(poly[i - 1].x, poly[i - 1].z),
    //                         new Vector2(poly[i].x, poly[i].z),
    //                         radius, cells, grid);
    //}
    //static void RasterizeCapsule(Vector2 a, Vector2 b, float r, HashSet<Vector2Int> cells, GridMap grid)
    //{
    //    float cs = grid.cellSize; var o = grid.origin;
    //    float minx = Mathf.Min(a.x, b.x) - r, maxx = Mathf.Max(a.x, b.x) + r;
    //    float miny = Mathf.Min(a.y, b.y) - r, maxy = Mathf.Max(a.y, b.y) + r;
    //    int cx0 = Mathf.FloorToInt((minx - o.x) / cs), cx1 = Mathf.FloorToInt((maxx - o.x) / cs);
    //    int cy0 = Mathf.FloorToInt((miny - o.z) / cs), cy1 = Mathf.FloorToInt((maxy - o.z) / cs);
    //    for (int cy = cy0; cy <= cy1; cy++)
    //        for (int cx = cx0; cx <= cx1; cx++)
    //        {
    //            float x0 = o.x + cx * cs, x1 = x0 + cs, z0 = o.z + cy * cs, z1 = z0 + cs;
    //            if (CapsuleIntersectsRect(a, b, r, x0, z0, x1, z1)) cells.Add(new Vector2Int(cx, cy));
    //        }
    //}
    //static bool CapsuleIntersectsRect(Vector2 a, Vector2 b, float r, float rx0, float ry0, float rx1, float ry1)
    //{
    //    if (SegRect(a, b, rx0, ry0, rx1, ry1)) return true;
    //    return DistSegRect(a, b, rx0, ry0, rx1, ry1) <= r;
    //}
    //static bool SegRect(Vector2 p1, Vector2 p2, float rx0, float ry0, float rx1, float ry1)
    //{
    //    if (InRect(p1, rx0, ry0, rx1, ry1) || InRect(p2, rx0, ry0, rx1, ry1)) return true;
    //    Vector2 r00 = new(rx0, ry0), r10 = new(rx1, ry0), r11 = new(rx1, ry1), r01 = new(rx0, ry1);
    //    return SegSeg(p1, p2, r00, r10) || SegSeg(p1, p2, r10, r11) || SegSeg(p1, p2, r11, r01) || SegSeg(p1, p2, r01, r00);
    //}
    //static bool InRect(Vector2 p, float rx0, float ry0, float rx1, float ry1)
    //    => p.x >= rx0 && p.x <= rx1 && p.y >= ry0 && p.y <= ry1;
    //static bool SegSeg(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    //{
    //    Vector2 r = p2 - p1, s = q2 - q1; float rxs = r.x * s.y - r.y * s.x, qpxr = (q1.x - p1.x) * r.y - (q1.y - p1.y) * r.x;
    //    if (Mathf.Abs(rxs) < 1e-8f && Mathf.Abs(qpxr) < 1e-8f) return true;
    //    if (Mathf.Abs(rxs) < 1e-8f) return false;
    //    float t = ((q1.x - p1.x) * s.y - (q1.y - p1.y) * s.x) / rxs, u = ((q1.x - p1.x) * r.y - (q1.y - p1.y) * r.x) / rxs;
    //    return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    //}
    //static float DistSegRect(Vector2 p1, Vector2 p2, float rx0, float ry0, float rx1, float ry1)
    //{
    //    if (SegRect(p1, p2, rx0, ry0, rx1, ry1)) return 0f;
    //    Vector2 r00 = new(rx0, ry0), r10 = new(rx1, ry0), r11 = new(rx1, ry1), r01 = new(rx0, ry1);
    //    float d(float ax, float ay, float bx, float by) => DistSegSeg(p1, p2, new Vector2(ax, ay), new Vector2(bx, by));
    //    return Mathf.Min(d(rx0, ry0, rx1, ry0), Mathf.Min(d(rx1, ry0, rx1, ry1), Mathf.Min(d(rx1, ry1, rx0, ry1), d(rx0, ry1, rx0, ry0))));
    //}
    //static float DistSegSeg(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    //{
    //    if (SegSeg(p1, p2, q1, q2)) return 0f;
    //    return Mathf.Min(DistPointSeg(p1, q1, q2), Mathf.Min(DistPointSeg(p2, q1, q2),
    //           Mathf.Min(DistPointSeg(q1, p1, p2), DistPointSeg(q2, p1, p2))));
    //}
    //static float DistPointSeg(Vector2 p, Vector2 a, Vector2 b)
    //{
    //    Vector2 ab = b - a; float t = Vector2.Dot(p - a, ab) / (ab.sqrMagnitude + 1e-12f); t = Mathf.Clamp01(t);
    //    return (p - (a + ab * t)).magnitude;
    //}
}
