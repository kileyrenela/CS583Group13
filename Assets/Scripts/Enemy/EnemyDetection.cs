using UnityEngine;

// =============================================================================
// EnemyDetection.cs
// =============================================================================
// PURPOSE
//   Answers ONE question: "Can I detect the player right now?"
//   Owns all the rules for sight, hearing, sprint noise, and how hiding affects
//   detectability. EnemyAI calls CanDetectPlayer() each frame to decide state
//   transitions; the AI itself has no detection logic.
//
// DETECTION PRIORITY (checked in order in CanDetectPlayer)
//   1. Player is in a chest -> ALWAYS undetectable (immediate, hard rule)
//   2. Player is in a dark zone -> only detectable within darkZoneOverrideRange (~3u)
//   3. Grace period -> if the player JUST hid, enemy "remembers" them for hideGracePeriod
//      seconds (prevents last-millisecond cheese-hides right under the enemy's nose)
//   4. Hearing -> within hearingRange (~5u), detected regardless of LOS
//   5. Sprint noise -> if sprinting AND within sprintNoiseRange (~10u), detected without LOS
//   6. Line of sight -> within sight range, in FOV cone, no walls between us
//
// REQUIRES (in scene)
//   - A GameObject tagged "Player" with PlayerVisibility and PlayerController components
//   - obstacleMask LayerMask set in Inspector to layers that block sight (Walls, Default, etc.)
//
// TUNING TIPS
//   - If enemy feels too easy: increase patrolSightRange, decrease darkZoneOverrideRange
//   - If enemy feels unfair: lower sprintNoiseRange, increase hideGracePeriod
//   - If enemy detects through walls: obstacleMask doesn't include the wall layer
// =============================================================================

public class EnemyDetection : MonoBehaviour
{
    [Header("Line of Sight")]
    [SerializeField] private float patrolSightRange = 15f;  // Eyesight when calm
    [SerializeField] private float chaseSightRange = 25f;   // "Hyper-aware" range during chase
    [SerializeField] private float fieldOfView = 120f;      // Cone width in degrees (full angle, not half)

    [Header("Proximity")]
    [SerializeField] private float hearingRange = 5f;             // Always hears within this range
    [SerializeField] private float sprintNoiseRange = 10f;        // Sprinting is louder
    [SerializeField] private float darkZoneOverrideRange = 3f;    // Even in dark, breathing gives you away

    [Header("Hiding")]
    [SerializeField] private float hideGracePeriod = 1.5f;  // Anti-cheese: enemy keeps tracking briefly after you hide

    [Header("References")]
    [SerializeField] private LayerMask obstacleMask;  // Which layers block line-of-sight (Walls, etc.)

    // Cached references to the player. Looked up once in Start to avoid the
    // cost of FindGameObjectWithTag every frame.
    private Transform player;
    private PlayerVisibility playerVisibility;
    private PlayerController playerController;

    // Grace-period bookkeeping
    private float timeSinceLastSeen;
    private bool playerWasVisible;

    // The most recent position where we KNOW the player was. Used by EnemyAI
    // for Investigate/Search states to navigate to "where I last saw them".
    private Vector3 lastKnownPosition;

    public Vector3 LastKnownPosition => lastKnownPosition;

    private void Start()
    {
        // Find the player at scene start. WARNING: if the player is spawned
        // dynamically AFTER the enemy's Start(), this will be null. For our
        // current design (player exists in the scene at load), this is fine.
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerVisibility = playerObj.GetComponent<PlayerVisibility>();
            playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    // ------------------------- Main API --------------------------------------
    // Called by EnemyAI every frame. Returns true if the enemy should react
    // (chase, investigate, etc.). Also updates lastKnownPosition as a side-effect
    // when the player is detected.
    public bool CanDetectPlayer(EnemyState currentState)
    {
        if (player == null) return false;

        float distance = Vector3.Distance(transform.position, player.position);

        // ---- Hiding rules (early-outs) -------------------------------------
        if (playerVisibility != null && playerVisibility.IsHidden)
        {
            // Inside a chest? Completely undetectable. No range check, no anything.
            if (playerVisibility.CurrentState == VisibilityState.HiddenInChest)
                return false;

            // In a dark zone, you're invisible UNLESS the enemy is right on top
            // of you (breathing/clothing rustle). This is the "tense moment when
            // they walk past your hiding spot" mechanic.
            if (playerVisibility.CurrentState == VisibilityState.HiddenInDark)
            {
                if (distance <= darkZoneOverrideRange)
                {
                    UpdateLastKnownPosition();
                    return true;
                }
                return false;
            }
        }

        // ---- Grace period --------------------------------------------------
        // If the player was visible last frame and just hid, the enemy continues
        // tracking for hideGracePeriod seconds. Prevents the player from running
        // into a chest mid-chase and instantly disappearing (which feels cheap).
        // NOTE: This block is currently somewhat dead code because the early-outs
        // above return before we reach here when IsHidden. Left in place as a
        // fail-safe and intentional design hook. TODO: revisit if balance feedback
        // suggests grace period should kick in for chest hides too.
        if (playerWasVisible && playerVisibility != null && playerVisibility.IsHidden)
        {
            timeSinceLastSeen += Time.deltaTime;
            if (timeSinceLastSeen < hideGracePeriod)
                return true;
            playerWasVisible = false;
            return false;
        }

        // ---- Hearing (close range, ignores LOS) ----------------------------
        if (distance <= hearingRange)
        {
            UpdateLastKnownPosition();
            return true;
        }

        // ---- Sprint noise (medium range, ignores LOS) ----------------------
        // Sprinting in place doesn't count - PlayerController.IsSprinting requires
        // both the button held AND active movement.
        if (playerController != null && playerController.IsSprinting && distance <= sprintNoiseRange)
        {
            UpdateLastKnownPosition();
            return true;
        }

        // ---- Line of sight -------------------------------------------------
        // Sight range expands during chase to model "I've locked onto you".
        float sightRange = currentState == EnemyState.Chase ? chaseSightRange : patrolSightRange;
        if (distance <= sightRange && HasLineOfSight())
        {
            UpdateLastKnownPosition();
            return true;
        }

        return false;
    }

    // ------------------------- Line-of-sight check ---------------------------
    // Two gates:
    //   1) Is the player inside the FOV cone?
    //   2) If so, is there a wall between us?
    private bool HasLineOfSight()
    {
        if (player == null) return false;

        // Step 1: angle gate.
        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToPlayer);

        // fieldOfView is the FULL cone angle (e.g., 120° = 60° to either side).
        if (angle > fieldOfView * 0.5f)
            return false;

        // Step 2: occlusion check. Cast from "eye height" to "player chest height"
        // so we don't accidentally aim at the floor or a low railing.
        float distance = Vector3.Distance(transform.position, player.position);
        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 playerCenter = player.position + Vector3.up * 1f;

        if (Physics.Raycast(eyePos, (playerCenter - eyePos).normalized, out RaycastHit hit, distance, obstacleMask))
        {
            // We hit SOMETHING in obstacleMask before reaching the player.
            // If that "something" is the Player itself (rare - depends on layer setup),
            // we have LOS. Otherwise, a wall is in the way.
            if (!hit.transform.CompareTag("Player"))
                return false;
        }

        return true;
    }

    // Updates the position the enemy will navigate to in Investigate/Search.
    // Also resets grace-period tracking.
    private void UpdateLastKnownPosition()
    {
        lastKnownPosition = player.position;
        playerWasVisible = true;
        timeSinceLastSeen = 0f;
    }
}
