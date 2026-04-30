using UnityEngine;

// =============================================================================
// HideableChest.cs
// =============================================================================
// PURPOSE
//   The "hide in a chest" mechanic. Implements IInteractable so PlayerInteraction
//   picks it up automatically. When the player presses E:
//     - First press: enter chest (teleport in, lock movement, mark HiddenInChest)
//     - Second press: exit chest (teleport back, unlock movement, mark Visible)
//
// SETUP IN SCENE
//   1. Create the chest prefab (visual mesh + collider)
//   2. Add this component to it
//   3. Create a child empty GameObject named "HidePosition" placed inside/behind
//      the chest mesh. This is where the camera teleports to.
//   4. Drag that child into the `hidePosition` field
//   5. (Optional) Create an overlay UI/mesh that shows "you're inside the chest"
//      view (dark interior with peek slit) and assign to chestVisualOverlay
//
// IMPORTANT GOTCHAS
//   - CharacterController must be DISABLED before teleporting via transform.position;
//     otherwise it fights you and snaps you back. We disable -> set position -> enable.
//   - Re-entering the chest should restore the player to their pre-hide position
//     and rotation, NOT spawn them on top of the chest (they could get stuck on it).
//   - Player input is fully disabled while hidden - they can't accidentally walk
//     out of the chest mesh.
//
// FUTURE IMPROVEMENTS / TODO
//   - Add audio cue: chest creak on enter/exit
//   - Add tension audio: enemy footsteps get louder via 3D spatial audio
//     (already part of the spec - ties to Victor's audio work)
//   - Consider a "peek" mechanic: hold mouse to peek through the chest slit
//     and see the enemy. Adds tension but currently out of scope.
// =============================================================================

public class HideableChest : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform hidePosition;        // Empty child GameObject - "where the camera goes"
    [SerializeField] private GameObject chestVisualOverlay; // Optional: overlay shown while hidden (dark + slit)

    // Dynamic prompt - changes based on whether someone's already inside.
    public string InteractionPrompt => isOccupied ? "Press E to exit" : "Press E to hide";

    private bool isOccupied;
    private PlayerController hiddenPlayer;
    private PlayerVisibility playerVisibility;
    private Vector3 savedPlayerPosition;     // So we can put them back where they were
    private Quaternion savedPlayerRotation;

    // IInteractable.Interact(): toggles enter/exit based on current state.
    public void Interact(PlayerController player)
    {
        if (isOccupied)
            ExitChest(player);
        else
            EnterChest(player);
    }

    // ------------------------- Entering -------------------------------------
    private void EnterChest(PlayerController player)
    {
        isOccupied = true;
        hiddenPlayer = player;
        playerVisibility = player.GetComponent<PlayerVisibility>();

        // Save where the player was so ExitChest can restore them.
        savedPlayerPosition = player.transform.position;
        savedPlayerRotation = player.transform.rotation;

        // Disable player input first so they can't fight the teleport.
        player.SetInputEnabled(false);

        // Disable the CharacterController BEFORE moving the transform - otherwise
        // it interprets the teleport as collision and pushes back.
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;

        // Teleport. Prefer the explicit hidePosition; fall back to "above the chest"
        // if the designer forgot to assign it.
        if (hidePosition != null)
        {
            player.transform.position = hidePosition.position;
            player.transform.rotation = hidePosition.rotation;
        }
        else
        {
            player.transform.position = transform.position + Vector3.up * 0.5f;
        }

        // Re-enable controller. Now physics works normally from the new position.
        cc.enabled = true;

        // Show the visual overlay if assigned.
        if (chestVisualOverlay != null)
            chestVisualOverlay.SetActive(true);

        // Tell PlayerVisibility we're hidden - this propagates to EnemyDetection.
        if (playerVisibility != null)
            playerVisibility.SetState(VisibilityState.HiddenInChest);
    }

    // ------------------------- Exiting --------------------------------------
    private void ExitChest(PlayerController player)
    {
        isOccupied = false;

        // Same disable/move/enable dance as enter, in reverse direction.
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = savedPlayerPosition;
        player.transform.rotation = savedPlayerRotation;
        cc.enabled = true;

        // Restore input.
        player.SetInputEnabled(true);

        if (chestVisualOverlay != null)
            chestVisualOverlay.SetActive(false);

        if (playerVisibility != null)
            playerVisibility.SetState(VisibilityState.Visible);

        // Clear our refs so we don't accidentally hold onto a stale player
        // (matters if scene is reloaded mid-hide, etc.).
        hiddenPlayer = null;
        playerVisibility = null;
    }
}
