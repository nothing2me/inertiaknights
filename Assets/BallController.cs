using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections;

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
    [Header("Input Actions")]
    [SerializeField] public InputAction moveAction; // Define an input action for movement
    [SerializeField] public InputAction jumpAction; // Define an input action for jumping
    [SerializeField] public InputAction respawnAction; // Define an input action for respawning
    [SerializeField] public InputAction dashAction; // Define an input action for dashing
    [SerializeField] public InputAction groundSlamAction; // Define an input action for ground slam
    [SerializeField] public InputAction attackAction; // Define an input action for attack/lunge
    [SerializeField] public InputAction brakeAction;

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
        if (IsOwner)
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
            }

            // Diagnostic: Log current network configuration
            if (NetworkManager.Singleton != null)
            {
                Debug.Log($"[Netcode Diagnostic] Tick Rate: {NetworkManager.Singleton.NetworkConfig.TickRate}hz. Owner Client Id: {OwnerClientId}");
            }

            // Set my name if I have one saved (e.g. from UI)
            SetPlayerNameServerRpc(NetworkManagerUI.LocalPlayerName);
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
    void FixedUpdate()
    {
        if (!IsOwner) return;

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
        Vector3 movement = (camForward * moveInput.y + camRight * moveInput.x).normalized;

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
        bool hitEnemy = false;

        EnemyPlayer enemy = collision.gameObject.GetComponent<EnemyPlayer>();
        if (enemy != null && damageAmount > 0.1f)
        {
            bool killsEnemy = enemy.currentHealth.Value - damageAmount <= 0f;
            enemy.TakeDamage(damageAmount);
            if (killsEnemy) spriteAnimator?.TriggerGoal();
            hitEnemy = true;
        }

        BossController boss = collision.gameObject.GetComponent<BossController>();
        if (boss != null && damageAmount > 0.1f)
        {
            bool killsBoss = boss.currentHealth.Value - damageAmount <= 0f;
            boss.TakeDamage(damageAmount);
            if (killsBoss) spriteAnimator?.TriggerGoal();
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

        BallController otherPlayer = collision.gameObject.GetComponent<BallController>();
        if (otherPlayer != null && otherPlayer != this)
        {
            int pvpDamage = Mathf.FloorToInt(impactSpeed * damageMultiplier);
            
            if (pvpDamage > 0)
            {
                otherPlayer.TakeDamageServerRpc(pvpDamage);
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

        // Check for mercy period (mercy check logic remains server-side for security)
        // Note: we'd ideally sync lastDamageTime but for now we'll just check it here
        if (Time.time < lastDamageTime + damageCooldown) return;

        currentHealth.Value -= damage;
        lastDamageTime = Time.time;

        if (currentHealth.Value <= 0)
        {
            currentHealth.Value = 0;
            RespawnClientRpc(); // Tell the client to reset position
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

        // Snap camera instantly to avoid jerk
        if (playerCameraController != null) playerCameraController.SnapToTarget();

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

    private void OnDrawGizmosSelected()
    {
        // Draw a red sphere to visualize the attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

}
