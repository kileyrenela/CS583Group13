using System;
using UnityEngine;
using UnityEngine.AI;

// =============================================================================
// EnemyAI.cs
// =============================================================================
// PURPOSE
//   The brain of the enemy. Runs the finite state machine (Patrol, Investigate,
//   Chase, Search, Catch) and drives the NavMeshAgent's destination/speed each
//   frame. Detection logic is delegated to EnemyDetection - this file is
//   purely "what do I do given current state and detection results?"
//
// REQUIREMENTS
//   - NavMeshAgent component on this GameObject (auto-required)
//   - EnemyDetection component on this GameObject (auto-required)
//   - A baked NavMesh in the scene (Window > AI > Navigation > Bake)
//   - The Player GameObject tagged "Player" already in the scene at Start()
//   - Patrol waypoints assigned in Inspector (or enemy will stand still)
//
// HOW THE STATE MACHINE WORKS
//   - currentState dictates which Update*() method runs each frame
//   - SetState(newState) is the ONLY way to transition - it resets timers and
//     configures NavMeshAgent (speed, destination) for the new state
//   - Each Update*() polls EnemyDetection.CanDetectPlayer() and decides whether
//     to keep doing its thing or transition
//
// EVENTS
//   OnCatchPlayer fires when the enemy reaches the player (Catch state).
//   GameManager subscribes and triggers the jumpscare sequence.
//
// EXTENDING
//   - Add a new state? 1) add to EnemyState enum, 2) add an Update*() method,
//     3) add a case in Update() and SetState(), 4) wire transitions from other states
//   - Want enemy to react to sound events (e.g., dropped item)? Add a public
//     ReactToNoise(Vector3 pos) that forces Investigate at that position
// =============================================================================

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyDetection))]
public class EnemyAI : MonoBehaviour
{
    // ------------------------- Tuning (Inspector) ----------------------------
    // Designers can tweak these without touching code. Keep speed values
    // synced with the design spec table.

    [Header("Speeds")]
    [SerializeField] private float patrolSpeed = 2.5f;       // Calm walk
    [SerializeField] private float investigateSpeed = 3f;    // "Did I hear something?"
    [SerializeField] private float chaseSpeed = 4.5f;        // Sprint - faster than player walk (3.5), slower than player sprint (5.5) so sprinting CAN escape briefly
    [SerializeField] private float searchSpeed = 3f;         // Methodical area sweep

    [Header("Timers")]
    [SerializeField] private float investigateTime = 5f;     // How long to look around at last known pos
    [SerializeField] private float searchTime = 8f;          // How long to sweep nearby corridors after losing chase
    [SerializeField] private float loseChaseLOSTime = 3f;    // Seconds without LOS before giving up chase
    [SerializeField] private float catchDistance = 1.5f;     // Range at which "caught" triggers (jumpscare)

    [Header("Patrol")]
    [SerializeField] private PatrolWaypoint[] waypoints;     // Drag waypoints here in patrol order

    // ------------------------- Public events ---------------------------------
    // GameManager subscribes to this. Using event Action keeps the surface
    // area minimal compared to UnityEvents.
    public event Action OnCatchPlayer;

    // ------------------------- Cached references ----------------------------
    private NavMeshAgent agent;
    private EnemyDetection detection;
    private Transform player;

    // ------------------------- State variables -------------------------------
    private EnemyState currentState = EnemyState.Patrol;
    private int currentWaypointIndex;       // Which waypoint we're heading to next
    private float stateTimer;               // Generic per-state elapsed time
    private float lostSightTimer;           // Used in Chase to track how long we've been without LOS
    private bool waitingAtWaypoint;         // True while pausing at a waypoint
    private float waypointWaitTimer;

    public EnemyState CurrentState => currentState;

