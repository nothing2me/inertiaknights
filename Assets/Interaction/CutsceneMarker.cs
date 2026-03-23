using UnityEngine;

/// <summary>
/// Place on any empty GameObject in the scene.
/// CutsceneContext scans for all markers at trigger time and builds a name→Transform dictionary.
/// Reference markers by name in TeleportPlayerStep and CameraTargetStep.
/// </summary>
public class CutsceneMarker : MonoBehaviour
{
    [Tooltip("Unique name used to look up this point from cutscene steps.")]
    public string markerName;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"[Marker] {markerName}");
#endif
    }
}
