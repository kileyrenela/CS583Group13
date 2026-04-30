using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================================
// PlayerInteraction.cs
// =============================================================================
// PURPOSE
//   Detects what the player is looking at (chests, doors, exit zone, etc.) and
//   handles the "press E" interaction. Uses a forward raycast from the camera.
//
// HOW IT WORKS
//   Every frame we cast a ray from the camera's position along its forward
//   direction. If we hit something with an IInteractable component within
//   `interactRange`, we expose it as `CurrentTarget`. Pressing E (the Interact
//   action) calls Interact() on that target.
//
// COUPLING
//   - Reads from: child Camera (for ray origin/direction)
//   - Writes to: any IInteractable component (e.g., HideableChest)
//   - UIManager polls CurrentTarget to show/hide the "Press E to hide" prompt
//
// EXTENDING
//   To add a new interactable type (e.g., a door, key pickup, lever), create a
//   MonoBehaviour that implements IInteractable. No changes needed here.
// =============================================================================

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactRange = 2.5f;  // Roughly arm's length
    [SerializeField] private LayerMask interactLayer = ~0; // ~0 = "Everything";
                                                           // for performance, set to a specific
                                                           // "Interactable" layer in the Inspector
                                                           // once we have one.

    private Camera playerCamera;
    private IInteractable currentTarget;
    private bool inputEnabled = true;

    public IInteractable CurrentTarget => currentTarget;

    private void Awake()
    {
        // Find the FPS camera that's a child of the player. There should only be one.
        playerCamera = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (!inputEnabled) return;
        CheckForInteractable();
    }

    // Sweep a ray each frame to update CurrentTarget. Cheap because the ray is
    // short (2.5u) and only one cast per frame. If we ever raycast against
    // the entire scene at long range every frame, optimize with a layer mask.
    private void CheckForInteractable()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactLayer))
        {
            // GetComponent looks for IInteractable on the hit object. Walls and
            // floor return null and clear CurrentTarget below.
            var interactable = hit.collider.GetComponent<IInteractable>();
            currentTarget = interactable;
        }
        else
        {
            currentTarget = null;
        }
    }

    // Wired to the "Interact" action via PlayerInput Inspector binding.
    public void OnInteract(InputAction.CallbackContext context)
    {
        // performed = the button was actually pressed this frame (filters out started/canceled).
        if (!context.performed || !inputEnabled) return;

        // Pass the PlayerController to the interactable so it can manipulate
        // movement/visibility (e.g., chest disables controller while hiding).
        currentTarget?.Interact(GetComponent<PlayerController>());
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        // Clear target when disabled so the UI prompt hides immediately.
        if (!enabled) currentTarget = null;
    }
}
