using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds runtime references needed by cutscene steps.
/// Built once at trigger time by InteractableController or CutscenePlayer.PlayByName().
/// </summary>
public class CutsceneContext
{
    /// <summary>The local player's BallController (IsOwner == true).</summary>
    public BallController player;

    /// <summary>The local player's CameraController.</summary>
    public CameraController camera;

    /// <summary>The Transform of the object that was interacted with (may be null for group cutscenes).</summary>
    public Transform interactable;

    /// <summary>All CutsceneMarkers in the scene, keyed by markerName.</summary>
    public Dictionary<string, Transform> namedPoints;

    public CutsceneContext(BallController localPlayer, Transform interactableTransform)
    {
        player = localPlayer;
        camera = localPlayer?.CutsceneCameraRef;
        interactable = interactableTransform;

        // Scan scene for all CutsceneMarkers once at build time
        namedPoints = new Dictionary<string, Transform>();
        foreach (var marker in Object.FindObjectsByType<CutsceneMarker>(FindObjectsSortMode.None))
        {
            if (string.IsNullOrEmpty(marker.markerName)) continue;
            if (!namedPoints.ContainsKey(marker.markerName))
                namedPoints[marker.markerName] = marker.transform;
            else
                Debug.LogWarning($"[CutsceneContext] Duplicate CutsceneMarker name '{marker.markerName}'. First one used.");
        }
    }
}
