using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform target;
    public Vector3 pivotOffset = new Vector3(0, 1.5f, 0); // Offset from ball center
    public float targetDistance = 7f;
    public float minFollowDistance = 2f;
    public float maxFollowDistance = 15f;
    
    [Header("Smoothing Settings")]
    public float positionSmoothTime = 0.1f;
    public float rotationSmoothTime = 0.05f;
    public float zoomSmoothTime = 0.2f;

    [Header("Rotation Settings")]
    public float manualRotationSpeed = 2f;
    public float autoRotationSpeed = 5f;
    public float minPitch = -20f;
    public float maxPitch = 80f;
    public bool followMovementDirection = true;

    [Header("Collision Settings")]
    public bool enableCollision = true;
    public LayerMask collisionLayer;
    public float cameraRadius = 0.2f;
    public float collisionBuffer = 0.1f;

    // State variables
    private Vector3 currentPivotPosition;
    private Vector3 pivotVelocity;
    private float currentYaw;
    private float currentPitch;
    private float targetYaw;
    private float targetPitch;
    private float yawVelocity;
    private float pitchVelocity;
    private float currentDistance;
    private float distanceVelocity;
    private bool isManualControl = false;
    private Rigidbody targetRb;
    private Vector3 lastMovementDir = Vector3.forward;

    void Start()
    {
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
            currentPivotPosition = target.position + pivotOffset;
            
            // Initialize yaw/pitch based on starting transform
            currentYaw = targetYaw = transform.eulerAngles.y;
            currentPitch = targetPitch = transform.eulerAngles.x;
            if (currentPitch > 180) currentPitch = targetPitch -= 360;
            
            currentDistance = targetDistance;
        }
        Debug.Log($"[CameraController] Started on {gameObject.name}. Target: {(target != null ? target.name : "None")}. Collision Layer: {collisionLayer.value}");
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
        }
    }

    public void SetActiveState(bool isActive)
    {
        Camera cam = GetComponent<Camera>();
        AudioListener listener = GetComponent<AudioListener>();
        
        if (cam != null) cam.enabled = isActive;
        if (listener != null) listener.enabled = isActive;
    }

    // Awake unparenting removed - now handled by BallController.OnNetworkSpawn for the owner only.

    void Update()
    {
        if (target == null) return;
        HandleManualInput();
    }

    void LateUpdate() 
    {
        if (target == null) return;

        // 1. Pivot Tracking
        currentPivotPosition = Vector3.SmoothDamp(currentPivotPosition, target.position + pivotOffset, ref pivotVelocity, positionSmoothTime);
        // 2. Rotation Blending (Auto-Follow or Manual)
        if (!isManualControl && followMovementDirection && targetRb != null)
        {
            Vector3 velocity = targetRb.linearVelocity;
            velocity.y = 0;
            if (velocity.magnitude > 0.1f)
            {
                lastMovementDir = velocity.normalized;
                targetYaw = Quaternion.LookRotation(lastMovementDir).eulerAngles.y;
            }
        }

        // 3. Apply Rotation Smoothing
        currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, rotationSmoothTime);
        currentPitch = Mathf.SmoothDampAngle(currentPitch, targetPitch, ref pitchVelocity, rotationSmoothTime);
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);

        // 4. Distance and Collision
        float desiredDistance = targetDistance;
        if (enableCollision)
        {
            desiredDistance = GetCollisionDistance(currentPivotPosition, rotation, targetDistance);
        }
        
        currentDistance = Mathf.SmoothDamp(currentDistance, desiredDistance, ref distanceVelocity, zoomSmoothTime);

        // 5. Final Transforms
        transform.position = currentPivotPosition - (rotation * Vector3.forward * currentDistance);
        transform.rotation = rotation;
    }

    /// <summary>
    /// Instantly snaps the camera to its current target position/rotation, 
    /// bypassing all smoothing. Call this after teleporting the target ball.
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null) return;

        currentPivotPosition = target.position + pivotOffset;
        currentYaw = targetYaw;
        currentPitch = targetPitch;
        currentDistance = targetDistance;
        
        yawVelocity = pitchVelocity = distanceVelocity = 0;
        pivotVelocity = Vector3.zero;

        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        if (enableCollision)
        {
            currentDistance = GetCollisionDistance(currentPivotPosition, rotation, targetDistance);
        }

        transform.position = currentPivotPosition - (rotation * Vector3.forward * currentDistance);
        transform.rotation = rotation;
    }

    private void HandleManualInput()
    {
        bool rightClickState = false;
        if (Mouse.current != null) 
            rightClickState = Mouse.current.rightButton.isPressed;
        else 
            rightClickState = Input.GetMouseButton(1);
            
        if (rightClickState)
        {
            if (!isManualControl)
            {
                isManualControl = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                
                // Sync targets to current values to prevent snap on start
                targetYaw = currentYaw;
                targetPitch = currentPitch;
            }

            Vector2 delta = Vector2.zero;
            if (Mouse.current != null)
                delta = Mouse.current.delta.ReadValue() * 0.1f;
            else
                delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
            
            if (delta.sqrMagnitude > 0)
            {
                targetYaw += delta.x * manualRotationSpeed;
                targetPitch -= delta.y * manualRotationSpeed;
                targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
            }
        }
        else
        {
            if (isManualControl)
            {
                isManualControl = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    private float GetCollisionDistance(Vector3 pivot, Quaternion rotation, float maxDistance)
    {
        Vector3 direction = rotation * -Vector3.forward;
        RaycastHit hit;
        
        // Start ray slightly in front of pivot to avoid hitting target itself
        Vector3 rayStart = pivot + direction * 0.5f;
        float castDist = maxDistance - 0.5f;

        if (Physics.SphereCast(rayStart, cameraRadius, direction, out hit, castDist, collisionLayer))
        {
            return Mathf.Max(0.1f, hit.distance + 0.5f - collisionBuffer);
        }

        return maxDistance;
    }
}
