using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Attach this to a Quad child of an NPC.
/// Billboards the quad towards the camera and plays 4-directional animations
/// based on the NPC's actual 3D rotation (or velocity) relative to the Camera's view.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class NPCDirectionalBillboard : MonoBehaviour
{
    [Header("Spritesheet")]
    public Texture2D spritesheet;
    public int cols = 4;
    public int rows = 3;

    [Header("Animation")]
    public float fps = 8f;
    public float moveThreshold = 0.1f;

    [Header("Size & Position")]
    public float spriteWidth = 1.5f;
    public float spriteHeight = 1.5f;
    public Vector3 pivotOffset = new Vector3(0f, 0.75f, 0f);

    [Header("Direction Logic")]
    [Tooltip("If true, determines facing direction based on movement velocity (like a rolling ball). If false, uses the NPC's Z-axis rotation (transform.forward).")]
    public bool useVelocityForDirection = false;

    private enum AnimState
    {
        IdleReverse = 0,
        IdleForward = 1,
        IdleRight = 2,
        IdleLeft = 3,
        WalkReverse = 4,
        WalkForward = 5,
        WalkRight = 6,
        WalkLeft = 7
    }

    private enum LastDir { Reverse, Forward, Right, Left }

    // Matches the Player's spritesheet layout exactly
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
    };

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
    };

    private Material mat;
    private Camera cam;
    private Transform rootTransform;
    private Rigidbody rb;
    private NavMeshAgent agent;
    private Mesh mesh;
    private Vector2[] uvs = new Vector2[4];

    private AnimState curState = AnimState.IdleReverse;
    private LastDir lastDir = LastDir.Reverse;
    private int frameIdx = 0;
    private float frameTimer = 0f;

    void Awake()
    {
        mat = new Material(Shader.Find("Sprites/Default"));
        if (spritesheet != null)
        {
            mat.mainTexture = spritesheet;
        }
        GetComponent<MeshRenderer>().material = mat;
        mesh = GetComponent<MeshFilter>().mesh;
    }

    void Start()
    {
        rootTransform = transform.parent != null ? transform.parent : transform;
        rb = rootTransform.GetComponent<Rigidbody>();
        agent = rootTransform.GetComponent<NavMeshAgent>();

        // Unparent so we don't inherit rotation if the base object rotates/spins
        transform.SetParent(null);
        transform.localScale = new Vector3(spriteWidth, spriteHeight, 1f);
    }

    void LateUpdate()
    {
        if (rootTransform != null)
            transform.position = rootTransform.position + pivotOffset;

        if (cam == null || !cam.isActiveAndEnabled)
            cam = Camera.main;

        UpdateState();
        TickAnim();
        ApplyFrame();
        Billboard();
    }

    void UpdateState()
    {
        if (rootTransform == null || cam == null) return;

        // 1. Determine speed for Walk vs Idle
        float speed = 0f;
        Vector3 velocity = Vector3.zero;

        if (rb != null)
        {
            velocity = rb.linearVelocity;
            speed = new Vector2(velocity.x, velocity.z).magnitude;
        }
        else if (agent != null)
        {
            velocity = agent.velocity;
            speed = new Vector2(velocity.x, velocity.z).magnitude;
        }
        else
        {
            // Calculate manual velocity if no physics/agent
        }

        bool isWalking = speed >= moveThreshold;

        // 2. Determine facing direction vector
        Vector3 facingDir = Vector3.forward;
        if (useVelocityForDirection && speed > 0.01f)
        {
            facingDir = velocity.normalized;
        }
        else
        {
            facingDir = rootTransform.forward;
        }

        facingDir.y = 0;
        facingDir.Normalize();

        // 3. Calculate direction relative to Camera
        Vector3 camFwd = cam.transform.forward;
        Vector3 camRight = cam.transform.right;
        camFwd.y = 0; camFwd.Normalize();
        camRight.y = 0; camRight.Normalize();

        float fwdDot = Vector3.Dot(facingDir, camFwd);
        float rgtDot = Vector3.Dot(facingDir, camRight);

        // If the dot product to right axis is greater than forward/back, we see the side.
        if (Mathf.Abs(rgtDot) >= Mathf.Abs(fwdDot))
        {
            if (rgtDot > 0) lastDir = LastDir.Right; // NPC facing right relative to cam
            else lastDir = LastDir.Left;             // NPC facing left relative to cam
        }
        else
        {
            // fwdDot > 0 means NPC is facing SAME direction as camera (We see its BACK)
            // fwdDot < 0 means NPC is facing OPPOSITE direction as camera (We see its FRONT)
            if (fwdDot > 0) lastDir = LastDir.Reverse;
            else lastDir = LastDir.Forward;
        }

        // Apply state
        AnimState nextState = AnimState.IdleReverse;
        if (isWalking)
        {
            if (lastDir == LastDir.Reverse) nextState = AnimState.WalkReverse;
            else if (lastDir == LastDir.Forward) nextState = AnimState.WalkForward;
            else if (lastDir == LastDir.Right) nextState = AnimState.WalkRight;
            else if (lastDir == LastDir.Left) nextState = AnimState.WalkLeft;
        }
        else
        {
            if (lastDir == LastDir.Reverse) nextState = AnimState.IdleReverse;
            else if (lastDir == LastDir.Forward) nextState = AnimState.IdleForward;
            else if (lastDir == LastDir.Right) nextState = AnimState.IdleRight;
            else if (lastDir == LastDir.Left) nextState = AnimState.IdleLeft;
        }

        SetState(nextState);
    }

    void SetState(AnimState next)
    {
        if (next == curState) return;
        curState = next;
        frameIdx = 0;
        frameTimer = 0f;
    }

    void TickAnim()
    {
        var anim = ANIMS[(int)curState];
        if (anim.frames.Length <= 1) return;

        frameTimer += Time.deltaTime;
        if (frameTimer >= 1f / fps)
        {
            frameTimer -= 1f / fps;
            frameIdx = (frameIdx + 1) % anim.frames.Length;
        }
    }

    void ApplyFrame()
    {
        var anim = ANIMS[(int)curState];
        int fid = anim.frames[frameIdx % anim.frames.Length];
        var cell = FRAME_CELLS[fid];

        int uvRow = (rows - 1) - cell.row;
        float scaleX = 1f / cols;
        float scaleY = 1f / rows;
        float offX = cell.col * scaleX;
        float offY = uvRow * scaleY;
        
        uvs[0] = new Vector2(offX, offY);                   // Bottom Left
        uvs[1] = new Vector2(offX + scaleX, offY);          // Bottom Right
        uvs[2] = new Vector2(offX, offY + scaleY);          // Top Left
        uvs[3] = new Vector2(offX + scaleX, offY + scaleY); // Top Right
        mesh.uv = uvs;

        Vector3 s = transform.localScale;
        s.x = anim.mirror ? -spriteWidth : spriteWidth;
        transform.localScale = s;
    }

    void Billboard()
    {
        if (cam == null) return;
        transform.rotation = cam.transform.rotation;
    }
}
