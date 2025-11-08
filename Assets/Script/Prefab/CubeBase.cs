using UnityEngine;

[ExecuteAlways]
public class CubeBase : MonoBehaviour
{
    [Tooltip("Ô tương đối so với pivot của prefab cha (origin)")]
    public Vector2Int relativeCell;
    /// <summary>
    /// Gọi khi khởi tạo hoặc khi bạn thay đổi layout trong prefab.
    /// </summary>
    public void RecalculateRelativeCell()
    {
        if (!GridMap.Instance || !transform.parent)
            return;

        var g = GridMap.Instance;

        // Lấy cell của chính cube (world → grid)
        Vector2Int myCell = g.WorldToCell(transform.position);

        // Lấy cell của parent (prefab cha)
        Vector2Int parentCell = g.WorldToCell(transform.parent.position);

        // relative = cubeCell - parentCell
        relativeCell = myCell - parentCell;
    }

#if UNITY_EDITOR
    // Cập nhật realtime trong Editor khi bạn di chuyển các cube trong prefab
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (transform.parent != null)
            RecalculateRelativeCell();
    }
#endif
}
