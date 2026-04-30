// =============================================================================
// IInteractable.cs
// =============================================================================
// PURPOSE
//   Common interface for anything the player can interact with via the E key.
//   PlayerInteraction raycasts forward, looks for an IInteractable on the hit
//   object, and calls Interact() when E is pressed.
//
// IMPLEMENTATIONS
//   - HideableChest (chest hide mechanic)
//   - (future) Door, Lever, KeyPickup, NPC dialogue, etc.
//
// HOW TO ADD A NEW INTERACTABLE
//   1. Create a new MonoBehaviour (e.g., DoorInteractable.cs)
//   2. Implement IInteractable
//   3. Provide an InteractionPrompt string (shown by UIManager: "Press E to open")
//   4. Implement Interact(PlayerController) - the player ref lets you manipulate
//      movement, visibility, etc.
//   5. Attach to a GameObject with a Collider so the raycast can hit it
//   6. (Optional) Put it on an "Interactable" layer and update PlayerInteraction's
//      LayerMask for performance
// =============================================================================

public interface IInteractable
{
    // Shown by the UI when the player is looking at this object.
    // Should be context-aware (e.g., "Press E to hide" vs "Press E to exit").
    string InteractionPrompt { get; }

    // Called when the player presses E while looking at this object.
    // Receives the PlayerController so the implementation can disable input,
    // teleport the player, change visibility state, etc.
    void Interact(PlayerController player);
}
