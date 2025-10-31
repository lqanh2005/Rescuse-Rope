using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.UIElements;

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

    [Header("Style")]
    public Color lineColor = new Color(1, 1, 1, 0.15f);
    public float lineWidth = 0.02f;

    private LineRenderer lineRenderer;
    private List<Vector3> points = new List<Vector3>();
    [Header("Occupancy (chống trùng ô)")]
    public bool enforceUniqueAnchors = true;
    [Header("Runtime Render (Game view)")]
    public bool drawInGame = true;            // Bật để vẽ trong Game view
    public int majorEvery = 5;               // 0 = tắt vạch đậm
    public Color minorColor = new Color(1, 1, 1, 0.18f);
    public Color majorColor = new Color(1, 1, 1, 0.35f);
    public float zOffset = 0.001f;
    // Lưu chỗ đã có anchor
    private readonly Dictionary<Vector2Int, Transform> _occupancy = new();

    void OnEnable() => Instance = this;
    void OnDisable() { if (Instance == this) Instance = null; }

    void Start()
    {
    }

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
    static Material _mat;
    static Material RuntimeMat
    {
        get
        {
#if UNITY_URP || UNITY_RENDER_PIPELINE_UNIVERSAL
            if (_mat == null) _mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
#else
            if (_mat == null) _mat = new Material(Shader.Find("Hidden/Internal-Colored"));
#endif
            _mat.hideFlags = HideFlags.HideAndDontSave;
            _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _mat.SetInt("_ZWrite", 0); // không ghi depth -> trong suốt đẹp hơn
            return _mat;
        }
    }

    // Vẽ vào mọi camera khi render (cả Play lẫn khi đang ở Editor Game view)
    void OnRenderObject()
    {
        if (!drawInGame || cellSize <= 0f || width <= 0 || height <= 0) return;

        Vector3 start = new Vector3(origin.x, yLevel + zOffset, origin.z);
        float W = width * cellSize;
        float H = height * cellSize;

        RuntimeMat.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.LINES);

        // các đường dọc
        for (int x = 0; x <= width; x++)
        {
            bool isMajor = majorEvery > 0 && (x % majorEvery == 0);
            GL.Color(isMajor ? majorColor : minorColor);

            float xPos = start.x + x * cellSize;
            GL.Vertex3(xPos, start.y, start.z);
            GL.Vertex3(xPos, start.y, start.z + H);
        }

        // các đường ngang
        for (int z = 0; z <= height; z++)
        {
            bool isMajor = majorEvery > 0 && (z % majorEvery == 0);
            GL.Color(isMajor ? majorColor : minorColor);

            float zPos = start.z + z * cellSize;
            GL.Vertex3(start.x, start.y, zPos);
            GL.Vertex3(start.x + W, start.y, zPos);
        }

        GL.End();
        GL.PopMatrix();
    }
}
