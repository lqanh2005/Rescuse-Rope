using UnityEngine;
using Obi;

public class RopeAutoLength : MonoBehaviour
{
    public ObiRope rope;
    public ObiRopeCursor cursor;
    public Transform anchorA, anchorB;

    public float slack = 0.08f;
    public float minLen = 0.15f;
    public float maxLen = 30f;
    public float changeSpeed = 3f;

    void Reset()
    {
        rope = GetComponent<ObiRope>();
        cursor = GetComponent<ObiRopeCursor>();
    }

    void Update()
    {
        if (!rope || !cursor || !anchorA || !anchorB) return;


        float dist = Vector3.Distance(anchorA.position, anchorB.position);
        float target = Mathf.Clamp(dist + slack, minLen, maxLen);
        float newLen = Mathf.MoveTowards(rope.restLength, target, changeSpeed * Time.deltaTime);

        if (Mathf.Abs(newLen - rope.restLength) > 0.0001f)
            cursor.ChangeLength(newLen);
    }
}
