using System.Collections.Generic;
using UnityEngine;

public enum Rot { R0, R90, R180, R270 }

[CreateAssetMenu(menuName = "Grid/Piece Footprint")]
public class PieceFootprintSO : ScriptableObject
{
    public List<Vector2Int> baseOffsets = new() { new(0, 0) }; 

    public IEnumerable<Vector2Int> GetOffsets(Rot rot)
    {
        foreach (var v in baseOffsets)
            yield return rot switch
            {
                Rot.R0 => v,
                Rot.R90 => new Vector2Int(v.y, -v.x),
                Rot.R180 => new Vector2Int(-v.x, -v.y),
                Rot.R270 => new Vector2Int(-v.y, v.x),
                _ => v
            };
    }
}
