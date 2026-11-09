using UnityEngine;

// Bridges the on-screen touch controls (MobileControls) into the same movement/interact
// signals keyboard input already drives, so PlayerMovement and the various "press E/F to
// interact" scripts don't need separate mobile-aware branches — they just OR this in.
public static class MobileInput
{
    static Vector2 _moveAxis;
    static Vector2 _lookAxis;
    static bool _interactPressed;

    public static Vector2 MoveAxis => _moveAxis;

    // The right-hand stick, -1..1 per axis; PlayerMovement turns the camera at a steady rate while it is held.
    public static Vector2 LookAxis => _lookAxis;

    public static bool InteractPressed => _interactPressed;

    // Called continuously by MobileJoystick while dragging, and with Vector2.zero on release.
    public static void SetMoveAxis(Vector2 axis)
    {
        _moveAxis = axis.sqrMagnitude > 1f ? axis.normalized : axis;
    }

    public static void SetLookAxis(Vector2 axis)
    {
        _lookAxis = axis.sqrMagnitude > 1f ? axis.normalized : axis;
    }

    public static void TriggerInteract()
    {
        _interactPressed = true;
    }

    // The View button: a latch that PlayerMovement consumes, so the press survives whichever LateUpdate runs first.
    static bool _viewPressed;
    public static void TriggerViewToggle() => _viewPressed = true;
    public static bool ConsumeViewToggle() { bool pressed = _viewPressed; _viewPressed = false; return pressed; }

    // Called once per frame (from MobileControls.LateUpdate) after every script has had
    // a chance to read InteractPressed, mirroring InputAction.WasPressedThisFrame semantics.
    public static void ClearFrameState()
    {
        _interactPressed = false;
    }
}
