using Obi;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteAlways]
public class RopeSimplexVector : MonoBehaviour
{
    [Header("References")]
    public ObiRope rope;                 // Obi Rope (Obi 7)
    public LineRenderer line;            // LineRenderer để vẽ vector cong
    public Transform arrow;              // (tuỳ chọn) mũi tên ở đầu

    [Header("Length Control")]
    [Range(0f, 1f)] public float progress = 1f; // 0..1: tỷ lệ chiều dài vector hiển thị dọc rope
    public float extraHead = 0f;                // kéo nhô đầu mũi tên thêm (m), tuỳ ý

    [Header("Smoothing (optional)")]
    public bool smooth = false;                 // bật nếu muốn mượt hơn
    [Range(0, 3)] public int catmullRomSubdiv = 1; // 0 = không nội suy; 1-3 = mịn dần

    // Internal buffers (để hạn chế GC)
    private readonly List<Vector3> pts = new();       // polyline lấy từ rope (world space)
    private readonly List<Vector3> smoothed = new();  // polyline sau khi smoothing (nếu bật)
    private readonly List<float> S = new();           // độ dài tích luỹ
    private readonly List<Vector3> cut = new();       // polyline sau khi cắt theo L

    void LateUpdate()
    {
        if (rope == null || line == null)
        {
            if (line) line.positionCount = 0;
            return;
        }

        // 1) Lấy đường rope bằng API actor (KHÔNG đụng solver trực tiếp)
        SampleRopeWorldPointsSafe(rope, pts);
        if (pts.Count < 2)
        {
            line.positionCount = 0;
            return;
        }

        // 2) Smoothing (tuỳ chọn)
        var poly = pts;
        if (smooth)
        {
            CatmullRom(poly, catmullRomSubdiv, smoothed);
            poly = smoothed;
        }

        // 3) Cắt theo progress
        BuildCumulative(poly, S);
        float total = S[S.Count - 1];
        float L = Mathf.Clamp01(progress) * total + Mathf.Max(0f, extraHead);

        TrimPolyline(poly, S, L, cut);

        // 4) Vẽ
        line.positionCount = cut.Count;
        if (cut.Count > 0) line.SetPositions(cut.ToArray());

        // 5) Mũi tên ở đầu
        if (arrow != null && cut.Count >= 2)
        {
            var tail = cut[cut.Count - 2];
            var head = cut[cut.Count - 1];
            var dir = head - tail;
            if (dir.sqrMagnitude > 1e-8f)
            {
                arrow.position = head;
                arrow.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }
    }

    // ===== Helpers =====

    // An toàn cho Obi 7: duyệt actor-particles, lấy world pos qua API
    static void SampleRopeWorldPointsSafe(ObiRope rope, List<Vector3> outPts)
    {
        outPts.Clear();

        int pcount = rope.particleCount;
        if (pcount <= 0) return;

        // Nếu muốn bỏ qua hạt không hoạt động, bạn có thể dùng IsParticleActive(i) (nếu API có)
        for (int i = 0; i < pcount-2; i++)
        {
            // Trả về world-space luôn, không cần solver.TransformPoint
            Vector3 wp = rope.GetParticlePosition(i);
            outPts.Add(wp);
        }
    }

    static void BuildCumulative(List<Vector3> pts, List<float> s)
    {
        s.Clear();
        s.Add(0f);
        float acc = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            acc += Vector3.Distance(pts[i - 1], pts[i]);
            s.Add(acc);
        }
    }

    static void TrimPolyline(List<Vector3> pts, List<float> s, float L, List<Vector3> outPts)
    {
        outPts.Clear();
        if (pts.Count == 0) return;

        float total = s[s.Count - 1];
        float target = Mathf.Clamp(L, 0f, total);

        outPts.Add(pts[0]);
        for (int i = 1; i < pts.Count; i++)
        {
            if (s[i] < target)
            {
                outPts.Add(pts[i]);
            }
            else
            {
                float seg = s[i] - s[i - 1];
                float t = seg > 1e-6f ? (target - s[i - 1]) / seg : 0f;
                Vector3 p = Vector3.Lerp(pts[i - 1], pts[i], t);
                outPts.Add(p);
                break;
            }
        }
    }

    static void CatmullRom(List<Vector3> src, int subdiv, List<Vector3> dst)
    {
        dst.Clear();
        if (subdiv <= 0 || src.Count < 4)
        {
            dst.AddRange(src);
            return;
        }

        for (int i = 0; i < src.Count - 3; i++)
        {
            Vector3 p0 = src[i];
            Vector3 p1 = src[i + 1];
            Vector3 p2 = src[i + 2];
            Vector3 p3 = src[i + 3];

            dst.Add(p1);
            for (int j = 1; j <= subdiv; j++)
            {
                float t = j / (float)(subdiv + 1);
                dst.Add(CatmullEval(p0, p1, p2, p3, t));
            }
        }

        dst.Add(src[src.Count - 2]);
        dst.Add(src[src.Count - 1]);
    }

    static Vector3 CatmullEval(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) +
                       (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }
}
