// =============================================================================
// GameState.cs
// =============================================================================
// PURPOSE
//   Enum describing the high-level game state. GameManager is the only thing
//   that mutates this; many other systems READ it to know what to do (e.g.,
//   UIManager hides interaction prompts when not Playing).
//
// STATE MEANINGS
//   Playing  - Normal gameplay. Time scale 1, cursor locked, player input on.
//   Paused   - ESC menu open. Time scale 0, cursor visible, player input off.
//   GameOver - Caught by enemy after jumpscare. Game over screen visible.
//   Victory  - Reached the exit. Victory screen visible.
//
// WHY AN ENUM (NOT BOOLS)
//   States are mutually exclusive and we want exhaustive switch statements.
//   Using bools (isPaused, isDead, isWon) leads to invalid combinations like
//   "paused AND dead" - an enum makes that impossible.
// =============================================================================

public enum GameState
{
    Playing,
    Paused,
    GameOver,
    Victory
}