    // ------------------------- Unity lifecycle -------------------------------

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        detection = GetComponent<EnemyDetection>();
    }

    private void Start()
    {
        // Find the player ONCE. Player must exist in the scene at this point.
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        // Kick off in Patrol state. SetState configures speed and starts toward waypoint.
        SetState(EnemyState.Patrol);
    }

    private void Update()
    {
        // Dispatch to the per-state update. Each state owns its own transition logic.
        // Catch is terminal - nothing to update; the jumpscare cinematic runs in GameManager.
        switch (currentState)
        {
            case EnemyState.Patrol:      UpdatePatrol();      break;
            case EnemyState.Investigate: UpdateInvestigate(); break;
            case EnemyState.Chase:       UpdateChase();       break;
            case EnemyState.Search:      UpdateSearch();      break;
            case EnemyState.Catch:       /* terminal */       break;
        }
    }

    // ------------------------- State transitions -----------------------------
    // Centralized so ALL state changes go through one path. This keeps timers
    // reset properly and ensures NavMeshAgent settings match the new state.
    private void SetState(EnemyState newState)
    {
        currentState = newState;
        stateTimer = 0f;
        lostSightTimer = 0f;

        switch (newState)
        {
            case EnemyState.Patrol:
                agent.speed = patrolSpeed;
                GoToNextWaypoint();
                break;

            case EnemyState.Investigate:
                agent.speed = investigateSpeed;
                // Walk to where we last saw/heard the player.
                agent.SetDestination(detection.LastKnownPosition);
                break;

            case EnemyState.Chase:
                agent.speed = chaseSpeed;
                // Don't set destination here - UpdateChase does it every frame
                // because the player is moving.
                break;

            case EnemyState.Search:
                agent.speed = searchSpeed;
                SearchNearby();
                break;

            case EnemyState.Catch:
                agent.ResetPath();      // Stop moving - the jumpscare takes over
                OnCatchPlayer?.Invoke(); // Notify GameManager
                break;
        }
    }

    // ------------------------- PATROL ---------------------------------------
    // Walks the waypoint loop, pausing at each waypoint for its WaitTime.
    // Transitions: -> Chase if detected.
    private void UpdatePatrol()
    {
        // Detection check - any sign of player triggers chase.
        if (detection.CanDetectPlayer(currentState))
        {
            SetState(EnemyState.Chase);
            return;
        }

        // While pausing at a waypoint, run the wait timer.
        if (waitingAtWaypoint)
        {
            waypointWaitTimer += Time.deltaTime;
            if (waypoints.Length > 0 && waypointWaitTimer >= waypoints[currentWaypointIndex].WaitTime)
            {
                waitingAtWaypoint = false;
                // Loop back to 0 with modulo so the patrol cycles forever.
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                GoToNextWaypoint();
            }
            return;
        }

        // Have we arrived at the current waypoint? Use NavMeshAgent's standard
        // arrival check: not still computing the path AND within stoppingDistance.
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            waitingAtWaypoint = true;
            waypointWaitTimer = 0f;
        }
    }

    private void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        agent.SetDestination(waypoints[currentWaypointIndex].transform.position);
        waitingAtWaypoint = false;
    }

    // ------------------------- INVESTIGATE ----------------------------------
    // Walks to last known position, looks around for investigateTime seconds.
    // Transitions: -> Chase if spotted, -> Patrol if timer expires.
    private void UpdateInvestigate()
    {
        if (detection.CanDetectPlayer(currentState))
        {
            SetState(EnemyState.Chase);
            return;
        }

        stateTimer += Time.deltaTime;

        // Once we've arrived at the last-known position, slowly rotate to scan
        // the area. This is the "looking around" beat that gives the player
        // a chance to sneak past.
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            transform.Rotate(Vector3.up, 120f * Time.deltaTime);
        }

        // Give up after investigateTime and go back to patrolling.
        if (stateTimer >= investigateTime)
        {
            SetState(EnemyState.Patrol);
        }
    }

    // ------------------------- CHASE ----------------------------------------
    // Pursues the player at chaseSpeed. While we have LOS, destination updates
    // every frame to track them. Without LOS, we head to last known position
    // and start the lostSight timer.
    // Transitions: -> Catch if close enough, -> Search if LOS lost too long.
    private void UpdateChase()
    {
        if (player == null) return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // Caught the player! Immediate transition.
        if (distToPlayer <= catchDistance)
        {
            SetState(EnemyState.Catch);
            return;
        }

        if (detection.CanDetectPlayer(currentState))
        {
            // Fresh detection - reset the "lost sight" timer and chase live position.
            lostSightTimer = 0f;
            agent.SetDestination(player.position);
        }
        else
        {
            // Lost the player. Head toward last known position and tick the timer.
            lostSightTimer += Time.deltaTime;
            agent.SetDestination(detection.LastKnownPosition);

            if (lostSightTimer >= loseChaseLOSTime)
            {
                SetState(EnemyState.Search);
            }
        }
    }

    // ------------------------- SEARCH ---------------------------------------
    // Pokes around the area for searchTime seconds, picking random nearby
    // points to walk to.
    // Transitions: -> Chase if re-spotted, -> Patrol if timer expires.
    private void UpdateSearch()
    {
        if (detection.CanDetectPlayer(currentState))
        {
            SetState(EnemyState.Chase);
            return;
        }

        stateTimer += Time.deltaTime;

        // Reached our random search point? Pick another nearby.
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            SearchNearby();
        }

        if (stateTimer >= searchTime)
        {
            SetState(EnemyState.Patrol);
        }
    }

    // Picks a random NavMesh point within ~5u of last known position. Using
    // NavMesh.SamplePosition ensures the destination is reachable.
    private void SearchNearby()
    {
        Vector3 randomDir = UnityEngine.Random.insideUnitSphere * 5f;
        randomDir += detection.LastKnownPosition;

        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }
}
