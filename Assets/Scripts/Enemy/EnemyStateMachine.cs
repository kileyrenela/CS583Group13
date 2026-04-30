// =============================================================================
// EnemyStateMachine.cs
// =============================================================================
// PURPOSE
//   Defines the finite state machine (FSM) states for the enemy. This is just
//   the enum - the actual transition logic lives in EnemyAI.cs.
//
// STATES
//   Patrol      - Default. Walking the predefined waypoint loop.
//   Investigate - Heard/saw something at a distance. Walks to last known position
//                 and looks around for a few seconds.
//   Chase       - Confirmed visual on player. Sprints toward them.
//   Search      - Lost line-of-sight during chase. Pokes around the area for ~8s.
//   Catch       - Reached the player. Triggers jumpscare -> Game Over.
//
// TRANSITION DIAGRAM (see EnemyAI.cs for the actual code)
//
//   Patrol -----[detect at distance]-----> Investigate
//   Patrol -----[detect close/sprint]----> Chase
//   Investigate -[spot player]-----------> Chase
//   Investigate -[timer expires]---------> Patrol
//   Chase ------[lose LOS 3s]------------> Search
//   Chase ------[reach player]-----------> Catch
//   Search -----[re-spot player]---------> Chase
//   Search -----[timer expires]----------> Patrol
//   Catch ------[(terminal)]-------------> (GameOver via OnCatchPlayer event)
// =============================================================================

public enum EnemyState
{
    Patrol,
    Investigate,
    Chase,
    Search,
    Catch
}
