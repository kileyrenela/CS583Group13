using UnityEngine;
using UnityEngine.SceneManagement;

// =============================================================================
// MainMenuUI.cs
// =============================================================================
// PURPOSE
//   Wires up the buttons on the Main Menu scene. Just two buttons: Play and Quit.
//
// SETUP IN MAINMENU SCENE
//   1. Create the MainMenu.unity scene (still TODO - currently only Game.unity exists)
//   2. Add a Canvas with title "Labyrinth", Play button, Quit button
//   3. Add this component to the Canvas (or any GameObject in the scene)
//   4. On each Button's OnClick, drag this GameObject and pick the matching method
//
// IMPORTANT
//   "Game" must be added to Build Settings (File > Build Settings) or
//   SceneManager.LoadScene("Game") will throw at runtime.
// =============================================================================

public class MainMenuUI : MonoBehaviour
{
    public void OnPlayButton()
    {
        // Load the gameplay scene by name.
        // NOTE: scene name must match the file in Assets/Scenes/Game.unity
        // AND it must be added to Build Settings.
        SceneManager.LoadScene("Game");
    }

    public void OnQuitButton()
    {
        // Application.Quit is a no-op in the Editor; works in built games.
        Application.Quit();
    }
}
