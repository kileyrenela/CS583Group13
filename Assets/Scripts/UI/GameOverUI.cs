using UnityEngine;

// =============================================================================
// GameOverUI.cs
// =============================================================================
// PURPOSE
//   Wires up the Restart and Quit buttons on the Game Over panel. Tiny by
//   design - all logic lives in GameManager.
//
// SETUP
//   1. The GameOver panel is a child of the Canvas, shown by UIManager when
//      GameManager fires OnGameOver
//   2. Add this component to the panel
//   3. Wire button OnClick: Restart -> OnRestartButton, Quit -> OnQuitButton
// =============================================================================

public class GameOverUI : MonoBehaviour
{
    public void OnRestartButton()
    {
        // Reloads the current scene. GameManager handles resetting timeScale.
        GameManager.Instance.RestartGame();
    }

    public void OnQuitButton()
    {
        // Fully quits the application (or no-op in Editor).
        // If we want "Quit to menu" instead, use QuitToMenu().
        GameManager.Instance.QuitGame();
    }
}
