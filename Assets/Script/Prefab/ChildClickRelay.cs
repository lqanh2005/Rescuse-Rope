using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ChildClickRelay : MonoBehaviour
{
    private DraggableMultiCell _parent;

    void Awake()
    {
        _parent = GetComponentInParent<DraggableMultiCell>();
    }

    void OnMouseDown()
    {
        if (_parent) _parent.BeginDrag(Camera.main);
    }

    void OnMouseDrag()
    {
        if (_parent) _parent.Drag(Camera.main);
    }

    void OnMouseUp()
    {
        if (_parent) _parent.EndDrag();
    }
}
