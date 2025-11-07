using System.Collections.Generic;
using UnityEngine;
using Obi;

public class RopeCutBlockerManager : MonoBehaviour
{
    public List<ObiRope> ropes = new();
    public float ropeRadius = 0.05f;

    [SerializeField] private GridMap grid;

    // mỗi segment kèm rope owner
    private struct Seg
    {
        public Vector2 a, b;
        public ObiRope rope;
        public Seg(Vector2 a, Vector2 b, ObiRope r) { this.a = a; this.b = b; this.rope = r; }
    }

    private readonly List<Seg> _segments = new();

    // cell bị chiếm theo từng rope
    private readonly Dictionary<ObiRope, HashSet<Vector2Int>> _cellsByRope = new();

    void Awake()
    {
        if (!grid) grid = FindObjectOfType<GridMap>(true);
    }

    void LateUpdate()
    {
        Rebuild();
    }

    public void Rebuild()
    {
        _segments.Clear();
        _cellsByRope.Clear();
        if (!grid) return;

        var list = (ropes != null && ropes.Count > 0) ? ropes : new List<ObiRope>(FindObjectsOfType<ObiRope>(true));
        foreach (var r in list)
            if (r) AppendRope(r);

        RasterizeSegmentsToCells();
    }

    void AppendRope(ObiRope rope)
    {
        int count = rope.particleCount;
        if (count < 2) return;

        Vector3 prev = rope.GetParticlePosition(0);
        for (int i = 1; i < count; i++)
        {
            Vector3 cur = rope.GetParticlePosition(i);
            var a = new Vector2(prev.x, prev.z);
            var b = new Vector2(cur.x, cur.z);
            if ((b - a).sqrMagnitude > 1e-8f)
                _segments.Add(new Seg(a, b, rope));
            prev = cur;
        }
    }

    // ========== PUBLIC QUERIES ==========

    /// <summary>
    /// Kiểm tra đoạn từ P->Q có cắt bất kỳ rope nào TRỪ rope ignore không.
    /// </summary>
    public bool SegmentCutsAnyRope(Vector3 P, Vector3 Q, ObiRope ignoreRope = null)
    {
        Vector2 p = new Vector2(P.x, P.z);
        Vector2 q = new Vector2(Q.x, Q.z);

        foreach (var seg in _segments)
        {
            if (seg.rope == ignoreRope) continue; // <<< BỎ QUA CHÍNH ROPE ĐANG KÉO

            if (ropeRadius <= 0f)
            {
                if (SegSegIntersect(p, q, seg.a, seg.b))
                    return true;
            }
            else
            {
                if (SegmentCapsuleOverlap(p, q, seg.a, seg.b, ropeRadius))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// cell này có bị rope khác chiếm không?
    /// </summary>
    public bool IsCellBlocked(Vector2Int cell, ObiRope ignoreRope = null)
    {
        foreach (var kv in _cellsByRope)
        {
            if (kv.Key == ignoreRope) continue; // <<< BỎ QUA CHÍNH ROPE ĐANG KÉO
            if (kv.Value.Contains(cell)) return true;
        }
        return false;
    }

    // ========== RASTERIZE ==========

    void RasterizeSegmentsToCells()
    {
        float cs = grid.cellSize;
        var o = grid.origin;

        float r = ropeRadius;

        foreach (var seg in _segments)
        {
            if (!_cellsByRope.TryGetValue(seg.rope, out var cellSet))
                _cellsByRope[seg.rope] = cellSet = new HashSet<Vector2Int>();

            float minx = Mathf.Min(seg.a.x, seg.b.x) - r;
            float maxx = Mathf.Max(seg.a.x, seg.b.x) + r;
            float miny = Mathf.Min(seg.a.y, seg.b.y) - r;
            float maxy = Mathf.Max(seg.a.y, seg.b.y) + r;

            int cx0 = Mathf.FloorToInt((minx - o.x) / cs);
            int cx1 = Mathf.FloorToInt((maxx - o.x) / cs);
            int cy0 = Mathf.FloorToInt((miny - o.z) / cs);
            int cy1 = Mathf.FloorToInt((maxy - o.z) / cs);

            for (int cy = cy0; cy <= cy1; cy++)
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    float x0 = o.x + cx * cs;
                    float x1 = x0 + cs;
                    float z0 = o.z + cy * cs;
                    float z1 = z0 + cs;

                    if (CapsuleIntersectsRect(seg.a, seg.b, r, x0, z0, x1, z1))
                        cellSet.Add(new Vector2Int(cx, cy));
                }
        }
    }

    // (giữ nguyên các hàm geometry phía dưới...)
    static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
    static bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        return Mathf.Abs(Cross(b - a, p - a)) < 1e-6f &&
               Vector2.Dot(p - a, p - b) <= 0f;
    }
    static bool SegSegIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        Vector2 r = p2 - p1;
        Vector2 s = q2 - q1;
        float rxs = Cross(r, s);
        float qpxr = Cross(q1 - p1, r);

        if (Mathf.Abs(rxs) < 1e-8f && Mathf.Abs(qpxr) < 1e-8f)
            return OnSegment(p1, p2, q1) || OnSegment(p1, p2, q2) ||
                   OnSegment(q1, q2, p1) || OnSegment(q1, q2, p2);

        if (Mathf.Abs(rxs) < 1e-8f) return false;

        float t = Cross(q1 - p1, s) / rxs;
        float u = Cross(q1 - p1, r) / rxs;
        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    }

