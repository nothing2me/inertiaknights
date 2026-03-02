using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform target;        // The ball to follow
    public Vector3 offset = new Vector3(0, 5, -7); // Distance from the ball
    public float smoothTime = 0.15f; // Time it takes to reach the target

    [Header("Rotation Settings")]
    [Header("Rotation Settings")]
    public bool lockRotation = false;
    public bool followMovementDirection = true;
    public float rotationSpeed = 5f;
    public Vector3 fixedAngle = new Vector3(30, 0, 0); // Desired camera angle if locked

    [Header("Isometric Settings")]
    public bool useIsometricView = false;
    public Vector3 isometricOffset = new Vector3(10, 10, -10);
    public Vector3 isometricRotation = new Vector3(35, -45, 0);

    private Vector3 currentVelocity = Vector3.zero;
    private Rigidbody targetRb;
    private Vector3 lastMovementDir = Vector3.forward;

    void Start()
    {
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
        }
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

    void FixedUpdate() 
    {
        if (target == null) return;

        Vector3 desiredPosition;
        Quaternion desiredRotation;

        if (useIsometricView)
        {
            // Isometric View logic
            desiredPosition = target.position + isometricOffset;
            desiredRotation = Quaternion.Euler(isometricRotation);

            // Smooth position, snappy rotation (or also smooth if preferred)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSpeed * Time.fixedDeltaTime);
        }
        else if (followMovementDirection && targetRb != null)
        {
            // Calculate movement direction (ignoring Y for horizontal orientation)
            Vector3 velocity = targetRb.linearVelocity;
            velocity.y = 0;

            if (velocity.magnitude > 0.2f)
            {
                lastMovementDir = velocity.normalized;
            }

            // Calculate rotation that looks in the movement direction
            Quaternion movementRotation = Quaternion.LookRotation(lastMovementDir);
            
            // Calculate desired position by rotating the offset
            Vector3 rotatedOffset = movementRotation * offset;
            desiredPosition = target.position + rotatedOffset;

            // Camera should look at the ball (target)
            Vector3 lookDirection = target.position - transform.position;
            if (lookDirection != Vector3.zero)
            {
                desiredRotation = Quaternion.LookRotation(lookDirection);
            }
            else
            {
                desiredRotation = transform.rotation;
            }

            // Apply smooth position and rotation
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSpeed * Time.fixedDeltaTime);
        }
        else
        {
            // Fallback to original follow behavior
            desiredPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);
            
            if (lockRotation)
            {
                transform.rotation = Quaternion.Euler(fixedAngle);
            }
        }
    }

    // Remove LateUpdate brutally locking rotation since we might want dynamic rotation
    // void LateUpdate() { ... }
}
