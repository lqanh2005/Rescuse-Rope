using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Footprint
{
    public enum Kind { Single, Rect, Custom }
    public Kind kind = Kind.Single;

    [Header("Rect")]
    public int width = 1, height = 1;
    public Vector2Int pivot = Vector2Int.zero; // 0..w-1, 0..h-1

    [Header("Custom (VD: hình chữ L)")]
    public List<Vector2Int> customOffsets = new(); // các offset cell tương đối anchor

    [Header("Rotation")]
    [Range(0, 3)] public int rotationSteps = 0; // 0,1,2,3 => 0/90/180/270

    IEnumerable<Vector2Int> BaseOffsets()
    {
        if (kind == Kind.Single)
        {
            yield return Vector2Int.zero;
            yield break;
        }

        if (kind == Kind.Rect)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    yield return new Vector2Int(x - pivot.x, y - pivot.y);
            yield break;
        }

        // Custom
        foreach (var o in customOffsets)
            yield return o;
    }

    public IEnumerable<Vector2Int> RotatedOffsets()
    {
        foreach (var o in BaseOffsets())
            yield return Rotate90(o, rotationSteps);
    }

    static Vector2Int Rotate90(Vector2Int v, int steps)
    {
        steps = ((steps % 4) + 4) % 4;
        return steps switch
        {
            0 => v,
            1 => new Vector2Int(-v.y, v.x),
            2 => new Vector2Int(-v.x, -v.y),
            _ => new Vector2Int(v.y, -v.x),
        };
    }
}