    static bool SegmentCapsuleOverlap(Vector2 p, Vector2 q, Vector2 a, Vector2 b, float r)
    {
        if (SegSegIntersect(p, q, a, b)) return true;
        return SegmentSegmentDistance(p, q, a, b) <= r;
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

    static float PointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / (ab.sqrMagnitude + 1e-12f);
        t = Mathf.Clamp01(t);
        return (p - (a + ab * t)).magnitude;
    }

    static bool CapsuleIntersectsRect(Vector2 a, Vector2 b, float r, float rx0, float ry0, float rx1, float ry1)
    {
        if (SegmentIntersectsRect(a, b, rx0, ry0, rx1, ry1)) return true;
        return DistanceSegmentRect(a, b, rx0, ry0, rx1, ry1) <= r;
    }

    static bool SegmentIntersectsRect(Vector2 p1, Vector2 p2, float rx0, float ry0, float rx1, float ry1)
    {
        if (PointInRect(p1, rx0, ry0, rx1, ry1) || PointInRect(p2, rx0, ry0, rx1, ry1)) return true;
        Vector2 r00 = new Vector2(rx0, ry0), r10 = new Vector2(rx1, ry0),
                r01 = new Vector2(rx0, ry1), r11 = new Vector2(rx1, ry1);
        return SegSegIntersect(p1, p2, r00, r10) || SegSegIntersect(p1, p2, r10, r11) ||
               SegSegIntersect(p1, p2, r11, r01) || SegSegIntersect(p1, p2, r01, r00);
    }

    static bool PointInRect(Vector2 p, float rx0, float ry0, float rx1, float ry1)
        => p.x >= rx0 && p.x <= rx1 && p.y >= ry0 && p.y <= ry1;

    static float DistanceSegmentRect(Vector2 p1, Vector2 p2, float rx0, float ry0, float rx1, float ry1)
    {
        if (SegmentIntersectsRect(p1, p2, rx0, ry0, rx1, ry1)) return 0f;
        Vector2 r00 = new Vector2(rx0, ry0), r10 = new Vector2(rx1, ry0),
                r01 = new Vector2(rx0, ry1), r11 = new Vector2(rx1, ry1);
        return Mathf.Min(
            SegmentSegmentDistance(p1, p2, r00, r10),
            Mathf.Min(
                SegmentSegmentDistance(p1, p2, r10, r11),
                Mathf.Min(
                    SegmentSegmentDistance(p1, p2, r11, r01),
                    SegmentSegmentDistance(p1, p2, r01, r00)
                )
            )
        );
    }
}
