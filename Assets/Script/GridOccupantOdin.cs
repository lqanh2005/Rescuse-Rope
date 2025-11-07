using UnityEngine;
using Sirenix.OdinInspector;

[ExecuteAlways]
public class GridOccupantOdin : MonoBehaviour
{
//    [InfoBox("Chọn Grid nếu để ngoài hierarchy khác/scene khác.")]
//    [Required, SerializeField] private GridMap grid;  // cho phép bạn gán tay

//    [PropertySpace, ShowInInspector, ReadOnly]
//    public Vector2Int CurrentCell
//    {
//        get { var g = ResolveGrid(); return g ? g.WorldToCell(transform.position) : default; }
//    }

//    [Button("Snap Nearest Free (Bottom)", ButtonSizes.Large)]
//    public void SnapNearestFreeBottom()
//    {
//        var g = ResolveGrid();
//        if (!g) { Debug.LogError("Không tìm thấy GridPlane. Gán vào field 'grid' hoặc để một GridPlane trong scene."); return; }

//#if UNITY_EDITOR
//        var all = FindObjectsOfType<GridOccupantOdin>(true);
//        var taken = new System.Collections.Generic.HashSet<Vector2Int>();
//        foreach (var o in all) if (o != this) taken.Add(g.WorldToCell(o.transform.position));

//        var prefer = g.WorldToCell(transform.position);
//        var chosen = g.FindNearestFreeCellAvoid(prefer, taken);
//        var pos = g.CellToWorldBottomAligned(chosen, transform);

//        UnityEditor.Undo.RecordObject(transform, "Snap Nearest Free (Bottom)");
//        transform.position = pos;
//        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
//#endif
//    }

//    // ---- Helpers ----
//    private GridMap ResolveGrid()
//    {
//        if (grid) return grid;
//        grid = GridMap.GetOrFind(); // fallback: tự tìm trong scene
//        return grid;
//    }

//    [InlineButton(nameof(AutoAssignGrid), "Pick Scene Grid")]
//    [ShowInInspector, ReadOnly] private string _hint => grid ? $"Grid: {grid.name}" : "Chưa chọn Grid";

//#if UNITY_EDITOR
//    private void AutoAssignGrid()
//    {
//        grid = GridMap.GetOrFind();
//        if (!grid) Debug.LogWarning("Không thấy GridPlane nào trong các scene đang mở.");
//        UnityEditor.EditorUtility.SetDirty(this);
//    }
//#endif
}
