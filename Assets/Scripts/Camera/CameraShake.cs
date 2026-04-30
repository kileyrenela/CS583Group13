using System.Collections;
using UnityEngine;

// =============================================================================
// CameraShake.cs
// =============================================================================
// PURPOSE
//   Procedurally shakes the camera for a given intensity and duration. Used by
//   the jumpscare cinematic in GameManager.
//
// PLACEMENT
//   Attach to the player's Camera GameObject (the child of the player). The
//   shake offsets transform.localPosition, so the shake is relative to the
//   parent (player body) - this means the shake works correctly even while
//   the player is moving.
//
// HOW IT WORKS
//   - Caches the camera's local position when shake starts
//   - Each frame, applies a random (x, y) offset scaled by intensity
//   - When duration expires, restores the original position
//
// NOTES & LIMITATIONS
//   - If multiple shakes overlap, the second one will save the SHAKEN position
//     as "original" - leading to drift. For our use case (one jumpscare per
//     game), this is fine. If we ever need overlapping shakes, refactor to
//     accumulate offsets each frame and lerp toward zero on stop.
//   - Shake is only on X/Y (screen plane). No Z because that would zoom in/out
//     uncomfortably for first-person.
//   - This uses Time.deltaTime which respects timeScale. If we ever want shake
//     during pause (we don't), switch to Time.unscaledDeltaTime.
// =============================================================================

public class CameraShake : MonoBehaviour
{
    // Public entry point. Call this from anywhere (e.g., GameManager.JumpscareSequence).
    public void Shake(float intensity, float duration)
    {
        StartCoroutine(ShakeCoroutine(intensity, duration));
    }

    private IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        // Snapshot the resting local position so we can restore it after.
        Vector3 originalPos = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Random offset in the [-intensity, +intensity] range each frame.
            float x = Random.Range(-1f, 1f) * intensity;
            float y = Random.Range(-1f, 1f) * intensity;
            transform.localPosition = originalPos + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;  // Wait one frame, then continue the loop
        }

        // Restore the camera to its rest position so it doesn't end up offset.
        transform.localPosition = originalPos;
    }
}
