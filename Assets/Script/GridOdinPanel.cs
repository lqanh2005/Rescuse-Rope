// GridOdinPanel.cs
using UnityEngine;
using Sirenix.OdinInspector;

public class GridOdinPanel : MonoBehaviour
{
    [Title("Level Tools (Editor)")]
    [Button("Bake Level (Unique + Bottom Snap)", ButtonSizes.Large)]
    public void BakeLevelUniqueBottom()
    {
#if UNITY_EDITOR
        var grid = GetComponent<GridMap>();
        if (grid == null)
        {
            Debug.LogError("[GridOdinPanel] Không tìm thấy GridPlane trên GameObject này.");
            return;
        }
        grid.BakeLevelToGrid(); // gọi hàm đã viết trong GridPlane (Editor-only)
#else
        Debug.LogWarning("Chỉ chạy trong Editor.");
#endif
    }

    [PropertySpace(8)]
    [Button("Snap All (Keep Current Cells)", ButtonSizes.Medium)]
    public void SnapAllKeepCells()
    {
#if UNITY_EDITOR
        var grid = GetComponent<GridMap>();
        if (grid == null) { Debug.LogError("[GridOdinPanel] Thiếu GridPlane."); return; }

        // Snap tất cả occupants xuống đáy, KHÔNG đổi cell hiện tại
        var occs = FindObjectsOfType<GridOccupant>(true);
        foreach (var o in occs)
        {
            var cell = grid.WorldToCell(o.transform.position);
            var pos = grid.CellToWorldBottomAligned(cell, o.transform, o.bottomExtraOffset);
            UnityEditor.Undo.RecordObject(o.transform, "Snap All Keep Cells");
            o.transform.position = pos;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[GridOdinPanel] Snapped {occs.Length} occupants (giữ nguyên cell).");
#else
        Debug.LogWarning("Chỉ chạy trong Editor.");
#endif
    }
}
