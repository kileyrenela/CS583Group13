using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================================
// PauseMenuUI.cs
// =============================================================================
// PURPOSE
//   Backs the in-game Pause Menu panel. Two responsibilities:
//     1. Wire up the Resume/Quit buttons (called from button OnClick events)
//     2. Catch the ESC keybinding to toggle pause state
//
// SETUP
//   1. The Pause panel is a child of the main Canvas in Game.unity
//   2. Add this component to the panel (or any persistent GameObject in the scene)
//   3. Wire Button OnClick events: Resume -> OnResumeButton, Quit -> OnQuitButton
//   4. The Player's PlayerInput component must have a "Pause" action mapped to ESC
//      and route it to OnPause here (via Send Messages or Invoke Unity Events)
//
// NOTE: ESC binding placement
//   The OnPause callback is wired via PlayerInput. That means it only fires
//   while the player's input asset is enabled. Currently the action map is
//   active in all GameStates, so ESC works in Paused state too (toggling back
//   to Playing). If we ever split into multiple action maps (UI vs gameplay),
//   make sure the Pause action is on a map that's always active.
// =============================================================================

public class PauseMenuUI : MonoBehaviour
{
    public void OnResumeButton()
    {
        GameManager.Instance.TogglePause();
    }

    public void OnQuitButton()
    {
        GameManager.Instance.QuitToMenu();
    }

    // Called by the Input System when ESC is pressed.
    public void OnPause(InputAction.CallbackContext context)
    {
        // Only fire on the moment of press (performed), not on hold or release.
        if (!context.performed) return;
        GameManager.Instance.TogglePause();
    }
}
