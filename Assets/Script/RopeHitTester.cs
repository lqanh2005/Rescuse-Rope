using UnityEngine;
using Obi;

public class RopeHitTester : MonoBehaviour
{
    public ObiSolver solver;
    [Range(0.0f, 0.1f)]
    public float rayThickness = 0.04f;
    public bool synchronous = true;

    public bool DragSegmentHitsAnyRope(Vector3 from, Vector3 to)
    {
        if (solver == null) return false;
        Vector3 dir = to - from;
        float len = dir.magnitude;
        if (len < 1e-4f) return false;
        dir /= Mathf.Max(len, 1e-6f);

        bool prevSync = solver.synchronousSpatialQueries;
        if (synchronous) solver.synchronousSpatialQueries = true;

        int filter = ObiUtils.MakeFilter(ObiUtils.CollideWithEverything, 0);
        int qIndex = solver.EnqueueRaycast(new Ray(from, dir), filter, len, rayThickness);

        var results = solver.queryResults;
        bool hit = false;
        for (int i = 0; i < results.count; ++i)
        {
            var r = results[i];
            if (r.queryIndex == qIndex && r.distanceAlongRay <= len)
            { hit = true; break; }
        }

        if (synchronous) solver.synchronousSpatialQueries = prevSync;
        return hit;
    }
}
