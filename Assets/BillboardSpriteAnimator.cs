using UnityEngine;

/// <summary>
/// Attach to a child Quad on the Ball prefab.
/// Billboards a spritesheet character toward the camera, playing
/// directional walk/idle/falling/goal animations based on physics velocity.
///
/// Spritesheet layout (4 cols x 3 rows, matches sprite-tester.html):
///   Row 0: [ 0: reverse idle | 1: reverse walk 1 | 2: reverse walk 2 | 3: forward idle ]
///   Row 1: [ 4: forward walk1 | 5: forward walk2  | 6: side idle       | 7: side walk 1  ]
///   Row 2: [ 8: side walk 2   | 9: falling 1      | 10: falling 2      | 11: goal/celebrate ]
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class BillboardSpriteAnimator : MonoBehaviour
{
    // ── Spritesheet ──────────────────────────────────────────────
    [Header("Spritesheet")]
    public Texture2D spritesheet;
    public int cols = 4;
    public int rows = 3;

    // ── Animation ────────────────────────────────────────────────
    [Header("Animation")]
    public float fps = 8f;
    [Tooltip("Horizontal speed (m/s) below which the character is considered idle.")]
    public float moveThreshold = 1.5f;
    [Tooltip("Vertical velocity below which falling animation triggers (must be airborne).")]
    public float fallVelocityThreshold = -2f;
    [Tooltip("Seconds the goal pose stays visible after TriggerGoal() is called.")]
    public float goalDuration = 1.2f;

    // ── Size ─────────────────────────────────────────────────────
    [Header("Size & Position")]
    public float spriteWidth  = 1.5f;
    public float spriteHeight = 1.5f;
    [Tooltip("Offset the sprite so the feet sit at the ball centre. Raise Y by ~half the sprite height.")]
    public Vector3 pivotOffset = new Vector3(0f, 0.75f, 0f);

    // ── References ───────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Auto-found on parent if left empty.")]
    public BallController ballController;
    [Tooltip("Hide the sphere mesh and show only the sprite.")]
    public bool hideBallMesh = false;

    // ─────────────────────────────────────────────────────────────
    // Private state
    // ─────────────────────────────────────────────────────────────

    private enum AnimState
    {
        IdleReverse = 0,
        IdleForward = 1,
        IdleRight   = 2,
        IdleLeft    = 3,
        WalkReverse = 4,
        WalkForward = 5,
        WalkRight   = 6,
        WalkLeft    = 7,
        Falling     = 8,
        Goal        = 9,
    }

    private enum LastDir { Reverse, Forward, Right, Left }

    // (col, row) in the spritesheet for each frame id 0-11
    private static readonly (int col, int row)[] FRAME_CELLS =
    {
        (0, 0),  // 0  reverse idle
        (1, 0),  // 1  reverse walk 1
        (2, 0),  // 2  reverse walk 2
        (3, 0),  // 3  forward idle
        (0, 1),  // 4  forward walk 1
        (1, 1),  // 5  forward walk 2
        (2, 1),  // 6  side (right) idle
        (3, 1),  // 7  side walk 1
        (0, 2),  // 8  side walk 2
        (1, 2),  // 9  falling 1
        (2, 2),  // 10 falling 2 / hit
        (3, 2),  // 11 goal / celebrate
    };

    // Each AnimState entry: (frameIds, mirrored)
    // Index must match AnimState enum values
    private static readonly (int[] frames, bool mirror)[] ANIMS =
    {
        (new[] { 0 },     false),  // IdleReverse
        (new[] { 3 },     false),  // IdleForward
        (new[] { 6 },     false),  // IdleRight
        (new[] { 6 },     true),   // IdleLeft  (mirror of right)
        (new[] { 1, 2 },  false),  // WalkReverse
        (new[] { 4, 5 },  false),  // WalkForward
        (new[] { 7, 8 },  false),  // WalkRight
        (new[] { 7, 8 },  true),   // WalkLeft  (mirror of right)
        (new[] { 9, 10 }, false),  // Falling
        (new[] { 11 },    false),  // Goal
    };

    private Rigidbody  rb;
    private Material   mat;
    private Camera     cam;
    private Transform  followTarget;  // the ball transform we trail (unparented at runtime)

    private AnimState  curState   = AnimState.IdleReverse;
    private LastDir    lastDir    = LastDir.Reverse;
    private int        frameIdx   = 0;
    private float      frameTimer = 0f;
    private bool       isGoal     = false;
    private float      goalTimer  = 0f;

    // ─────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────

    void Awake()
    {
        // Build a dedicated material so we don't mutate any shared asset
        mat = new Material(Shader.Find("Unlit/Transparent"));
        if (spritesheet != null) mat.mainTexture = spritesheet;
        GetComponent<MeshRenderer>().material = mat;

        // Tile size is constant — set once here
        mat.mainTextureScale = new Vector2(1f / cols, 1f / rows);
    }

    void Start()
    {
        // Auto-find references
        if (ballController == null)
            ballController = GetComponentInParent<BallController>();
        if (ballController != null)
            rb = ballController.GetComponent<Rigidbody>();

        // Remember the parent ball transform, then UNPARENT so we don't
        // inherit the Rigidbody's spin. We'll manually follow it in LateUpdate.
        followTarget = transform.parent;
        transform.SetParent(null);

        // Initial size (position is set every LateUpdate)
        transform.localScale = new Vector3(spriteWidth, spriteHeight, 1f);

        // Optionally hide the ball's sphere mesh
        if (hideBallMesh)
        {
            // The MeshRenderer on the ball itself (ballController's GameObject)
            var ballRend = ballController != null
                ? ballController.GetComponent<MeshRenderer>()
                : followTarget != null ? followTarget.GetComponent<MeshRenderer>() : null;
            if (ballRend != null) ballRend.enabled = false;
        }
    }

    void LateUpdate()
    {
        // Stick to the ball every frame without inheriting its rotation
        if (followTarget != null)
            transform.position = followTarget.position + pivotOffset;

        RefreshCamera();
        UpdateState();
        TickAnim();
        ApplyFrame();
        Billboard();
    }

    // ─────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this from BallController (or ScoreCounter) when the player hits a goal.
    /// </summary>
    public void TriggerGoal()
    {
        isGoal    = true;
        goalTimer = goalDuration;
    }

    // ─────────────────────────────────────────────────────────────
    // Internal helpers
    // ─────────────────────────────────────────────────────────────

    void RefreshCamera()
    {
        if (cam == null || !cam.isActiveAndEnabled)
            cam = Camera.main;
    }

    void UpdateState()
    {
        // --- Goal override (timed) ---
        if (isGoal)
        {
            goalTimer -= Time.deltaTime;
            if (goalTimer <= 0f) isGoal = false;
            SetState(AnimState.Goal);
            return;
        }

        if (rb == null) return;

        Vector3 vel      = rb.linearVelocity;
        float   horzSpeed = new Vector2(vel.x, vel.z).magnitude;

        // --- Falling ---
        if (vel.y < fallVelocityThreshold && !IsGrounded())
        {
            SetState(AnimState.Falling);
            return;
        }

        // --- Idle ---
        if (horzSpeed < moveThreshold)
        {
            SetState(IdleForDir(lastDir));
            return;
        }

        // --- Walking: determine camera-relative dominant axis ---
        Vector3 camFwd   = cam != null ? cam.transform.forward : Vector3.forward;
        Vector3 camRight = cam != null ? cam.transform.right   : Vector3.right;
        camFwd.y   = 0; camFwd.Normalize();
        camRight.y = 0; camRight.Normalize();

        Vector3 velDir  = vel.normalized;
        float   fwdDot  = Vector3.Dot(velDir, camFwd);
        float   rgtDot  = Vector3.Dot(velDir, camRight);

        if (Mathf.Abs(rgtDot) >= Mathf.Abs(fwdDot))
        {
            // Dominant: strafe
            if (rgtDot > 0) { lastDir = LastDir.Right; SetState(AnimState.WalkRight); }
            else             { lastDir = LastDir.Left;  SetState(AnimState.WalkLeft);  }
        }
        else
        {
            // Dominant: forward / backward
            // fwdDot > 0 = moving same dir as camera = moving away = back of character (Reverse)
            // fwdDot < 0 = moving toward camera = front of character (Forward)
            if (fwdDot > 0) { lastDir = LastDir.Reverse; SetState(AnimState.WalkReverse); }
            else             { lastDir = LastDir.Forward; SetState(AnimState.WalkForward); }
        }
    }

    bool IsGrounded()
    {
        float   dist  = ballController != null ? ballController.groundCheckDistance : 0.55f;
        Vector3 origin = ballController != null ? ballController.transform.position : transform.parent.position;
        LayerMask mask = ballController != null ? ballController.groundLayer : Physics.DefaultRaycastLayers;
        return Physics.Raycast(origin, Vector3.down, dist, mask);
    }

    static AnimState IdleForDir(LastDir d)
    {
        switch (d)
        {
            case LastDir.Forward: return AnimState.IdleForward;
            case LastDir.Right:   return AnimState.IdleRight;
            case LastDir.Left:    return AnimState.IdleLeft;
            default:              return AnimState.IdleReverse;
        }
    }

    void SetState(AnimState next)
    {
        if (next == curState) return;
        curState   = next;
        frameIdx   = 0;
        frameTimer = 0f;
    }

    void TickAnim()
    {
        var anim = ANIMS[(int)curState];
        if (anim.frames.Length <= 1) return;  // static, nothing to cycle

        frameTimer += Time.deltaTime;
        if (frameTimer >= 1f / fps)
        {
            frameTimer -= 1f / fps;
            frameIdx = (frameIdx + 1) % anim.frames.Length;
        }
    }

    void ApplyFrame()
    {
        var anim   = ANIMS[(int)curState];
        int fid    = anim.frames[frameIdx % anim.frames.Length];
        var cell   = FRAME_CELLS[fid];

        // Unity UVs start bottom-left; spritesheet row 0 is at the TOP → flip row
        int uvRow  = (rows - 1) - cell.row;
        float offX = cell.col * (1f / cols);
        float offY = uvRow    * (1f / rows);
        mat.mainTextureOffset = new Vector2(offX, offY);

        // Mirror by flipping local X scale (left-walk variants)
        Vector3 s = transform.localScale;
        s.x = anim.mirror ? -spriteWidth : spriteWidth;
        transform.localScale = s;
    }

    void Billboard()
    {
        if (cam == null) return;

        // Cylindrical billboard: face camera on Y axis only (stays upright)
        Vector3 dir = transform.position - cam.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }
}
