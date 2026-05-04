using System.Collections;
using UnityEngine;

/// <summary>
/// Instantly moves the local player to a CutsceneMarker by name.
/// Also zeroes their Rigidbody velocity and snaps the camera.
/// </summary>
[System.Serializable]
public class TeleportPlayerStep : CutsceneStep
{
    [Tooltip("Name of a CutsceneMarker in the scene to teleport to.")]
    public string markerName;

    public override IEnumerator Execute(CutsceneContext ctx)
    {
        if (ctx?.player == null) yield break;
        if (string.IsNullOrEmpty(markerName) || ctx.namedPoints == null) yield break;

        if (!ctx.namedPoints.TryGetValue(markerName, out Transform target))
        {
            Debug.LogWarning($"[TeleportPlayerStep] No CutsceneMarker named '{markerName}'.");
            yield break;
        }

        ctx.player.transform.position = target.position;

        var rb = ctx.player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        ctx.camera?.SnapToTarget();
    }
}
