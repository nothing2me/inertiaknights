using UnityEngine;

public class MainMenuCamera : MonoBehaviour
{
    [Header("Camera Movement Settings")]
    [Tooltip("If true, the camera will slowly pan/rotate. If false, it remains static.")]
    public bool enableMovement = true;
    
    [Tooltip("Speed of the rotation/pan.")]
    public float panSpeed = 0.5f;
    
    [Tooltip("Maximum angle to pan left and right (e.g., 45 means it will swing from -45 to +45 degrees).")]
    public float maxPanAngle = 45f;

    [Tooltip("Axis to rotate around.")]
    public Vector3 rotationAxis = Vector3.up;

    private Quaternion startRotation;
    private float timeElapsed = 0f;

    private void Start()
    {
        // Store the initial rotation so we can pivot around it
        startRotation = transform.rotation;
    }

    private void Update()
    {
        if (enableMovement)
        {
            timeElapsed += Time.deltaTime * panSpeed;
            // Mathf.Sin loops smoothly from -1 to 1. Multiply by maxPanAngle to get the swing range.
            float angle = Mathf.Sin(timeElapsed) * maxPanAngle;
            
            // Apply the rotation relative to the starting rotation
            transform.rotation = startRotation * Quaternion.AngleAxis(angle, rotationAxis);
        }
    }
}
