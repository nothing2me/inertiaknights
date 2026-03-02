using UnityEngine;
using UnityEngine.UI; // Required for standard UI elements like Slider and Text
using TMPro; // Required for TextMeshPro elements

public class EnemyPlayer : MonoBehaviour
{
    [Header("Health Settings")]
    public float health = 100f;
    public float maxHealth = 100f;
    
    [Header("UI Settings")]
    public string enemyName = "Target";
    public GameObject uiContainer; // The canvas or panel holding the UI
    public Vector3 uiOffset = new Vector3(0, 1.5f, 0); // Offset to keep it above the enemy
    public Slider healthSlider;
    public TextMeshProUGUI nameText;
    public Color normalTextColor = Color.white;
    public Color targetTextColor = Color.red;
    public float colorFadeSpeed = 5f;

    [Header("AI Settings")]
    public float chaseRange = 15f;
    public float moveSpeed = 8f;
    public float attackRange = 5f;
    public float lungeForce = 25f;
    public float attackCooldown = 2f;
    public float attackDamage = 1f;

    [Header("Idle AI Settings")]
    public float idleReturnDelay = 3f; // Seconds without target before returning
    public float idleRoamRadius = 5f;  // Roam radius around the spawn pole

    private Transform cameraTransform;
    private Transform playerTransform;
    private BallController playerController;
    private Rigidbody rb;
    private float lastAttackTime = -10f;
    private bool isDashing = false;
    private bool isTargeted = false;
    
    private Transform homeBase;
    private float timeSinceLastTarget = 0f;
    private Vector3 currentWanderTarget;
    private bool isWandering = false;

    void Start()
    {
        health = maxHealth;
        
        // Initialize UI
        if (nameText != null) nameText.text = enemyName;
        UpdateHealthUI();
        SetUIVisibility(false); // Hide by default

        // Cache the main camera for billboarding
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // Cache Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"EnemyPlayer {gameObject.name} is missing a Rigidbody! AI movement will not work.");
        }

