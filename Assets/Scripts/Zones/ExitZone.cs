using UnityEngine;

// =============================================================================
// ExitZone.cs
// =============================================================================
// PURPOSE
//   Marks the win condition. When the player walks into this trigger, the game
//   transitions to the Victory state (UIManager shows the victory screen).
//
// SETUP
//   1. Create an empty GameObject near the end-of-maze treasure chest
//   2. Add a Collider with Is Trigger = true (Box Collider sized to fill the area)
//   3. Add this component
//   4. Make sure GameManager is in the scene (this calls GameManager.Instance)
//
// WHY THE `triggered` FLAG
//   Without it, OnTriggerEnter might fire twice if the player's collider has
//   multiple intersections, or rapidly re-fire if they enter/exit. The flag
//   ensures Victory only triggers once per scene load.
// =============================================================================

[RequireComponent(typeof(Collider))]
public class ExitZone : MonoBehaviour
{
    private bool triggered;  // One-shot guard

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        // GameManager handles disabling input, showing UI, etc.
        GameManager.Instance.TriggerVictory();
    }
}
