using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class DragHandleToGrid : MonoBehaviour
{
    public Camera cam;
    public GridPlane grid;
    public bool snapWhileDragging = true;

    static DragHandleToGrid active;      // khoá kéo toàn cục
    Rigidbody rb;
    Collider col;
    bool dragging;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        col = GetComponent<Collider>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) TryBeginDrag();
        if (Input.GetMouseButtonUp(0)) EndDrag();

        if (dragging) DragUpdate();
    }

    void TryBeginDrag()
    {
        if (active != null) return;                        // đã có handle khác đang kéo
        if (cam == null || grid == null) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // 1) chỉ bắt đầu nếu bắn trúng collider CHÍNH MÌNH
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            if (hit.collider != col) return;               // không trúng mình → bỏ
        }
        else return;

        // 2) phải raycast được xuống đúng plane làm việc (không thì bỏ)
        if (!grid.RaycastToPlane(ray, out _)) return;

        dragging = true;
        active = this;

        // (tuỳ chọn) giải phóng ô cũ để người khác có thể chiếm
        grid.ReleaseOccupancyAt(transform.position, this);
    }

    void DragUpdate()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!grid.RaycastToPlane(ray, out Vector3 hit)) return;

        Vector3 target = snapWhileDragging ? grid.SnapWorldPoint(hit) : hit;

        // nếu snap khi kéo, đừng cho “đè” lên ô đã có người
        if (snapWhileDragging && grid.IsOccupied(target, out var owner) && owner != this)
        {
            target = grid.FindNearestFree(target);
        }

        rb.MovePosition(target);
    }

    void EndDrag()
    {
        if (!dragging) return;
        dragging = false;
        if (active == this) active = null;

        // Snap cuối + giữ chỗ
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (grid.RaycastToPlane(ray, out Vector3 hit))
        {
            Vector3 cell = grid.SnapWorldPoint(hit);
            if (grid.IsOccupied(cell, out var owner) && owner != this)
                cell = grid.FindNearestFree(cell);

            rb.MovePosition(cell);
            grid.ReserveOccupancyAt(cell, this);
        }
    }
}
