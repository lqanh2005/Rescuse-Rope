using System.Collections.Generic;
using UnityEngine;
using Obi;

public class RopeCleaner : MonoBehaviour
{
    public static RopeCleaner instance;
    [SerializeField] private ObiSolver solver;
    [SerializeField] private RopeRopeCollisionDetector detector;
    [SerializeField] private List<ObiRope> listMau = new();
    [SerializeField] private List<ObiRope> notColliding = new();

    private readonly HashSet<ObiRope> collidingSet = new();
    private bool armed = false;               // chỉ bắt đầu xoá sau khi đã thấy dữ liệu va chạm hợp lệ
    [SerializeField] private int graceFrames = 0; // tùy chọn: trễ thêm N frame trước khi xoá
    private void Awake()
    {
        instance = this;
    }

    public  void Check()
    {
        if (detector.LastUpdateFrame != Time.frameCount)
            return;

        // Lấy rope KHÔNG va chạm so với list mẫu:
        detector.GetNotCollidingFromSample(listMau, notColliding);

        // Xử lý: xoá khỏi list mẫu (và tuỳ chọn Destroy gameobject)
        for (int i = notColliding.Count - 1; i >= 0; --i)
        {
            var r = notColliding[i];
            if (r == null) continue;

            listMau.Remove(r);
            Debug.Log($"❌ Xoá rope {r.name} (không còn va chạm)");
            r.gameObject.SetActive(false);
        }
    }
}
