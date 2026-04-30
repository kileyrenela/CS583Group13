using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// =============================================================================
// GameManager.cs
// =============================================================================
// PURPOSE
//   Central orchestrator for the game's high-level flow. It does NOT contain
//   gameplay logic - that's in PlayerController, EnemyAI, etc. Instead it:
//     - Tracks the current GameState (Playing/Paused/GameOver/Victory)
//     - Fires events when state changes (UI listens to these)
//     - Runs the jumpscare cinematic (camera shake -> overlay -> game over)
//     - Wires the enemy's catch event to that cinematic
//     - Handles scene transitions (restart, return to menu, quit)
//
// SINGLETON PATTERN
//   GameManager.Instance is a static reference any script can reach. We use a
//   singleton because there's only ever ONE game state in a scene, and many
//   scripts (UI, ExitZone, menu buttons) need to talk to it.
//   Note: NOT a DontDestroyOnLoad singleton. Each scene has its own
//   GameManager. If you ever want to persist data between scenes (e.g.,
//   high scores), introduce a separate persistent manager.
//
// PLACEMENT IN SCENE
//   Put one GameManager component on a "GameManager" empty GameObject in the
//   Game.unity scene. The CameraShake component should be on the player camera
//   so this can find it via FindAnyObjectByType.
//
// EVENT FLOW (Caught by enemy)
//   1. EnemyAI enters Catch state -> fires OnCatchPlayer
//   2. GameManager.TriggerJumpscare() runs JumpscareSequence coroutine
//   3. OnJumpscareStart fires -> UIManager shows the jumpscare overlay
//   4. CameraShake.Shake runs concurrently
//   5. After all timers, SetState(GameOver) -> OnGameOver fires -> UIManager shows Game Over panel
// =============================================================================

public class GameManager : MonoBehaviour
{
    // Static singleton reference. Assigned in Awake. Other scripts use
    // GameManager.Instance.<method>.
    public static GameManager Instance { get; private set; }

    // ------------------------- Jumpscare timing ------------------------------
    // Tweak these in the Inspector to adjust the jumpscare feel.
    [Header("Jumpscare")]
    [SerializeField] private float shakeIntensity = 0.3f;          // Camera shake amplitude
    [SerializeField] private float shakeDuration = 0.3f;           // How long the shake lasts
    [SerializeField] private float jumpscareImageDuration = 0.5f;  // Enemy face on screen
    [SerializeField] private float fadeToBlackDuration = 0.3f;     // Fade before Game Over screen

    // ------------------------- Public events ---------------------------------
    // Subscribers wire up in their Start() and unsubscribe in OnDestroy().
    // Using events keeps GameManager loosely coupled to UI/audio - it just
    // shouts "game is over!" and doesn't care who's listening.
    public event Action OnGameOver;
    public event Action OnVictory;
    public event Action OnPause;
    public event Action OnResume;
    public event Action OnJumpscareStart;

    // ------------------------- Cached references ----------------------------
    private GameState currentState = GameState.Playing;
    private PlayerController playerController;
    private PlayerInteraction playerInteraction;

    public GameState CurrentState => currentState;

    // ------------------------- Singleton bootstrap --------------------------
    private void Awake()
    {
        // Standard singleton guard: if a duplicate exists (e.g., scene was loaded twice),
        // destroy this one.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Cache the player references. Done here (not Awake) because Awake order
        // is non-deterministic; by Start, all Awakes have run.
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerController = playerObj.GetComponent<PlayerController>();
            playerInteraction = playerObj.GetComponent<PlayerInteraction>();
        }

        // Subscribe to the enemy's catch event. There should only be one enemy
        // in the scene; if you ever support multiple, change to FindObjectsByType
        // and subscribe to each.
        var enemy = FindAnyObjectByType<EnemyAI>();
        if (enemy != null)
            enemy.OnCatchPlayer += TriggerJumpscare;

