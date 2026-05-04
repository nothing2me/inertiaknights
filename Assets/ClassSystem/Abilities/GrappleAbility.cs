using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Light class ability: grapple hook, rescue dash, and critical strike system.
/// Requires BallController on the same GameObject.
/// </summary>
public class GrappleAbility : MonoBehaviour
{
    [Header("Grapple Settings")]
    public float grappleRange = 50f;
    public float grapplePullForce = 35f;
    public float grappleCooldown = 3f;
    public LayerMask grappleLayer = ~0; // What can be grappled
    public Color ropeColor = new Color(0.4f, 0.85f, 1f, 0.8f);

    [Header("Rescue Dash")]
    public float rescueDashForce = 18f;
    public float rescueDashCooldown = 5f;
    public float rescueDashMinFallSpeed = 3f; // Must be falling this fast to use

    // ─── State ────────────────────────────────────────────────────
    private BallController ball;
    private Rigidbody rb;
    private CameraController cam;
    private LineRenderer ropeLine;

    private bool isGrappling = false;
    private Vector3 grapplePoint;
    private float lastGrappleTime = -100f;
    private float lastRescueDashTime = -100f;
    private bool rescueDashAvailable = true;

    // ─── Input ────────────────────────────────────────────────────
    private InputAction grappleAction;
    private InputAction rescueDashAction;

    void OnEnable()
    {
        ball = GetComponent<BallController>();
        rb = GetComponent<Rigidbody>();
        cam = ball.CutsceneCameraRef;

        // Create line renderer for rope visual
        if (ropeLine == null)
        {
            GameObject ropeGO = new GameObject("GrappleRope");
            ropeGO.transform.SetParent(transform);
            ropeLine = ropeGO.AddComponent<LineRenderer>();
            ropeLine.startWidth = 0.08f;
            ropeLine.endWidth = 0.04f;
            ropeLine.material = new Material(Shader.Find("Sprites/Default"));
            ropeLine.startColor = ropeColor;
            ropeLine.endColor = ropeColor;
            ropeLine.positionCount = 2;
            ropeLine.enabled = false;
        }

        // Set up inputs
        grappleAction = new InputAction("Grapple", InputActionType.Button, "<Keyboard>/q");
        grappleAction.Enable();

        rescueDashAction = new InputAction("RescueDash", InputActionType.Button, "<Keyboard>/r");
        rescueDashAction.Enable();
    }

    void OnDisable()
    {
        grappleAction?.Disable();
        rescueDashAction?.Disable();
        StopGrapple();
    }

    void Update()
    {
        if (ball == null || !ball.IsOwner) return;

        // ── Grapple ──
        if (grappleAction.WasPressedThisFrame() && !isGrappling &&
            Time.time >= lastGrappleTime + grappleCooldown)
        {
            TryStartGrapple();
        }

        if (grappleAction.WasReleasedThisFrame() && isGrappling)
        {
            StopGrapple();
        }

        // ── Rescue Dash (only when falling) ──
        if (rescueDashAction.WasPressedThisFrame() && rescueDashAvailable &&
            rb.linearVelocity.y < -rescueDashMinFallSpeed &&
            Time.time >= lastRescueDashTime + rescueDashCooldown)
        {
            PerformRescueDash();
        }

        // ── Visual update ──
        if (isGrappling)
        {
            UpdateGrappleVisual();
        }

        // Reset rescue dash when grounded
        bool grounded = Physics.Raycast(transform.position, Vector3.down,
            ball.groundCheckDistance, ball.groundLayer);
        if (grounded) rescueDashAvailable = true;
    }

    void FixedUpdate()
    {
        if (ball == null || !ball.IsOwner || !isGrappling) return;

        // Pull toward grapple point
        Vector3 direction = (grapplePoint - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, grapplePoint);

        if (distance < 2f)
        {
            StopGrapple();
            return;
        }

        rb.AddForce(direction * grapplePullForce, ForceMode.Force);
    }

    // ─── Grapple Logic ────────────────────────────────────────────

    private void TryStartGrapple()
    {
        // Raycast from camera forward
        Transform camT = cam != null ? cam.transform : Camera.main?.transform;
        if (camT == null) return;

        Ray ray = new Ray(camT.position, camT.forward);
        if (Physics.SphereCast(ray, 1.5f, out RaycastHit hit, grappleRange, grappleLayer))
        {
            grapplePoint = hit.point;
            isGrappling = true;
            lastGrappleTime = Time.time;

            ropeLine.enabled = true;
            Debug.Log($"[Light] Grapple attached to {hit.collider.name}!");
        }
    }

    private void StopGrapple()
    {
        isGrappling = false;
        if (ropeLine != null) ropeLine.enabled = false;
    }

    private void UpdateGrappleVisual()
    {
        if (ropeLine == null) return;
        ropeLine.SetPosition(0, transform.position);
        ropeLine.SetPosition(1, grapplePoint);
    }

    // ─── Rescue Dash ──────────────────────────────────────────────

    private void PerformRescueDash()
    {
        rescueDashAvailable = false;
        lastRescueDashTime = Time.time;

        // Get movement input direction, or default to camera forward
        Vector2 moveInput = ball.moveAction.ReadValue<Vector2>();
        Vector3 dashDir;

        if (moveInput.magnitude > 0.1f)
        {
            Transform camT = cam != null ? cam.transform : Camera.main?.transform;
            if (camT != null)
            {
                Vector3 fwd = camT.forward; fwd.y = 0; fwd.Normalize();
                Vector3 right = camT.right; right.y = 0; right.Normalize();
                dashDir = (fwd * moveInput.y + right * moveInput.x).normalized;
            }
            else
            {
                dashDir = Vector3.up;
            }
        }
        else
        {
            dashDir = Vector3.up;
        }

        // Add upward bias to counter the fall
        dashDir = (dashDir + Vector3.up * 0.6f).normalized;

        // Kill downward velocity first
        Vector3 vel = rb.linearVelocity;
        vel.y = Mathf.Max(vel.y, 0f);
        rb.linearVelocity = vel;

        rb.AddForce(dashDir * rescueDashForce, ForceMode.Impulse);
        Debug.Log("[Light] Rescue Dash!");
    }

    void OnDestroy()
    {
        if (ropeLine != null) Destroy(ropeLine.gameObject);
    }
}
