using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class BallController : NetworkBehaviour
{
    public float speed = 20f;
    public float jumpForce = 5f;
    public bool jumpOnSpace = true;
    public LayerMask groundLayer; // Layer(s) that are considered ground
    public LayerMask goalLayer; // Layer(s) that are considered goals
    public float groundCheckDistance = 0.55f; // Distance to check for ground below center
    public ScoreCounter scoreCounter; // Reference to the score counter UI
    public int maxHealth = 3;
    public float damageCooldown = 1.5f; // Mercy period after taking damage
    
    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(3, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private float lastDamageTime = -10f; // Initialize to allow immediate damage
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    [Header("Leap of Faith Spawn")]
    public bool spawnInSky = true;
    [HideInInspector] public bool isFallingFromSky = false;

    // ═══ CLASS SYSTEM ═════════════════════════════════════════════
    [Header("Class System")]
    [HideInInspector] public bool movementLocked = false;

    public NetworkVariable<int> playerClass = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Light class — crit
    [HideInInspector] public float critChance = 0f;
    [HideInInspector] public float critDamagePercent = 0.15f;
    [HideInInspector] public bool hasRescueDash = false;

    // Tank class — shield HP + wall penalty
    public NetworkVariable<float> shieldHealth = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [HideInInspector] public float maxShieldHealth = 0f;
    [HideInInspector] public float wallBouncePenalty = 0f;

    // Healer class — uber meter + immortality
    public NetworkVariable<float> uberMeter = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isImmortal = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Tank bubble — set each frame by TankAbility if inside a bubble
    [HideInInspector] public bool insideTankBubble = false;

    private PlayerClassManager classManager;
    /// <summary>Set by LockPlayerStep. Suppresses camera snap on respawn during cutscenes.</summary>
    public bool isCutsceneLocked = false;
    /// <summary>Exposes the camera controller to CutsceneContext without breaking encapsulation.</summary>
    public CameraController CutsceneCameraRef => playerCameraController;
    [Header("Input Actions")]
    [SerializeField] public InputAction moveAction; // Define an input action for movement
    [SerializeField] public InputAction jumpAction; // Define an input action for jumping
    [SerializeField] public InputAction respawnAction; // Define an input action for respawning
    [SerializeField] public InputAction dashAction; // Define an input action for dashing
    [SerializeField] public InputAction groundSlamAction; // Define an input action for ground slam
    [SerializeField] public InputAction attackAction; // Define an input action for attack/lunge
    [SerializeField] public InputAction brakeAction;
    [SerializeField] public InputAction interactAction; // Interact with NPCs, objects, etc.

    [Header("Brake Settings")]
    [Range(0f, 1f)] public float brakeFriction = 0.15f;

    [Header("Dash Settings")]
    public float dashForce = 20f;
    public float dashCooldown = 1f;
    private float lastDashTime = -10f;

    [Header("Ground Slam Settings")]
    public float groundSlamForce = 30f;
    [Range(0f, 1f)] public float slamDampenFactor = 0.2f;
    private bool isSlamming = false;

    [Header("Attack Settings")]
    public float attackRange = 10f;
    public float lungeForce = 40f;
    public float damageMultiplier = 2f;
    public float attackCooldown = 0.5f;

    [Header("Aimbot Assist Settings")]
    [Range(0f, 1f)] public float homingStrength = 0.5f; // 0 = no aimbot, 1 = intense homing
    public float homingDuration = 0.3f; // How long after dash starts the homing remains active

    [Header("Impact Settings")]
    [Range(0f, 1f)] public float playerImpactDampen = 0.1f; // 0.1 = player retains 10% of bounce velocity
    public float enemyKnockbackForce = 2f; 

    private float lastAttackTime = -10f;
    private bool attackTriggered = false;
    private System.Collections.Generic.HashSet<EnemyPlayer> enemiesInRange = new System.Collections.Generic.HashSet<EnemyPlayer>();
    private System.Collections.Generic.HashSet<BossController> bossesInRange = new System.Collections.Generic.HashSet<BossController>();
    private EnemyPlayer currentTarget = null;
    private Transform lungeTarget = null;
    private Transform homingTarget = null;
    
    [Header("Network Data")]
    public NetworkVariable<Unity.Collections.FixedString32Bytes> playerName = new NetworkVariable<Unity.Collections.FixedString32Bytes>("Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private CameraController playerCameraController;
    private Rigidbody rb;
    private BillboardSpriteAnimator spriteAnimator;
    private System.Collections.Generic.Dictionary<Collider, Color> originalColors = new System.Collections.Generic.Dictionary<Collider, Color>();

    void OnEnable()
    {
        // Enable the input actions when the object is enabled
        moveAction.Enable();
        jumpAction.Enable();
        respawnAction.Enable();
        dashAction.Enable();
        groundSlamAction.Enable();
        attackAction.Enable();
        brakeAction.Enable();
        interactAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        respawnAction.Disable();
        dashAction.Disable();
        groundSlamAction.Disable();
        attackAction.Disable();
        brakeAction.Disable();
        interactAction.Disable();
    }

    void Start()
    {
        // Get the Rigidbody component attached to the ball
        rb = GetComponent<Rigidbody>();

        // Cache the billboard sprite animator (lives on a child quad)
        spriteAnimator = GetComponentInChildren<BillboardSpriteAnimator>();

        // Prevent ball from falling through floor at high speeds
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        
        // Fix Jitter: Enforce interpolation for smooth LateUpdate camera tracking
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Initial spawn is now handled in OnNetworkSpawn for better network reliability

        // Save current state for respawns (this will be updated if we want to change spawn points)
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        // Initialize health (Server only)
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
    }

    public override void OnNetworkSpawn()
    {
        bool isLocalHumanPlayer = IsOwner && NetworkObject.IsPlayerObject;

        if (isLocalHumanPlayer)
        {
            scoreCounter = Object.FindFirstObjectByType<ScoreCounter>();
            
            // In a robust setup, the camera is a child of the prefab.
            // Let's find it specifically in our hierarchy while it's still a child.
            playerCameraController = GetComponentInChildren<CameraController>(true);
            
            if (playerCameraController != null)
            {
                // Unparent ONLY the local player's camera to avoid spinning
                if (playerCameraController.transform.parent != null)
                {
                    playerCameraController.transform.SetParent(null);
                }

                playerCameraController.SetTarget(transform);
                playerCameraController.SetMoveAction(moveAction);
                playerCameraController.SetActiveState(true);

                if (playerCameraController.TryGetComponent<Camera>(out Camera cam))
                {
                    cam.tag = "MainCamera";
                }
            }
            else
            {
                playerCameraController = Object.FindFirstObjectByType<CameraController>();
                if (playerCameraController != null)
                {
                    playerCameraController.SetTarget(transform);
                    playerCameraController.SetMoveAction(moveAction);
                    playerCameraController.SetActiveState(true);
                }
            }

            // --- Spawn Logic (Owner Only) ---
            if (PlayerSpawnManager.Instance != null)
            {
                transform.position = PlayerSpawnManager.Instance.GetRandomSpawnPosition();
                // Critical: Sync physics immediately so Netcode doesn't try to interpolate from (0,0,0)
                Physics.SyncTransforms();
                
                // Snap camera to the new spawned position
                if (playerCameraController != null) playerCameraController.SnapToTarget();
                
                // Leap of Faith Setup
                if (spawnInSky)
                {
                    isFallingFromSky = true;
                }
            }

            // Diagnostic: Log current network configuration
            if (NetworkManager.Singleton != null)
            {
                Debug.Log($"[Netcode Diagnostic] Tick Rate: {NetworkManager.Singleton.NetworkConfig.TickRate}hz. Owner Client Id: {OwnerClientId}");
            }

            // Set my name if I have one saved (e.g. from UI)
            SetPlayerNameServerRpc(NetworkManagerUI.LocalPlayerName);

            // ── Initialize Class System ──
            classManager = gameObject.AddComponent<PlayerClassManager>();
            classManager.Initialize(this);
        }
        else
        {
            // Disable input actions if this isn't our local ball
            DisableInputs();

            // Strictly ensure ANY camera components on remote players are disabled
            var remoteCams = GetComponentsInChildren<Camera>(true);
            foreach (var c in remoteCams) c.enabled = false;
            
            var remoteListeners = GetComponentsInChildren<AudioListener>(true);
            foreach (var l in remoteListeners) l.enabled = false;

            var remoteControllers = GetComponentsInChildren<CameraController>(true);
            foreach (var ctrl in remoteControllers) ctrl.enabled = false;

            // ── Initialize Class System for Bots/Remote Players ──
            classManager = gameObject.AddComponent<PlayerClassManager>();
            classManager.Initialize(this);
        }
    }

    [ServerRpc]
    public void SetPlayerNameServerRpc(string name)
    {
        playerName.Value = name;
    }

    private void DisableInputs()
    {
        moveAction.Disable();
        jumpAction.Disable();
        respawnAction.Disable();
        dashAction.Disable();
        groundSlamAction.Disable();
        attackAction.Disable();
        brakeAction.Disable();
        interactAction.Disable();
    }

    void Update()
    {
        if (!IsOwner) return;

        // Catch input in Update since FixedUpdate might miss quick clicks
        if (attackAction.triggered)
        {
            attackTriggered = true;
        }
    }

    // FixedUpdate is used for physics calculations
    private Vector3 GetBotWorldMovement()
    {
        BallController[] allPlayers = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        BallController nearestHuman = null;
        float shortestHumanDist = Mathf.Infinity;

        foreach (var p in allPlayers)
        {
            if (p.NetworkObject.IsPlayerObject)
            {
                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d < shortestHumanDist)
                {
                    shortestHumanDist = d;
                    nearestHuman = p;
                }
            }
        }

        EnemyPlayer nearestEnemy = null;
        float shortestEnemyDist = Mathf.Infinity;
        EnemyPlayer[] allEnemies = Object.FindObjectsByType<EnemyPlayer>(FindObjectsSortMode.None);
        foreach (var e in allEnemies)
        {
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d < shortestEnemyDist)
            {
                shortestEnemyDist = d;
                nearestEnemy = e;
            }
        }

        BossController nearestBoss = null;
        float shortestBossDist = Mathf.Infinity;
        BossController[] allBosses = Object.FindObjectsByType<BossController>(FindObjectsSortMode.None);
        foreach (var b in allBosses)
        {
            float d = Vector3.Distance(transform.position, b.transform.position);
            if (d < shortestBossDist)
            {
                shortestBossDist = d;
                nearestBoss = b;
            }
        }

        // Determine if we should attack an enemy or boss
        Transform attackTarget = null;
        float targetDist = Mathf.Infinity;

        if (nearestEnemy != null && shortestEnemyDist < 20f)
        {
            attackTarget = nearestEnemy.transform;
            targetDist = shortestEnemyDist;
        }
        else if (nearestBoss != null && shortestBossDist < 30f)
        {
            attackTarget = nearestBoss.transform;
            targetDist = shortestBossDist;
        }

        if (attackTarget != null)
        {
            if (targetDist < attackRange)
            {
                attackTriggered = true;
                lungeTarget = attackTarget;
            }
            Vector3 dir = (attackTarget.position - transform.position).normalized;
            dir.y = 0;
            return dir.normalized;
        }

        if (nearestHuman != null && shortestHumanDist > 4f)
        {
            Vector3 dir = (nearestHuman.transform.position - transform.position).normalized;
            dir.y = 0;
            return dir.normalized;
        }

        return Vector3.zero;
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;
        if (movementLocked || isFallingFromSky) return; // Locked until class is chosen or landed

        // Reset bubble flag each physics tick (TankAbility re-sets it if still inside)
        insideTankBubble = false;

        Vector3 movement = Vector3.zero;

        if (!NetworkObject.IsPlayerObject && IsServer)
        {
            movement = GetBotWorldMovement();
        }
        else
        {
            // Read the movement input value
            Vector2 moveInput = moveAction.ReadValue<Vector2>();

            // Get camera forward and right vectors - use playerCameraController if available
            Vector3 camForward = Vector3.forward;
            Vector3 camRight = Vector3.right;

            if (playerCameraController != null)
            {
                camForward = playerCameraController.transform.forward;
                camRight = playerCameraController.transform.right;
            }
            else if (Camera.main != null)
            {
                camForward = Camera.main.transform.forward;
                camRight = Camera.main.transform.right;
            }

            // Project vectors onto the XZ plane (ignore Y) and normalize
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            // Create a movement vector based on input relative to the camera
            movement = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        }

        // Apply force to the ball
        rb.AddForce(movement * speed);

        bool isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);

        if (brakeAction.IsPressed() && isGrounded)
        {
            // Exponential decay: velocity bleeds off fast at high speed, gently near zero.
            // brakeFriction is now a decay rate (units: 1/s). Higher = stops faster.
            // e.g. brakeFriction = 8  →  ~99% speed shed in ~0.6 seconds.
            float decay = Mathf.Exp(-brakeFriction * Time.fixedDeltaTime);
            Vector3 vel = rb.linearVelocity;
            vel.x *= decay;
            vel.z *= decay;
            rb.linearVelocity = vel;
            rb.angularVelocity *= decay;
        }

        // Update HUD stats
        if (scoreCounter != null)
        {
            scoreCounter.UpdateStats(rb.linearVelocity.magnitude, isGrounded, currentHealth.Value);
        }

        // Check for jump input (only if grounded)
        if (jumpOnSpace && jumpAction.IsPressed() && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        // Check for manual respawn
        if (respawnAction.triggered)
        {
            Respawn();
        }

        // Check for dash input
        if (dashAction.triggered && movement.magnitude > 0.1f && Time.time >= lastDashTime + dashCooldown)
        {
            rb.AddForce(movement * dashForce, ForceMode.Impulse);
            lastDashTime = Time.time;
            Debug.Log("Dash!");
        }

        // Check for ground slam input
        if (groundSlamAction.triggered && !isGrounded)
        {
            rb.AddForce(Vector3.down * groundSlamForce, ForceMode.Impulse);
            isSlamming = true;
            Debug.Log("Ground Slam!");
        }

        // Check for attack/lunge input (passed from Update)
        if (attackTriggered)
        {
            attackTriggered = false; // Reset flag
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                PerformLunge();
                lastAttackTime = Time.time;
            }
        }

        if (homingStrength > 0 && homingTarget != null && homingTarget.gameObject.activeSelf)
        {
            float timeSinceDash = Time.time - lastAttackTime;
            if (timeSinceDash < homingDuration)
            {
                Vector3 idealDirection = (homingTarget.position - transform.position).normalized;
                
                // Gradually curve current velocity towards the ideal direction
                Vector3 currentVelocity = rb.linearVelocity;
                float currentSpeed = currentVelocity.magnitude;
                
                // Blend the raw velocity vector towards the target based on homing strength (scaled by fixed tick)
                Vector3 newVelocityDirection = Vector3.Lerp(currentVelocity.normalized, idealDirection, homingStrength * 10f * Time.fixedDeltaTime).normalized;
                
                rb.linearVelocity = newVelocityDirection * currentSpeed;
            }
            else
            {
                homingTarget = null;
            }
        }

        ManageEnemyUI();
        UpdateTargeting();
    }

    private void UpdateTargeting()
    {
        Transform nearest = null;
        float shortestDistance = Mathf.Infinity;

        foreach (var enemy in enemiesInRange)
        {
            if (enemy == null || !enemy.gameObject.activeSelf) continue;
            float d = Vector3.Distance(transform.position, enemy.transform.position);
            if (d < shortestDistance) { shortestDistance = d; nearest = enemy.transform; }
        }

        foreach (var boss in bossesInRange)
        {
            if (boss == null || !boss.gameObject.activeSelf) continue;
            float d = Vector3.Distance(transform.position, boss.transform.position);
            if (d < shortestDistance) { shortestDistance = d; nearest = boss.transform; }
        }

        EnemyPlayer nearestEnemy = nearest != null ? nearest.GetComponent<EnemyPlayer>() : null;

        if (nearestEnemy != currentTarget)
        {
            if (currentTarget != null) currentTarget.SetTargeted(false);
            currentTarget = nearestEnemy;
            if (currentTarget != null) currentTarget.SetTargeted(true);
        }

        lungeTarget = nearest;
    }

    private void ManageEnemyUI()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, attackRange);
        var currentEnemies = new System.Collections.Generic.HashSet<EnemyPlayer>();
        var currentBosses = new System.Collections.Generic.HashSet<BossController>();

        foreach (var col in hitColliders)
        {
            EnemyPlayer enemy = col.GetComponent<EnemyPlayer>();
            if (enemy != null && enemy.gameObject.activeSelf)
                currentEnemies.Add(enemy);

            BossController boss = col.GetComponent<BossController>();
            if (boss != null && boss.gameObject.activeSelf)
                currentBosses.Add(boss);
        }

        foreach (var e in enemiesInRange)
            if (e != null && !currentEnemies.Contains(e)) e.SetUIVisibility(false);
        foreach (var e in currentEnemies)
            if (e != null) e.SetUIVisibility(true);

        foreach (var b in bossesInRange)
            if (b != null && !currentBosses.Contains(b)) b.SetUIVisibility(false);
        foreach (var b in currentBosses)
            if (b != null) b.SetUIVisibility(true);

        enemiesInRange = currentEnemies;
        bossesInRange = currentBosses;
    }

    private void PerformLunge()
    {
        if (lungeTarget == null) return;

        homingTarget = lungeTarget;

        Rigidbody targetRb = lungeTarget.GetComponent<Rigidbody>();
        Vector3 targetPosition = lungeTarget.position;

        if (targetRb != null)
        {
            float distance = Vector3.Distance(transform.position, targetPosition);
            float estimatedTravelTime = distance / lungeForce;
            targetPosition += targetRb.linearVelocity * estimatedTravelTime;
        }

        Vector3 direction = (targetPosition - transform.position).normalized;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.AddForce(direction * lungeForce, ForceMode.Impulse);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;

        // --- Leap of Faith Landing ---
        if (isFallingFromSky)
        {
            HayPile hayPile = other.GetComponent<HayPile>();
            if (hayPile != null)
            {
                isFallingFromSky = false;
                if (hayPile.cushionFall)
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                }
                Debug.Log("Landed safely in a Hay Pile via Trigger! Movement unlocked.");
            }
        }

        bool isInGoalLayer = (((1 << other.gameObject.layer) & goalLayer) != 0);
        Debug.Log($"Entered trigger: {other.gameObject.name} (Layer: {LayerMask.LayerToName(other.gameObject.layer)}, Mask: {goalLayer.value}, Matches: {isInGoalLayer})");

        // Check if the collided object is in the goal layer
        if (isInGoalLayer)
        {
            Debug.Log("Goal match confirmed!");
            
            // Increment score
            if (scoreCounter != null)
            {
                scoreCounter.AddScore(1);
            }
            else
            {
                Debug.LogError("ScoreCounter reference is missing on BallController! Did you drag the ScoreCounter from the hierarchy into the Ball Inspector?");
            }

            // Celebrate!
            spriteAnimator?.TriggerGoal();

            // Visual feedback: Turn goal green and save original color
            MeshRenderer renderer = other.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (!originalColors.ContainsKey(other))
                {
                    originalColors[other] = renderer.material.color;
                }
                renderer.material.color = Color.green;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;

        // Revert color if it was a goal
        if (((1 << other.gameObject.layer) & goalLayer) != 0)
        {
            MeshRenderer renderer = other.GetComponent<MeshRenderer>();
            if (renderer != null && originalColors.ContainsKey(other))
            {
                renderer.material.color = originalColors[other];
                originalColors.Remove(other);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsOwner) return;

        // --- Leap of Faith Landing ---
        if (isFallingFromSky)
        {
            HayPile hayPile = collision.gameObject.GetComponent<HayPile>();
            if (hayPile != null)
            {
                isFallingFromSky = false;
                if (hayPile.cushionFall)
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                }
                Debug.Log("Landed safely in a Hay Pile! Movement unlocked.");
            }
            else if (((1 << collision.gameObject.layer) & groundLayer) != 0)
            {
                // Missed the hay pile and hit the ground!
                isFallingFromSky = false;
                Debug.Log("Missed the hay pile and hit the ground! Movement unlocked anyway.");
            }
        }

        // Dampen bounce if slamming
        if (isSlamming)
        {
            rb.linearVelocity = rb.linearVelocity * slamDampenFactor;
            isSlamming = false;
        }

        // Check for EnemySpike script or Tag
        if (collision.gameObject.GetComponent<EnemySpike>() != null || collision.gameObject.CompareTag("EnemySpike"))
        {
            TakeDamage(1);
        }

        // Handle damage to EnemyPlayer or BossController
        float impactSpeed = collision.relativeVelocity.magnitude;
        float damageAmount = impactSpeed * damageMultiplier;

        // ── Light class: Critical Strike ──
        bool isCrit = false;
        if (critChance > 0f && Random.value < critChance)
            isCrit = true;

        bool hitEnemy = false;

        EnemyPlayer enemy = collision.gameObject.GetComponent<EnemyPlayer>();
        if (enemy != null && damageAmount > 0.1f)
        {
            float finalDmg = damageAmount;
            if (isCrit) finalDmg += enemy.maxHealth * critDamagePercent;

            bool killsEnemy = enemy.currentHealth.Value - finalDmg <= 0f;
            enemy.TakeDamage(finalDmg);
            if (isCrit) Debug.Log($"<color=yellow>★ CRIT!</color> {finalDmg:F0} damage");
            if (killsEnemy)
            {
                spriteAnimator?.TriggerGoal();
                OnEnemyKilled();
            }
            hitEnemy = true;
        }

        BossController boss = collision.gameObject.GetComponent<BossController>();
        if (boss != null && damageAmount > 0.1f)
        {
            float finalDmg = damageAmount;
            if (isCrit) finalDmg += boss.maxHealth * critDamagePercent;

            bool killsBoss = boss.currentHealth.Value - finalDmg <= 0f;
            boss.TakeDamage(finalDmg);
            if (isCrit) Debug.Log($"<color=yellow>★ CRIT!</color> {finalDmg:F0} damage");
            if (killsBoss)
            {
                spriteAnimator?.TriggerGoal();
                OnEnemyKilled();
            }
            hitEnemy = true;
        }

        if (hitEnemy)
        {
            rb.linearVelocity = rb.linearVelocity * playerImpactDampen;
            rb.angularVelocity = rb.angularVelocity * playerImpactDampen;

            Rigidbody enemyRb = collision.gameObject.GetComponent<Rigidbody>();
            if (enemyRb != null && collision.contactCount > 0)
            {
                float knockbackMult = 1f;
                BossController hitBoss = collision.gameObject.GetComponent<BossController>();
                if (hitBoss != null)
                    knockbackMult = 1f - hitBoss.knockbackResistance;

                Vector3 pushDirection = -collision.contacts[0].normal;
                pushDirection.y = 0.5f;
                enemyRb.AddForce(pushDirection.normalized * impactSpeed * enemyKnockbackForce * knockbackMult, ForceMode.Impulse);
            }
        }

        // ── Tank: Wall bounce penalty (hit static/non-enemy geometry) ──
        if (!hitEnemy && wallBouncePenalty > 0f && collision.gameObject.GetComponent<BallController>() == null)
        {
            TankAbility tankAbility = GetComponent<TankAbility>();
            if (tankAbility != null && tankAbility.enabled)
                tankAbility.ApplyWallBouncePenalty();
        }

        BallController otherPlayer = collision.gameObject.GetComponent<BallController>();
        if (otherPlayer != null && otherPlayer != this)
        {
            bool isThisBot = !NetworkObject.IsPlayerObject;
            bool isOtherBot = !otherPlayer.NetworkObject.IsPlayerObject;

            if (!isThisBot && !isOtherBot) // Only apply damage if BOTH are humans
            {
                int pvpDamage = Mathf.FloorToInt(impactSpeed * damageMultiplier);
                
                if (pvpDamage > 0)
                {
                    otherPlayer.TakeDamageServerRpc(pvpDamage);
                }
            }

            // Knock the other player back
            Rigidbody otherRb = otherPlayer.GetComponent<Rigidbody>();
            if (otherRb != null && collision.contactCount > 0)
            {
                Vector3 pushDirection = -collision.contacts[0].normal;
                pushDirection.y = 0.5f; 
                otherRb.AddForce(pushDirection.normalized * impactSpeed * enemyKnockbackForce, ForceMode.Impulse);
            }
            
            // Apply pool ball effect to ourselves too
            rb.linearVelocity = rb.linearVelocity * playerImpactDampen;
        }
    }

    public void TakeDamage(int damage)
    {
        // On owner, we just request the server to do it
        TakeDamageServerRpc(damage);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TakeDamageServerRpc(int damage)
    {
        // Only server can modify NetworkVariable
        if (!IsServer) return;

        // ── Immortality check (Healer uber) ──
        if (isImmortal.Value) return;

        // ── Tank bubble check ──
        if (insideTankBubble) return;

        // Check for mercy period
        if (Time.time < lastDamageTime + damageCooldown) return;

        lastDamageTime = Time.time;

        // ── Tank: Shield absorbs damage first ──
        if (shieldHealth.Value > 0f)
        {
            float remaining = shieldHealth.Value - damage;
            if (remaining >= 0f)
            {
                shieldHealth.Value = remaining;
                return; // Shield absorbed all damage
            }
            else
            {
                shieldHealth.Value = 0f;
                damage = Mathf.Abs(Mathf.FloorToInt(remaining)); // overflow to health
            }
        }

        currentHealth.Value -= damage;

        if (currentHealth.Value <= 0)
        {
            currentHealth.Value = 0;
            RespawnClientRpc();
        }
    }

    [ClientRpc]
    private void RespawnClientRpc()
    {
        if (IsOwner)
        {
            Debug.Log("Game Over! Respawning...");
            Respawn();
        }
    }

    [ContextMenu("Manual Respawn")]
    public void Respawn()
    {
        // Get a fresh position from the spawn manager if available
        if (PlayerSpawnManager.Instance != null)
        {
            spawnPosition = PlayerSpawnManager.Instance.GetRandomSpawnPosition();
        }

        // Reset position and rotation
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        // Snap camera instantly to avoid jerk — but only if not locked by a cutscene
        if (playerCameraController != null && !isCutsceneLocked) playerCameraController.SnapToTarget();

        // Reset speed and physics
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Tell server to reset health
        if (IsOwner)
        {
            ResetHealthServerRpc();
        }

        Debug.Log("Player Respawned");
    }

    [ServerRpc]
    private void ResetHealthServerRpc()
    {
        currentHealth.Value = maxHealth;
        lastDamageTime = -10f; // Reset mercy delay
    }

    // ═══ CLASS SYSTEM — RPCs ══════════════════════════════════════

    [ServerRpc]
    public void SetPlayerClassServerRpc(int classType)
    {
        playerClass.Value = classType;
    }

    [ServerRpc]
    public void SpawnBotTeamServerRpc(int excludedClassType)
    {
        if (!IsServer) return;
        
        System.Collections.Generic.List<int> availableClasses = new System.Collections.Generic.List<int>();
        for (int i = 1; i <= 3; i++)
        {
            if (i != excludedClassType) availableClasses.Add(i);
        }
        
        // Shuffle available classes
        for (int i = 0; i < availableClasses.Count; i++)
        {
            int temp = availableClasses[i];
            int randomIndex = Random.Range(i, availableClasses.Count);
            availableClasses[i] = availableClasses[randomIndex];
            availableClasses[randomIndex] = temp;
        }

        foreach (int classType in availableClasses)
        {
            Vector3 spawnPos = transform.position + new Vector3(Random.Range(-5f, 5f), 5f, Random.Range(-5f, 5f));
            if (PlayerSpawnManager.Instance != null)
            {
                spawnPos = PlayerSpawnManager.Instance.GetRandomSpawnPosition();
            }
            
            GameObject bot = Instantiate(NetworkManager.Singleton.NetworkConfig.PlayerPrefab, spawnPos, Quaternion.identity);
            NetworkObject netObj = bot.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn(true);
                
                BallController botController = bot.GetComponent<BallController>();
                if (botController != null)
                {
                    botController.SetPlayerClassServerRpc(classType);
                    string botName = classType == 1 ? "Sir Twink-a-Lot (Bot)" : (classType == 2 ? "Sir Heals-a-Lot (Bot)" : "Sir Tanks-a-Lot (Bot)");
                    botController.SetPlayerNameServerRpc(botName);
                }
            }
        }
    }

    [ServerRpc]
    public void ResetHealthToMaxServerRpc(int newMax)
    {
        maxHealth = newMax;
        currentHealth.Value = newMax;
        lastDamageTime = -10f;
    }

    [ServerRpc]
    public void SetShieldHealthServerRpc(float value)
    {
        shieldHealth.Value = value;
    }

    [ServerRpc]
    public void SetUberMeterServerRpc(float value)
    {
        uberMeter.Value = value;
    }

    // ── Heal Target (Healer beam / AOE) ──
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void HealTargetServerRpc(ulong targetNetworkObjectId, float amount)
    {
        if (!IsServer) return;
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var netObj)) return;

        BallController target = netObj.GetComponent<BallController>();
        if (target == null) return;

        // Heal normal health
        target.currentHealth.Value = Mathf.Min(target.currentHealth.Value + Mathf.CeilToInt(amount), target.maxHealth);

        // If target is Tank, also top up shield
        if (target.maxShieldHealth > 0f && target.shieldHealth.Value < target.maxShieldHealth)
        {
            target.shieldHealth.Value = Mathf.Min(target.shieldHealth.Value + amount * 0.5f, target.maxShieldHealth);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void HealSelfServerRpc(float amount)
    {
        if (!IsServer) return;
        currentHealth.Value = Mathf.Min(currentHealth.Value + Mathf.CeilToInt(amount), maxHealth);
    }

    // ── Uber: Immortality ──
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ActivateImmortalUberServerRpc(ulong targetNetworkObjectId, float duration)
    {
        if (!IsServer) return;

        // Make self immortal
        isImmortal.Value = true;
        StartCoroutine(EndImmortalAfter(this, duration));

        // Make target immortal too
        if (targetNetworkObjectId != 0 &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var netObj))
        {
            BallController target = netObj.GetComponent<BallController>();
            if (target != null)
            {
                target.isImmortal.Value = true;
                StartCoroutine(EndImmortalAfter(target, duration));
            }
        }
    }

    private IEnumerator EndImmortalAfter(BallController target, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (target != null && target.IsSpawned)
            target.isImmortal.Value = false;
    }

    // ── Uber: Teleport ally to self ──
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TeleportAllyServerRpc(ulong targetNetworkObjectId)
    {
        if (!IsServer) return;
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var netObj)) return;

        BallController target = netObj.GetComponent<BallController>();
        if (target == null) return;

        // Teleport target to healer's position + small offset
        Vector3 pos = transform.position + Vector3.up * 2f;
        TeleportPlayerClientRpc(target.OwnerClientId, pos);
    }

    [ClientRpc]
    private void TeleportPlayerClientRpc(ulong ownerClientId, Vector3 position)
    {
        if (NetworkManager.Singleton.LocalClientId != ownerClientId) return;

        // Find local ball and move it
        BallController[] all = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        foreach (var ball in all)
        {
            if (ball.IsOwner)
            {
                ball.transform.position = position;
                Rigidbody ballRb = ball.GetComponent<Rigidbody>();
                if (ballRb != null)
                {
                    ballRb.linearVelocity = Vector3.zero;
                    ballRb.angularVelocity = Vector3.zero;
                }
                Physics.SyncTransforms();
                if (ball.CutsceneCameraRef != null) ball.CutsceneCameraRef.SnapToTarget();
                break;
            }
        }
    }

    // ── Tank: Shield Bubble ──
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ActivateBubbleServerRpc(float duration)
    {
        // Server-side: bubble protection is handled client-side via insideTankBubble flag
        // This RPC exists for future server-side validation
        Debug.Log($"[Tank] Bubble shield active for {duration:F1}s");
    }

    // ── Tank: Heavy Slam ──
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TankHeavySlamServerRpc(float radius, float knockback, float killThreshold)
    {
        if (!IsServer) return;

        // Wait for the ball to actually land (slight delay)
        StartCoroutine(HeavySlamLandCheck(radius, knockback, killThreshold));
    }

    private IEnumerator HeavySlamLandCheck(float radius, float knockback, float killThreshold)
    {
        // Wait until we hit something (max 2 seconds)
        float timer = 0f;
        Rigidbody slamRb = GetComponent<Rigidbody>();
        while (timer < 2f && slamRb != null && slamRb.linearVelocity.y < -1f)
        {
            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Impact!
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (var hit in hits)
        {
            // Kill light enemies instantly
            EnemyPlayer enemy = hit.GetComponent<EnemyPlayer>();
            if (enemy != null && enemy.maxHealth <= killThreshold)
            {
                enemy.TakeDamage(enemy.maxHealth * 10f); // instant kill
                continue;
            }

            // Knock bosses back hard
            BossController boss = hit.GetComponent<BossController>();
            if (boss != null)
            {
                boss.TakeDamage(damageMultiplier * 5f);
                Rigidbody bossRb = boss.GetComponent<Rigidbody>();
                if (bossRb != null)
                {
                    Vector3 push = (boss.transform.position - transform.position).normalized;
                    push.y = 0.5f;
                    bossRb.AddForce(push.normalized * knockback, ForceMode.Impulse);
                }
            }

            // Knock other players
            BallController otherBall = hit.GetComponent<BallController>();
            if (otherBall != null && otherBall != this)
            {
                Rigidbody otherRb = otherBall.GetComponent<Rigidbody>();
                if (otherRb != null)
                {
                    Vector3 push = (otherBall.transform.position - transform.position).normalized;
                    push.y = 0.5f;
                    otherRb.AddForce(push.normalized * knockback * 0.5f, ForceMode.Impulse);
                }
            }
        }

        Debug.Log("[Tank] HEAVY SLAM IMPACT!");
    }

    // ── Healer: Health on kill callback ──
    private void OnEnemyKilled()
    {
        HealerAbility healerAbility = GetComponent<HealerAbility>();
        if (healerAbility != null && healerAbility.enabled)
        {
            healerAbility.OnEnemyKilled();
        }
    }

    // ═══ END CLASS SYSTEM ═════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        // Draw a red sphere to visualize the attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

}