        // In multiplayer, we don't find the player once at start
        // Instead, we'll find them dynamically in FixedUpdate
        FindNearestPlayer();
    }

    private void FindNearestPlayer()
    {
        BallController[] players = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        float closestDistance = Mathf.Infinity;
        BallController closestPlayer = null;

        foreach (var player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPlayer = player;
            }
        }

        if (closestPlayer != null)
        {
            playerController = closestPlayer;
            playerTransform = closestPlayer.transform;
        }
    }

    void Update()
    {
        // Billboard the UI and lock its position
        if (uiContainer != null && uiContainer.activeSelf)
        {
            // Find the local player's camera dynamically
            // This is the camera owned by the local client (IsOwner = true)
            if (cameraTransform == null || !cameraTransform.gameObject.activeInHierarchy)
            {
                BallController localPlayer = GetLocalPlayer();
                if (localPlayer != null)
                {
                    // Find the camera in the local player's hierarchy
                    Camera cam = localPlayer.GetComponentInChildren<Camera>(false); // false = only active cameras
                    if (cam != null) cameraTransform = cam.transform;
                }
            }

            if (cameraTransform != null)
            {
                // Lock position so the Canvas doesn't roll or spin if the enemy's body rotates!
                uiContainer.transform.position = transform.position + uiOffset;

                // Make it perfectly parallel to the camera
                uiContainer.transform.rotation = cameraTransform.rotation;
            }

            // Smoothly fade text color based on targeted status
            if (nameText != null)
            {
                Color targetColor = isTargeted ? targetTextColor : normalTextColor;
                nameText.color = Color.Lerp(nameText.color, targetColor, Time.deltaTime * colorFadeSpeed);
            }
        }
    }

    private BallController GetLocalPlayer()
    {
        BallController[] players = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.IsOwner) return p;
        }
        return null;
    }

    public void SetTargeted(bool targeted)
    {
        isTargeted = targeted;
    }

    void FixedUpdate()
    {
        // Re-scan for players occasionally or if current one is lost
        if (playerTransform == null || Time.frameCount % 60 == 0)
        {
            FindNearestPlayer();
        }

        if (playerTransform == null || rb == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Check if player is within chase range
        if (distanceToPlayer <= chaseRange)
        {
            timeSinceLastTarget = 0f; // Reset idle timer
            isWandering = false;

            // Calculate direction to player on the XZ plane
            Vector3 directionToPlayer = playerTransform.position - transform.position;
            directionToPlayer.y = 0; // Don't fly into the air
            directionToPlayer.Normalize();

            // Check if within attack range and cooldown is ready
            if (distanceToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
            {
                PerformDashAttack(directionToPlayer);
            }
            // Otherwise, chase the player if not currently dashing
            else if (!isDashing && distanceToPlayer > attackRange - 1f) // slight buffer so they don't stutter at exact attack range
            {
                rb.AddForce(directionToPlayer * moveSpeed);
            }
        }
        else if (!isDashing) // Player is out of range
        {
            timeSinceLastTarget += Time.fixedDeltaTime;

            if (timeSinceLastTarget >= idleReturnDelay && homeBase != null)
            {
                HandleIdleWander();
            }
            else
            {
                // Slow down existing momentum if just idling
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, new Vector3(0, rb.linearVelocity.y, 0), Time.fixedDeltaTime * 2f);
            }
        }
    }

    private void HandleIdleWander()
    {
        // If we need a new wander target around the base
        if (!isWandering || Vector3.Distance(transform.position, currentWanderTarget) < 1f)
        {
            // Pick a random point within roam radius of the home base
            Vector2 randomCircle = Random.insideUnitCircle * idleRoamRadius;
            currentWanderTarget = homeBase.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            isWandering = true;
        }

        // Move towards wander target
        Vector3 directionToWander = currentWanderTarget - transform.position;
        directionToWander.y = 0;
        
        if (directionToWander.magnitude > 0.5f)
        {
            directionToWander.Normalize();
            // Move slower when wandering (half speed)
            rb.AddForce(directionToWander * (moveSpeed * 0.4f)); 
        }
    }

    public void SetHomeBase(Transform spawnTransform)
    {
        homeBase = spawnTransform;
    }

    private void PerformDashAttack(Vector3 direction)
    {
        isDashing = true;
        lastAttackTime = Time.time;
        rb.AddForce(direction * lungeForce, ForceMode.Impulse);
        Debug.Log($"{gameObject.name} dashed at the player!");
        
        // Reset dash flag after a short delay so they can resume normal chasing
        Invoke(nameof(ResetDash), 0.5f);
    }

    private void ResetDash()
    {
        isDashing = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        // If we hit the player while moving fast enough (like during a dash)
        if (isDashing && collision.gameObject.CompareTag("Player") && playerController != null)
        {
            // Only deal damage if we actually hit them during the dash
            playerController.TakeDamage(Mathf.RoundToInt(attackDamage));
        }
        // Fallback in case tags aren't set but we hit the BallController
        else if (isDashing && collision.gameObject.GetComponent<BallController>() != null)
        {
             collision.gameObject.GetComponent<BallController>().TakeDamage(Mathf.RoundToInt(attackDamage));
        }
    }

    public void SetUIVisibility(bool isVisible)
    {
        if (uiContainer != null && uiContainer.activeSelf != isVisible)
        {
            uiContainer.SetActive(isVisible);
        }
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        Debug.Log($"{gameObject.name} took {amount:F1} damage! Remaining Health: {health:F1}");
        
        UpdateHealthUI();

        if (health <= 0)
        {
            Die();
        }
    }

    private void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = health;
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} has been defeated!");
        // We destroy the object so the EnemySpawn script knows it's truly dead
        // and can properly remove it from its activeEnemies list.
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize the ranges in the editor
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (homeBase != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(homeBase.position, idleRoamRadius);
        }
    }
}
