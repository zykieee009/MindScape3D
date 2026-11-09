// A scene's pause menu. The on-screen pause button (MobileControls) finds whichever one the scene uses.
public interface IPausable
{
    bool IsPaused { get; }
    void TogglePause();
}