        SetState(GameState.Playing);
    }

    // Always unsubscribe in OnDestroy to prevent leaks / NullReferenceExceptions
    // on scene reload.
    private void OnDestroy()
    {
        var enemy = FindAnyObjectByType<EnemyAI>();
        if (enemy != null)
            enemy.OnCatchPlayer -= TriggerJumpscare;
    }

    // ------------------------- State transitions -----------------------------
    // Centralized state-change. Each branch:
    //   1. Sets Time.timeScale (pauses physics/animation)
    //   2. Sets cursor lock/visibility (so menus are clickable)
    //   3. Toggles player input (so player can't move during pause/death)
    //   4. Fires the matching event (so UI can show/hide panels)
    public void SetState(GameState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case GameState.Playing:
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                SetPlayerInput(true);
                break;

            case GameState.Paused:
                Time.timeScale = 0f;     // Freezes the world (Update still runs, but Time.deltaTime = 0)
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SetPlayerInput(false);
                OnPause?.Invoke();
                break;

            case GameState.GameOver:
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SetPlayerInput(false);
                OnGameOver?.Invoke();
                break;

            case GameState.Victory:
                // No timescale freeze - we want any victory animations/audio to play through.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SetPlayerInput(false);
                OnVictory?.Invoke();
                break;
        }
    }

    // Called by ESC keybind (PauseMenuUI.OnPause) and the Resume button.
    public void TogglePause()
    {
        if (currentState == GameState.Playing)
            SetState(GameState.Paused);
        else if (currentState == GameState.Paused)
        {
            SetState(GameState.Playing);
            OnResume?.Invoke();
        }
    }

    // Called by EnemyAI's OnCatchPlayer event. Starts the cinematic.
    public void TriggerJumpscare()
    {
        // Guard against re-triggering after game is already over (e.g., enemy
        // catches player again during fade).
        if (currentState != GameState.Playing) return;
        SetPlayerInput(false);
        StartCoroutine(JumpscareSequence());
    }

    // Called by ExitZone trigger when player reaches the end.
    public void TriggerVictory()
    {
        if (currentState != GameState.Playing) return;
        SetState(GameState.Victory);
    }

    // Coroutine driving the jumpscare cinematic. Sequenced with WaitForSeconds.
    // NOTE: Uses unscaled-or-scaled time? -> WaitForSeconds uses Time.timeScale.
    // Since we don't change timeScale until SetState(GameOver) at the end, this
    // is fine. If we ever pause time during the jumpscare, swap to WaitForSecondsRealtime.
    private IEnumerator JumpscareSequence()
    {
        OnJumpscareStart?.Invoke();   // UI shows the enemy face overlay

        // Camera shake runs in parallel with the wait below.
        var shake = FindAnyObjectByType<CameraShake>();
        if (shake != null)
            shake.Shake(shakeIntensity, shakeDuration);

        yield return new WaitForSeconds(shakeDuration);
        yield return new WaitForSeconds(jumpscareImageDuration);
        yield return new WaitForSeconds(fadeToBlackDuration);

        // Final transition - shows the Game Over screen via OnGameOver event.
        SetState(GameState.GameOver);
    }

    // ------------------------- Scene transitions ----------------------------
    // Called by buttons on the GameOver/Victory panels.

    public void RestartGame()
    {
        // CRITICAL: reset timeScale before scene load, otherwise the new scene
        // loads with Time.timeScale = 0 and nothing animates.
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
        // NOTE: The "MainMenu" scene must be added to Build Settings
        // (File > Build Settings) or this will throw at runtime.
    }

    // Fully exit the application. In the Editor this is a no-op (you have to
    // press Stop manually). In a built game it closes the window.
    public void QuitGame()
    {
        Application.Quit();
    }

    // ------------------------- Helpers --------------------------------------
    private void SetPlayerInput(bool enabled)
    {
        if (playerController != null) playerController.SetInputEnabled(enabled);
        if (playerInteraction != null) playerInteraction.SetInputEnabled(enabled);
    }
}
