using UnityEngine;

// =============================================================================
// VictoryUI.cs
// =============================================================================
// PURPOSE
//   Wires up the Restart and Quit buttons on the Victory panel. Same pattern
//   as GameOverUI. The "Congratulations!" + "More levels coming soon!" text
//   is just a Text element on the panel - no script needed for it.
//
// SETUP
//   1. The Victory panel is a child of the Canvas, shown by UIManager when
//      GameManager fires OnVictory
//   2. Add this component to the panel
//   3. Wire button OnClick: Restart -> OnRestartButton, Quit -> OnQuitButton
// =============================================================================

public class VictoryUI : MonoBehaviour
{
    public void OnRestartButton()
    {
        GameManager.Instance.RestartGame();
    }

    public void OnQuitButton()
    {
        GameManager.Instance.QuitGame();
    }
}
