using NUnit.Framework;
using UnityEngine;
using Obi;
using System.Collections.Generic;

public class GamePlayController : MonoBehaviour
{
    public static GamePlayController instance;
    public RopeRopeCollisionDetector ropeRopeCollisionDetector;
    public List<ObiRope> sampleList = new List<ObiRope>(); 
    public List<ObiRope> collidingList = new List<ObiRope>();
    private readonly HashSet<ObiRope> collidingSet = new();

    private void Awake()
    {
        instance = this;
    }
    public void CheckWin()
    {
        collidingList = ropeRopeCollisionDetector.GetCollidingRopes();
        collidingSet.Clear();
        foreach (var rope in collidingList)
        {
            if (rope != null)
                collidingSet.Add(rope);
        }
        for (int i = sampleList.Count - 1; i >= 0; i--)
        {
            var rope = sampleList[i];
            if (rope == null)
            {
                sampleList.RemoveAt(i);
                continue;
            }
            if (!collidingSet.Contains(rope))
            {
                // Xóa hoặc hủy rope tùy nhu cầu
                Debug.Log($"❌ Xóa rope {rope.name} (không còn va chạm)");
                sampleList.RemoveAt(i);

                // Nếu bạn muốn destroy object luôn:
                Destroy(rope.gameObject);
            }
        }
    }
}
