using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// Tank class abilities:
///   Q = Shield Bubble — 7-8 second protective sphere around the Tank for the team.
///   R = Heavy Slam — devastating ground pound that kills light enemies instantly
///       and knocks bosses back hard.
///
/// Passive: two health bars (shield + normal). Shield depletes first.
///          Wall bounces are more punishing (velocity penalty on collision).
/// </summary>
public class TankAbility : MonoBehaviour
{
    [Header("Shield Bubble")]
    public float bubbleRadius = 8f;
    public float bubbleDuration = 7.5f;
    public float bubbleCooldown = 30f;
    public Color bubbleColor = new Color(1f, 0.6f, 0.2f, 0.15f);

    [Header("Heavy Slam")]
    public float heavySlamForce = 50f;
    public float slamRadius = 10f;
    public float slamKnockback = 25f;
    public float slamCooldown = 6f;
    public float lightEnemyKillThreshold = 100f; // enemies with <= this max HP die instantly

    // ─── State ────────────────────────────────────────────────────
    private BallController ball;
    private Rigidbody rb;

    private float lastBubbleTime = -100f;
    private float lastSlamTime = -100f;

    private CameraController cam;
    private float originalCamDistance = -1f;

    // Bubble visual
    private GameObject bubbleVisual;
    private float bubbleTimer = 0f;
    private bool bubbleActive = false;

    // ─── Input ────────────────────────────────────────────────────
    private InputAction bubbleAction;
    private InputAction slamAction;

    void OnEnable()
    {
        ball = GetComponent<BallController>();
        rb = GetComponent<Rigidbody>();

        bubbleAction = new InputAction("Bubble", InputActionType.Button, "<Keyboard>/q");
        slamAction   = new InputAction("HeavySlam", InputActionType.Button, "<Keyboard>/r");

        cam = ball.CutsceneCameraRef;

        bubbleAction.Enable();
        slamAction.Enable();
    }

    void OnDisable()
    {
        bubbleAction?.Disable();
        slamAction?.Disable();
        DestroyBubble();
    }

    void Update()
    {
        if (ball == null || !ball.IsOwner) return;

        // ── Shield Bubble (Q) ──
        if (bubbleAction.WasPressedThisFrame() && !bubbleActive &&
            Time.time >= lastBubbleTime + bubbleCooldown)
        {
            ActivateBubble();
        }

        // Update bubble
        if (bubbleActive)
        {
            bubbleTimer -= Time.deltaTime;
            if (bubbleTimer <= 0f)
            {
                DeactivateBubble();
            }
            else
            {
                UpdateBubble();
            }
        }

        // ── Heavy Slam (R — must be airborne) ──
        bool isGrounded = Physics.Raycast(transform.position, Vector3.down,
            ball.groundCheckDistance, ball.groundLayer);

        if (slamAction.WasPressedThisFrame() && !isGrounded &&
            Time.time >= lastSlamTime + slamCooldown)
        {
            PerformHeavySlam();
        }
    }

    // ─── Shield Bubble ────────────────────────────────────────────

    private void ActivateBubble()
    {
        bubbleActive = true;
        lastBubbleTime = Time.time;
        bubbleTimer = bubbleDuration;

        // Tell server to create bubble
        ball.ActivateBubbleServerRpc(bubbleDuration);

        // Create local visual
        CreateBubbleVisual();

        if (cam != null && originalCamDistance < 0)
        {
            originalCamDistance = cam.targetDistance;
            cam.targetDistance += 5f;
        }

        Debug.Log("[Tank] Shield Bubble activated!");
    }

    private void CreateBubbleVisual()
    {
        if (bubbleVisual != null) Destroy(bubbleVisual);

        bubbleVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bubbleVisual.name = "TankBubble";
        bubbleVisual.transform.SetParent(transform);
        bubbleVisual.transform.localPosition = Vector3.zero;
        bubbleVisual.transform.localScale = Vector3.one * bubbleRadius * 2f;

        // Remove collider — the bubble protection is handled via immunity check
        Collider col = bubbleVisual.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Semi-transparent material
        Renderer rend = bubbleVisual.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.color = bubbleColor;
            rend.material = mat;
        }
    }

    private void UpdateBubble()
    {
        if (bubbleVisual == null) return;

        // Pulse effect
        float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.03f;
        bubbleVisual.transform.localScale = Vector3.one * bubbleRadius * 2f * pulse;

        // Protect allies inside the bubble (make them immune to damage)
        Collider[] hits = Physics.OverlapSphere(transform.position, bubbleRadius);
        foreach (var hit in hits)
        {
            BallController ally = hit.GetComponent<BallController>();
            if (ally != null && ally.IsSpawned)
            {
                // The actual immunity is checked in BallController.TakeDamage
                // via the bubble zone. We flag it here each frame.
                ally.insideTankBubble = true;
            }
        }
    }

    private void DeactivateBubble()
    {
        bubbleActive = false;
        DestroyBubble();

        if (cam != null && originalCamDistance > 0)
        {
            cam.targetDistance = originalCamDistance;
            originalCamDistance = -1f;
        }

        Debug.Log("[Tank] Shield Bubble expired.");
    }

    private void DestroyBubble()
    {
        if (bubbleVisual != null)
        {
            Destroy(bubbleVisual);
            bubbleVisual = null;
        }
    }

    // ─── Heavy Slam ───────────────────────────────────────────────

    private void PerformHeavySlam()
    {
        lastSlamTime = Time.time;

        // Massive downward force
        rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.3f, 0f, rb.linearVelocity.z * 0.3f);
        rb.AddForce(Vector3.down * heavySlamForce, ForceMode.Impulse);

        // Tell server to handle damage on landing
        ball.TankHeavySlamServerRpc(slamRadius, slamKnockback, lightEnemyKillThreshold);

        Debug.Log("[Tank] HEAVY SLAM!");
    }

    // ─── Wall Bounce Penalty (called from BallController) ─────────

    /// <summary>
    /// Called by BallController on collision with static geometry.
    /// Reduces velocity by the wallBouncePenalty factor.
    /// </summary>
    public void ApplyWallBouncePenalty()
    {
        if (ball == null) return;

        float penalty = ball.wallBouncePenalty;
        if (penalty <= 0f) return;

        Vector3 vel = rb.linearVelocity;
        vel.x *= (1f - penalty);
        vel.z *= (1f - penalty);
        rb.linearVelocity = vel;
    }

    void OnDestroy()
    {
        DestroyBubble();
    }
}
