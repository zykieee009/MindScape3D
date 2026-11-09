using UnityEngine;
using UnityEngine.EventSystems;

// On-screen analog joystick for movement (left) or looking around (right, with controlsLook ticked). This component
// lives directly on the background circle's Image so touch/drag raycasts land on it;
// the handle is a purely visual child (raycastTarget off) that gets dragged within
// handleRange to show how far/which way the stick is pushed.
public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] RectTransform handle;
    [SerializeField] float handleRange = 70f;
    [SerializeField] bool controlsLook;

    RectTransform _rect;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    // Used when MobileControls builds the overlay at runtime.
    public void SetHandle(RectTransform target, float range, bool look = false)
    {
        handle = target;
        handleRange = range;
        controlsLook = look;
    }

    public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

    public void OnDrag(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            return;

        Vector2 offset = Vector2.ClampMagnitude(localPoint, handleRange);
        if (handle != null) handle.anchoredPosition = offset;
        SetAxis(offset / handleRange);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    void OnDisable()
    {
        Release();
    }

    void Release()
    {
        if (handle != null) handle.anchoredPosition = Vector2.zero;
        SetAxis(Vector2.zero);
    }

    void SetAxis(Vector2 axis)
    {
        if (controlsLook) MobileInput.SetLookAxis(axis);
        else MobileInput.SetMoveAxis(axis);
    }
}
