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
    public enum BillboardMode { Player, Enemy, Boss }

    [Header("Mode")]
    [Tooltip("Players switch sprites based on class. Enemies/Bosses strictly use the assigned Spritesheet.")]
    public BillboardMode mode = BillboardMode.Player;

    // ── Spritesheet ──────────────────────────────────────────────
    [Header("Spritesheet")]
    public Texture2D spritesheet;
    public int cols = 4;
    public int rows = 3;

    [Header("Class Specific Textures")]
    public Texture2D lightClassSpritesheet;
    public Texture2D tankClassSpritesheet;
    public Texture2D healerClassSpritesheet;
    public Texture2D defaultFallingSprite;
    
    private bool isDefaultWisp = false;

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
    private Mesh       mesh;
    private Vector2[]  uvs = new Vector2[4];

    private AnimState  curState   = AnimState.IdleReverse;
    private LastDir    lastDir    = LastDir.Reverse;
    private int        frameIdx   = 0;
    private float      frameTimer = 0f;
    private bool       isGoal     = false;
    private float      goalTimer  = 0f;

    private int        currentClassIndex = -1; // Tracks the currently applied class sprite

    // ─────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        // Instantly preview scale and offset changes in the Prefab Editor!
        transform.localScale = new Vector3(spriteWidth, spriteHeight, 1f);
        
        // If not playing, position it relative to its parent so you can preview the pivot offset
        if (!Application.isPlaying)
        {
            transform.localPosition = pivotOffset;
        }
    }
#endif

    void Awake()
    {
        // Build a dedicated material so we don't mutate any shared asset
        mat = new Material(Shader.Find("Sprites/Default"));
        if (spritesheet != null) mat.mainTexture = spritesheet;
        GetComponent<MeshRenderer>().material = mat;

        // Get instance of the mesh so we can modify UVs directly (Sprites/Default ignores mat.mainTextureScale/Offset)
        mesh = GetComponent<MeshFilter>().mesh;
    }

    void Start()
    {
        // Auto-find references
        if (ballController == null)
            ballController = GetComponentInParent<BallController>();
        
        // Grab the Rigidbody generically so it works for Enemies and Bosses too!
        rb = GetComponentInParent<Rigidbody>();

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
        {
            transform.position = followTarget.position + pivotOffset;
        }
        else
        {
            // The target we were following was destroyed (e.g., enemy/boss died).
            // Since we unparented ourselves in Start(), we must manually clean ourselves up!
            Destroy(gameObject);
            return;
        }

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

    /// <summary>
    /// Updates the billboarding texture based on the player's selected class.
    /// Matches PlayerClassType enum: 1 = Light, 2 = Healer, 3 = Tank
    /// </summary>
    public void SetClassSprite(int classIndex)
    {
        Texture2D newTex = spritesheet; // Default fallback
        isDefaultWisp = false;
        
        if (classIndex <= 0 && defaultFallingSprite != null) 
        {
            newTex = defaultFallingSprite;
            isDefaultWisp = true;
        }
        else if (classIndex == 1 && lightClassSpritesheet != null) newTex = lightClassSpritesheet;
        else if (classIndex == 2 && healerClassSpritesheet != null) newTex = healerClassSpritesheet;
        else if (classIndex == 3 && tankClassSpritesheet != null) newTex = tankClassSpritesheet;

        if (mat != null && newTex != null)
        {
            mat.mainTexture = newTex;
        }
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
        // --- Networked Class Sprite Routing ---
        if (mode == BillboardMode.Player && ballController != null)
        {
            int netClass = ballController.playerClass.Value;
            if (netClass != currentClassIndex)
            {
                currentClassIndex = netClass;
                SetClassSprite(currentClassIndex);
            }
        }

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
        // Must use followTarget since transform.parent was set to null in Start()
        Vector3 origin = followTarget != null ? followTarget.position : transform.position;
        LayerMask mask = ballController != null ? ballController.groundLayer : ~0; // Default to 'Everything' LayerMask
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
        if (isDefaultWisp)
        {
            int fid_wisp = Mathf.FloorToInt(Time.time * fps) % 2;
            float scaleX_wisp = 1f / 2f;
            float scaleY_wisp = 1f;
            float offX_wisp = fid_wisp * scaleX_wisp;
            float offY_wisp = 0f;
            
            uvs[0] = new Vector2(offX_wisp, offY_wisp);
            uvs[1] = new Vector2(offX_wisp + scaleX_wisp, offY_wisp);
            uvs[2] = new Vector2(offX_wisp, offY_wisp + scaleY_wisp);
            uvs[3] = new Vector2(offX_wisp + scaleX_wisp, offY_wisp + scaleY_wisp);
            mesh.uv = uvs;
            
            Vector3 wispScale = transform.localScale;
            wispScale.x = spriteWidth;
            transform.localScale = wispScale;
            return;
        }

        var anim   = ANIMS[(int)curState];
        int fid    = anim.frames[frameIdx % anim.frames.Length];
        var cell   = FRAME_CELLS[fid];

        // Unity UVs start bottom-left; spritesheet row 0 is at the TOP → flip row
        int uvRow  = (rows - 1) - cell.row;
        float scaleX = 1f / cols;
        float scaleY = 1f / rows;
        float offX = cell.col * scaleX;
        float offY = uvRow    * scaleY;
        
        // Sprites/Default ignores material offset/scale, so we slice by modifying the mesh UVs directly
        uvs[0] = new Vector2(offX, offY);                   // Bottom Left
        uvs[1] = new Vector2(offX + scaleX, offY);          // Bottom Right
        uvs[2] = new Vector2(offX, offY + scaleY);          // Top Left
        uvs[3] = new Vector2(offX + scaleX, offY + scaleY); // Top Right
        mesh.uv = uvs;

        // Mirror by flipping local X scale (left-walk variants)
        Vector3 s = transform.localScale;
        s.x = anim.mirror ? -spriteWidth : spriteWidth;
        transform.localScale = s;
    }

    void Billboard()
    {
        if (cam == null) return;

        // Spherical billboard: Face the camera directly on all axes (left/right and up/down)
        // Matching the camera's exact rotation prevents skewing at the edges of the screen
        transform.rotation = cam.transform.rotation;
    }
}
