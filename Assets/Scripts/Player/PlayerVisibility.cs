using System;
using UnityEngine;

// =============================================================================
// PlayerVisibility.cs
// =============================================================================
// PURPOSE
//   Tracks whether the player is currently hideable from the enemy and broadcasts
//   changes via an event. This is the single source of truth for "can the enemy
//   detect me right now?" - other systems READ from here, never duplicate the logic.
//
// WHO WRITES TO IT
//   - HideableChest    -> SetState(HiddenInChest) on enter, Visible on exit
//   - DarkZone         -> SetState(HiddenInDark) after stand-still timer, Visible when moving/leaving
//
// WHO READS FROM IT
//   - EnemyDetection   -> blocks detection or shortens range based on state
//   - UIManager        -> shows the "hidden" vignette/indicator in HiddenInDark
//
// DESIGN NOTE
//   Using an event (OnVisibilityChanged) instead of polling means UI/Enemy code
//   only reacts to actual transitions, not every frame. Event-driven > polled
//   when the value changes infrequently.
// =============================================================================

// The three possible visibility states. Order doesn't matter functionally,
// but keep "Visible" first (= default zero value) so a fresh component starts
// in the safe state.
public enum VisibilityState
{
    Visible,         // Enemy can detect via sight, hearing, or sprint noise
    HiddenInChest,   // Completely undetectable (chest is sealed)
    HiddenInDark     // Detectable only at very close range (~3u, "breathing")
}

public class PlayerVisibility : MonoBehaviour
{
    // Subscribers: UIManager (for indicator), and any future systems that care.
    // Using System.Action<T> keeps it lightweight - no UnityEvent overhead.
    public event Action<VisibilityState> OnVisibilityChanged;

    private VisibilityState currentState = VisibilityState.Visible;

    // Read-only public access. External systems must use SetState() to change.
    public VisibilityState CurrentState => currentState;

    // Convenience: most code just wants "is the player hidden at all?"
    public bool IsHidden => currentState != VisibilityState.Visible;

    // The ONLY way to mutate state. This:
    //   1) Skips redundant transitions (no event spam if state doesn't change)
    //   2) Fires the event so reactive systems (UI, enemy) can respond
    public void SetState(VisibilityState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        OnVisibilityChanged?.Invoke(currentState);
    }
}
