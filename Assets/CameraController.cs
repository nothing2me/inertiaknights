using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 pivotOffset = new Vector3(0, 1.5f, 0);

    [Header("Mouse Look")]
    [Range(0.5f, 15f)] public float mouseSensitivity = 3f;
    public bool invertY = false;
    [Range(-40f, 80f)] public float minPitch = -20f;
    [Range(-40f, 80f)] public float maxPitch = 80f;

    [Header("Camera Distance")]
    [Range(2f, 25f)] public float targetDistance = 7f;
    [Range(1f, 10f)] public float minFollowDistance = 2f;
    [Range(5f, 30f)] public float maxFollowDistance = 15f;

    [Header("Tilt (Super Monkey Ball Style)")]
    [Tooltip("Z-axis roll when strafing left/right. 0 = disabled.")]
    [Range(0f, 25f)] public float maxRollTilt = 10f;
    [Tooltip("Pitch nudge when pushing forward/backward. 0 = disabled.")]
    [Range(0f, 15f)] public float maxPitchLean = 5f;
    [Tooltip("How smoothly the roll/lean eases in and out")]
    [Range(0.01f, 0.5f)] public float tiltSmoothTime = 0.15f;

    [Header("Smoothing")]
    [Tooltip("Mouse look responsiveness (lower = snappier)")]
    [Range(0.01f, 0.3f)] public float mouseSmoothTime = 0.05f;
    [Tooltip("How smoothly the camera tracks the ball's position")]
    [Range(0.01f, 0.5f)] public float positionSmoothTime = 0.12f;
    [Tooltip("How smoothly camera distance adjusts on collision")]
    [Range(0.01f, 0.5f)] public float zoomSmoothTime = 0.2f;

    [Header("Collision")]
    public bool enableCollision = true;
    public LayerMask collisionLayer;
    [Range(0.05f, 1f)] public float cameraRadius = 0.2f;
    [Range(0f, 0.5f)] public float collisionBuffer = 0.1f;

    private InputAction moveInputAction;

    private Vector3 currentPivotPosition;
    private Vector3 pivotVelocity;
    private float currentYaw;
    private float targetYaw;
    private float yawVelocity;
    private float mousePitch = 20f;
    private float currentPitch;
    private float targetPitch;
    private float pitchVelocity;
    private float currentRoll;
    private float rollVelocity;
    private float currentDistance;
    private float distanceVelocity;
    private Rigidbody targetRb;

    void Start()
    {
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
            currentPivotPosition = target.position + pivotOffset;

            currentYaw = targetYaw = transform.eulerAngles.y;
            float rawPitch = transform.eulerAngles.x;
            mousePitch = Mathf.Clamp(rawPitch > 180f ? rawPitch - 360f : rawPitch, minPitch, maxPitch);
            currentPitch = targetPitch = mousePitch;
            currentRoll = 0f;
            currentDistance = targetDistance;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
            targetRb = target.GetComponent<Rigidbody>();
    }

    public void SetMoveAction(InputAction action)
    {
        moveInputAction = action;
    }

    public void SetActiveState(bool isActive)
    {
        Camera cam = GetComponent<Camera>();
        AudioListener listener = GetComponent<AudioListener>();

        if (cam != null) cam.enabled = isActive;
        if (listener != null) listener.enabled = isActive;

        if (isActive)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        currentPivotPosition = Vector3.SmoothDamp(
            currentPivotPosition,
            target.position + pivotOffset,
            ref pivotVelocity,
            positionSmoothTime
        );

        if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            float scale = mouseSensitivity * 0.1f;

            targetYaw += mouseDelta.x * scale;

            float yDir = invertY ? 1f : -1f;
            mousePitch += mouseDelta.y * scale * yDir;
            mousePitch = Mathf.Clamp(mousePitch, minPitch, maxPitch);
        }

        currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, mouseSmoothTime);

        Vector2 moveInput = Vector2.zero;
        if (moveInputAction != null && moveInputAction.enabled)
            moveInput = moveInputAction.ReadValue<Vector2>();

        float targetRollValue = -moveInput.x * maxRollTilt;
        currentRoll = Mathf.SmoothDamp(currentRoll, targetRollValue, ref rollVelocity, tiltSmoothTime);

        targetPitch = Mathf.Clamp(mousePitch - moveInput.y * maxPitchLean, minPitch, maxPitch);
        currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, mouseSmoothTime);

        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, currentRoll);

        float desiredDistance = targetDistance;
        if (enableCollision)
            desiredDistance = GetCollisionDistance(currentPivotPosition, rotation, targetDistance);

        currentDistance = Mathf.SmoothDamp(currentDistance, desiredDistance, ref distanceVelocity, zoomSmoothTime);

        transform.position = currentPivotPosition - (rotation * Vector3.forward * currentDistance);
        transform.rotation = rotation;
    }

    public void SnapToTarget()
    {
        if (target == null) return;

        currentPivotPosition = target.position + pivotOffset;
        currentYaw = targetYaw;
        currentPitch = targetPitch = mousePitch;
        currentRoll = 0f;
        currentDistance = targetDistance;

        yawVelocity = pitchVelocity = rollVelocity = distanceVelocity = 0;
        pivotVelocity = Vector3.zero;

        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, currentRoll);
        if (enableCollision)
            currentDistance = GetCollisionDistance(currentPivotPosition, rotation, targetDistance);

        transform.position = currentPivotPosition - (rotation * Vector3.forward * currentDistance);
        transform.rotation = rotation;
    }

    private float GetCollisionDistance(Vector3 pivot, Quaternion rotation, float maxDistance)
    {
        Vector3 direction = rotation * -Vector3.forward;

        Vector3 rayStart = pivot + direction * 0.5f;
        float castDist = maxDistance - 0.5f;

        if (Physics.SphereCast(rayStart, cameraRadius, direction, out RaycastHit hit, castDist, collisionLayer))
            return Mathf.Max(0.1f, hit.distance + 0.5f - collisionBuffer);

        return maxDistance;
    }
}
