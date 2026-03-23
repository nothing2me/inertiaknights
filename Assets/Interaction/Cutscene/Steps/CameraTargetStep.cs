using System.Collections;
using UnityEngine;

/// <summary>
/// Points the local player's CameraController at a CutsceneMarker.
/// Leave markerName empty to restore camera back to the player.
/// On restore, calls SnapToTarget() to prevent jitter from velocity bleed.
/// </summary>
[System.Serializable]
public class CameraTargetStep : CutsceneStep
{
    [Tooltip("Name of a CutsceneMarker to look at. Leave empty to restore to player.")]
    public string markerName;
    [Tooltip("Seconds to hold this view before the next step runs.")]
    public float holdDuration = 0f;

    public override IEnumerator Execute(CutsceneContext ctx)
    {
        if (ctx?.camera == null) yield break;

        if (string.IsNullOrEmpty(markerName))
        {
            // Restore to player
            ctx.camera.SetTarget(ctx.player?.transform);
            ctx.camera.SnapToTarget();
        }
        else
        {
            if (ctx.namedPoints == null || !ctx.namedPoints.TryGetValue(markerName, out Transform target))
            {
                Debug.LogWarning($"[CameraTargetStep] No CutsceneMarker named '{markerName}'.");
                yield break;
            }
            ctx.camera.SetTarget(target);
        }

        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);
    }
}
