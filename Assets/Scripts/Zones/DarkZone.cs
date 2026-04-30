using UnityEngine;

// =============================================================================
// DarkZone.cs
// =============================================================================
// PURPOSE
//   Defines an "unlit" area where the player can become hidden by standing still.
//   The classic stealth-game shadow-hiding mechanic: enter shadow + stop moving
//   = invisible to most detection. Move = re-detectable.
//
// HOW IT WORKS
//   1. Player enters trigger -> we cache references
//   2. While player is inside AND velocity is near zero -> standStillTimer ticks up
//   3. Once timer >= hideDelay -> set PlayerVisibility to HiddenInDark
//   4. If player moves while hidden -> set back to Visible immediately
//   5. Player leaves trigger -> set Visible (if was hidden)
//
// SETUP IN SCENE
//   1. Create an empty GameObject with a Box Collider (or any collider)
//   2. Set the Collider's "Is Trigger" = true
//   3. Add this DarkZone component
//   4. Place where there are no torches/wall lights nearby (so the visuals match
//      the mechanic - it would feel weird if a "dark zone" was brightly lit)
//   5. Make sure the Player tag is "Player" or this won't recognize them
//
// IMPORTANT INTERACTION
//   - HiddenInChest takes priority. If the player walks into a dark zone WHILE
//     hidden in a chest (which shouldn't happen - they can't move - but as a
//     fail-safe), we don't override their state.
//   - EnemyDetection still detects HiddenInDark players within ~3u (breathing).
//     This is a feature: dark zones aren't free safety, they're tense.
//
// TUNING
//   - standStillThreshold (0.1): how slow counts as "still". Set higher if it
//     feels too sensitive.
//   - hideDelay (1s): grace before becoming hidden. Tune for tension - longer =
//     harder to hide reactively, shorter = easier to abuse.
// =============================================================================

[RequireComponent(typeof(Collider))]
public class DarkZone : MonoBehaviour
{
    [SerializeField] private float standStillThreshold = 0.1f; // Velocity below this = "still"
    [SerializeField] private float hideDelay = 1f;             // Seconds of stillness needed before hiding kicks in

    // Cached refs to the player components - looked up on enter to avoid
    // GetComponent calls every frame in OnTriggerStay.
    private PlayerController playerInZone;
    private PlayerVisibility playerVisibility;
    private float standStillTimer;

    // Fired once when the Player collider first overlaps the trigger.
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInZone = other.GetComponent<PlayerController>();
        playerVisibility = other.GetComponent<PlayerVisibility>();
        standStillTimer = 0f;
    }

    // Fired EVERY FixedUpdate while the player is inside. Most of the work
    // happens here.
    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player") || playerInZone == null) return;

        // Don't stomp on chest hiding. (In practice the player can't move
        // while in a chest, but this is a safety belt.)
        if (playerVisibility.CurrentState == VisibilityState.HiddenInChest) return;

        float speed = playerInZone.Velocity.magnitude;

        if (speed <= standStillThreshold)
        {
            // Player is standing still. Tick the timer; once it crosses hideDelay,
            // mark them hidden. SetState short-circuits if we're already hidden,
            // so calling it every frame is fine.
            standStillTimer += Time.deltaTime;
            if (standStillTimer >= hideDelay)
            {
                playerVisibility.SetState(VisibilityState.HiddenInDark);
            }
        }
        else
        {
            // Player started moving. Reset timer and un-hide if previously hidden.
            standStillTimer = 0f;
            if (playerVisibility.CurrentState == VisibilityState.HiddenInDark)
            {
                playerVisibility.SetState(VisibilityState.Visible);
            }
        }
    }

    // Fired once when the Player collider exits the trigger.
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // If the player was hidden when leaving, restore Visible state.
        // Without this, they'd remain "hidden" outside the zone forever.
        if (playerVisibility != null && playerVisibility.CurrentState == VisibilityState.HiddenInDark)
        {
            playerVisibility.SetState(VisibilityState.Visible);
        }

        playerInZone = null;
        playerVisibility = null;
        standStillTimer = 0f;
    }
}
