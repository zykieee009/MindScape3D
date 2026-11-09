using UnityEngine;
using UnityEngine.InputSystem;

// Resolves the actions from the shared GameControls asset (KeyBindings.cs) once and exposes
// them to PlayerMovement and PlayerControls, so neither has to know action names or enable the map itself.
public class PlayerInputBindings : MonoBehaviour
{
    public InputAction Move { get; private set; }
    public InputAction Run { get; private set; }
    public InputAction FreeLook { get; private set; }
    public InputAction ZoomIn { get; private set; }
    public InputAction ZoomOut { get; private set; }
    public InputAction OpenTasks { get; private set; }
    public InputAction OpenQuest { get; private set; }
    public InputAction Look { get; private set; }       // pointer delta (mouse / trackpad)
    public InputAction LookKeys { get; private set; }   // arrow keys and gamepad right stick, -1..1

    void Awake()
    {
        var gameplay = KeyBindings.Gameplay;
        if (gameplay == null)
        {
            Debug.LogError("PlayerInputBindings could not find the Gameplay action map.", this);
            return;
        }

        gameplay.Enable();
        Move = gameplay.FindAction("Move");
        Run = gameplay.FindAction("Run");
        FreeLook = gameplay.FindAction("FreeLook");
        ZoomIn = gameplay.FindAction("ZoomIn");
        ZoomOut = gameplay.FindAction("ZoomOut");
        OpenTasks = gameplay.FindAction("OpenTasks");
        OpenQuest = gameplay.FindAction("OpenQuest");
        Look = gameplay.FindAction("Look");
        LookKeys = gameplay.FindAction("LookKeys");
    }
}
