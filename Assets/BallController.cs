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
    private bool attackTriggered = false; // Flag to pass input from Update to FixedUpdate
    private System.Collections.Generic.HashSet<EnemyPlayer> enemiesInRange = new System.Collections.Generic.HashSet<EnemyPlayer>();
    private EnemyPlayer currentTarget = null;
    private EnemyPlayer initialDashTarget = null; // Store the target specifically for the homing phase
    
    [Header("Network Data")]
    public NetworkVariable<Unity.Collections.FixedString32Bytes> playerName = new NetworkVariable<Unity.Collections.FixedString32Bytes>("Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private CameraController playerCameraController;
    private Rigidbody rb;
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
    }

    void OnDisable()
    {
        // Disable the input actions when the object is disabled
        moveAction.Disable();
        jumpAction.Disable();
        respawnAction.Disable();
        dashAction.Disable();
        groundSlamAction.Disable();
        attackAction.Disable();
    }

    void Start()
    {
        // Get the Rigidbody component attached to the ball
        rb = GetComponent<Rigidbody>();

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
                playerCameraController.SetActiveState(true);

                // Tag this as the MainCamera for this client
                if (playerCameraController.TryGetComponent<Camera>(out Camera cam))
                {
                    cam.tag = "MainCamera";
                }

                Debug.Log($"Local camera unparented and enabled for {gameObject.name}");
            }
            else
            {
                // Fallback for legacy global camera
                playerCameraController = Object.FindFirstObjectByType<CameraController>();
                if (playerCameraController != null)
                {
                    playerCameraController.SetTarget(transform);
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

        // Check for ground
        bool isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);

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

        // Apply Aimbot Homing if actively during a dash
        if (homingStrength > 0 && initialDashTarget != null && initialDashTarget.gameObject.activeSelf)
        {
            float timeSinceDash = Time.time - lastAttackTime;
            if (timeSinceDash < homingDuration)
            {
                // Calculate ideal direction towards target
                Vector3 idealDirection = (initialDashTarget.transform.position - transform.position).normalized;
                
                // Gradually curve current velocity towards the ideal direction
                Vector3 currentVelocity = rb.linearVelocity;
                float currentSpeed = currentVelocity.magnitude;
                
                // Blend the raw velocity vector towards the target based on homing strength (scaled by fixed tick)
                Vector3 newVelocityDirection = Vector3.Lerp(currentVelocity.normalized, idealDirection, homingStrength * 10f * Time.fixedDeltaTime).normalized;
                
                rb.linearVelocity = newVelocityDirection * currentSpeed;
            }
            else
            {
                initialDashTarget = null; // Homing phase ended
            }
        }

        ManageEnemyUI();
        UpdateTargeting();
    }

    private void UpdateTargeting()
    {
        EnemyPlayer nearestEnemy = null;
        float shortestDistance = Mathf.Infinity;

        // Find the absolute closest enemy among those in range
        foreach (var enemy in enemiesInRange)
        {
            if (enemy == null || !enemy.gameObject.activeSelf) continue;

            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                nearestEnemy = enemy;
            }
        }

        // If our target changed
        if (nearestEnemy != currentTarget)
        {
            // Un-target old
            if (currentTarget != null)
            {
                currentTarget.SetTargeted(false);
            }

            // Target new
            currentTarget = nearestEnemy;

            if (currentTarget != null)
            {
                currentTarget.SetTargeted(true);
            }
        }
    }

    private void ManageEnemyUI()
    {
        // Find all enemies currently in range
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, attackRange);
        System.Collections.Generic.HashSet<EnemyPlayer> currentEnemiesInRange = new System.Collections.Generic.HashSet<EnemyPlayer>();

        foreach (var hitCollider in hitColliders)
        {
            EnemyPlayer enemy = hitCollider.GetComponent<EnemyPlayer>();
            if (enemy != null && enemy.gameObject.activeSelf)
            {
                currentEnemiesInRange.Add(enemy);
            }
        }

        // Hide UI for enemies that left the range
        foreach (var enemy in enemiesInRange)
        {
            if (enemy != null && !currentEnemiesInRange.Contains(enemy))
            {
                enemy.SetUIVisibility(false);
            }
        }

        // Show UI for enemies currently in range
        foreach (var enemy in currentEnemiesInRange)
        {
            if (enemy != null)
            {
                enemy.SetUIVisibility(true);
            }
        }

        // Update the tracked list
        enemiesInRange = currentEnemiesInRange;
    }

    private void PerformLunge()
    {
        // Use the centrally managed nearest enemy target
        if (currentTarget != null)
        {
            // Store the target for homing logic
            initialDashTarget = currentTarget;

            // Get the Rigidbody of the enemy to read its velocity
            Rigidbody enemyRb = currentTarget.GetComponent<Rigidbody>();
            Vector3 targetPosition = currentTarget.transform.position;

            if (enemyRb != null)
            {
                // Simple predictive aiming (First-Order Interception)
                // Time to reach target = Distance / Speed
                // We use our lunge impulse as an approximation of speed
                float distance = Vector3.Distance(transform.position, targetPosition);
                // Approximate lunge speed based on impulse over mass (assuming mass=1 for simplicity)
                // Or just use the lungeForce directly as a flat rate
                float estimatedTravelTime = distance / lungeForce; 
                
                // Predict where the enemy will be by adding their velocity multiplied by the time it takes us to reach them
                targetPosition += enemyRb.linearVelocity * estimatedTravelTime;
            }

            // Calculate direction towards the *predicted* enemy location
            Vector3 direction = targetPosition - transform.position;
            direction.Normalize();

            // Cancel out current vertical velocity so we don't curve mid-air while dashing downwards
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

            // Lunge directly towards the predicted position
            rb.AddForce(direction * lungeForce, ForceMode.Impulse);
            Debug.Log($"Predictive Lunge & Aimbot engaged! Targeting {currentTarget.gameObject.name}.");
        }
        else
        {
            Debug.Log("No enemies in range for lunge.");
        }
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

        // Handle damage to EnemyPlayer
        EnemyPlayer enemy = collision.gameObject.GetComponent<EnemyPlayer>();
        if (enemy != null)
        {
            // Use the relative velocity of the impact rather than post-collision velocity
            float impactSpeed = collision.relativeVelocity.magnitude;
            float damageAmount = impactSpeed * damageMultiplier;
            
            if (damageAmount > 0.1f)
            {
                enemy.TakeDamage(damageAmount);
            }

            // Pool ball effect: Force the player to stop/dampen their bounce instead of flying away
            rb.linearVelocity = rb.linearVelocity * playerImpactDampen;
            rb.angularVelocity = rb.angularVelocity * playerImpactDampen; // Kill wild spin

            // Knock the enemy back
            Rigidbody enemyRb = enemy.GetComponent<Rigidbody>();
            if (enemyRb != null && collision.contactCount > 0)
            {
                // The contact normal points towards the player, so the negative normal points into the enemy
                Vector3 pushDirection = -collision.contacts[0].normal;
                
                // Add a bit of upward lift so they fly back dramatically instead of scraping the ground
                pushDirection.y = 0.5f; 
                
            // Apply force proportional to how hard we hit them
                enemyRb.AddForce(pushDirection.normalized * impactSpeed * enemyKnockbackForce, ForceMode.Impulse);
            }
        }

        // Handle PVP damage to other players
        BallController otherPlayer = collision.gameObject.GetComponent<BallController>();
        if (otherPlayer != null && otherPlayer != this)
        {
            float impactSpeed = collision.relativeVelocity.magnitude;
            int damageAmount = Mathf.FloorToInt(impactSpeed * damageMultiplier);
            
            if (damageAmount > 0)
            {
                // Tell the server to damage the other player
                otherPlayer.TakeDamageServerRpc(damageAmount);
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
