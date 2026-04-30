using UnityEngine;

// =============================================================================
// UIManager.cs
// =============================================================================
// PURPOSE
//   Centralized UI controller for the in-game HUD and overlay screens. It
//   subscribes to GameManager and PlayerVisibility events and toggles UI panels
//   on/off in response. Keeps individual UI panel scripts (PauseMenuUI, etc.)
//   thin - they just handle their own button callbacks.
//
// ARCHITECTURE
//   This is the bridge between game-state events and Unity UI. Pattern:
//     GameManager fires event -> UIManager toggles panel
//     PlayerVisibility fires event -> UIManager toggles "hidden" indicator
//     PlayerInteraction has a target -> UIManager shows interaction prompt
//
// SETUP IN SCENE (Game.unity)
//   1. Create a Canvas (UI > Canvas)
//   2. Under it, create child panels:
//        - PauseMenuPanel (with Resume/Quit buttons -> PauseMenuUI script)
//        - GameOverPanel (with Restart/Quit -> GameOverUI script)
//        - VictoryPanel (with Restart/Quit -> VictoryUI script)
//        - InteractionPrompt (Text: "Press E to ...")
//        - JumpscareOverlay (full-screen Image with enemy face)
//        - HiddenIndicator (vignette / icon shown while hidden in dark)
//   3. Add a UIManager component to the Canvas (or to a child empty)
//   4. Drag each panel into the matching SerializedField in the Inspector
//   5. Make sure all panels start enabled in the editor (HideAll disables them
//      at runtime so they're easy to author when active)
//
// EVENT LIFETIME
//   Subscribe in Start, unsubscribe in OnDestroy. Critical for scene reload -
//   without unsubscribe, dead UIManager refs leak into next scene's events
//   and explode with NullReferenceException.
// =============================================================================

public class UIManager : MonoBehaviour
{
    // Drag UI panels here in the Inspector. All optional - missing panels are
    // simply skipped (no NullReferenceException).
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject interactionPrompt;  // "Press E to hide" text
    [SerializeField] private GameObject jumpscareOverlay;   // Full-screen enemy face
    [SerializeField] private GameObject hiddenIndicator;    // Vignette/icon when hidden in dark

    private PlayerInteraction playerInteraction;
    private PlayerVisibility playerVisibility;

    private void Start()
    {
        // ---- Subscribe to GameManager events ------------------------------
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnPause          += ShowPauseMenu;
            gm.OnResume         += HidePauseMenu;
            gm.OnGameOver       += ShowGameOver;
            gm.OnVictory        += ShowVictory;
            gm.OnJumpscareStart += ShowJumpscare;
        }

        // ---- Find player components for HUD updates -----------------------
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerInteraction = playerObj.GetComponent<PlayerInteraction>();
            playerVisibility = playerObj.GetComponent<PlayerVisibility>();
            if (playerVisibility != null)
                playerVisibility.OnVisibilityChanged += OnVisibilityChanged;
        }

        // Start with all panels hidden. Designers can leave panels enabled in
        // the editor (so they're easy to author) and we hide them here at runtime.
        HideAll();
    }

    // CRITICAL: unsubscribe to prevent dangling refs after scene reload.
    private void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnPause          -= ShowPauseMenu;
            gm.OnResume         -= HidePauseMenu;
            gm.OnGameOver       -= ShowGameOver;
            gm.OnVictory        -= ShowVictory;
            gm.OnJumpscareStart -= ShowJumpscare;
        }

        if (playerVisibility != null)
            playerVisibility.OnVisibilityChanged -= OnVisibilityChanged;
    }

    private void Update()
    {
        // Interaction prompt is polled (not event-driven) because the target
        // changes every frame as the player looks around. Cheap toggle.
        UpdateInteractionPrompt();
    }

    // Show the interaction prompt only when:
    //   - We're actively playing (not paused/dead/won)
    //   - The player is currently looking at an interactable
    private void UpdateInteractionPrompt()
    {
        if (interactionPrompt == null || playerInteraction == null) return;

        bool showPrompt = GameManager.Instance.CurrentState == GameState.Playing
                          && playerInteraction.CurrentTarget != null;
        interactionPrompt.SetActive(showPrompt);
    }

    // Show the "you are hidden" indicator only in HiddenInDark state.
    // Chest hiding has its own visual (the chest interior overlay).
    private void OnVisibilityChanged(VisibilityState state)
    {
        if (hiddenIndicator != null)
            hiddenIndicator.SetActive(state == VisibilityState.HiddenInDark);
    }

    // Disable all overlays at game start.
    private void HideAll()
    {
        if (pauseMenuPanel != null)    pauseMenuPanel.SetActive(false);
        if (gameOverPanel != null)     gameOverPanel.SetActive(false);
        if (victoryPanel != null)      victoryPanel.SetActive(false);
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
        if (jumpscareOverlay != null)  jumpscareOverlay.SetActive(false);
        if (hiddenIndicator != null)   hiddenIndicator.SetActive(false);
    }

    // ---- Event handlers (one-line each, kept compact) ---------------------
    private void ShowPauseMenu() { if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true); }
    private void HidePauseMenu() { if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false); }

    // When game-over fires, hide the jumpscare overlay (it was on during the
    // cinematic) and show the actual Game Over panel.
    private void ShowGameOver()
    {
        if (jumpscareOverlay != null) jumpscareOverlay.SetActive(false);
        if (gameOverPanel != null)    gameOverPanel.SetActive(true);
    }

    private void ShowVictory()   { if (victoryPanel != null)     victoryPanel.SetActive(true); }
    private void ShowJumpscare() { if (jumpscareOverlay != null) jumpscareOverlay.SetActive(true); }
}
