using UnityEngine;

// =============================================================================
// PatrolWaypoint.cs
// =============================================================================
// PURPOSE
//   Marks a single point in the world that the enemy will visit on patrol.
//   Place these in the editor along main corridors; the enemy walks them in
//   array order, looping back to the first when it reaches the last.
//
// USAGE
//   1) Create empty GameObjects in the scene (name them "Waypoint_01", etc.)
//   2) Add this PatrolWaypoint component to each
//   3) On the Enemy GameObject, drag them in order into EnemyAI's "Waypoints" array
//   4) Tune `waitTime` per waypoint if you want the enemy to pause longer in
//      certain spots (e.g., guarding a key intersection)
//
// VISUALIZATION
//   OnDrawGizmos draws a yellow wire sphere in the Scene view so you can see
//   waypoint placement at design time. Gizmos only render in the editor - no
//   runtime cost.
//
// LEVEL DESIGN TIP (for Nick)
//   - Place waypoints so the patrol loop covers most main corridors.
//   - Avoid putting two waypoints in line-of-sight of each other - it makes the
//     enemy feel like it's just walking back and forth in one spot.
//   - The NavMeshAgent will path BETWEEN waypoints, so put them at decision
//     points (intersections, room entries) rather than mid-corridor.
// =============================================================================

public class PatrolWaypoint : MonoBehaviour
{
    // How long the enemy pauses at this waypoint before moving on. Longer waits
    // at strategic spots (e.g., near the player spawn, near the exit) make the
    // enemy feel more deliberate.
    [SerializeField] private float waitTime = 1f;

    public float WaitTime => waitTime;

    // Editor-only visualization. The yellow sphere appears in the Scene view
    // so designers can see waypoints without selecting them.
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}
