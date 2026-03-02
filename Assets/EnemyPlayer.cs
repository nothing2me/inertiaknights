using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class EnemyPlayer : NetworkBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    // Server-authoritative health variable
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    [Header("UI Settings")]
    public string enemyName = "Target";
    public GameObject uiContainer; 
    public Vector3 uiOffset = new Vector3(0, 0.8f, 0); 
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
    public float idleReturnDelay = 3f; 
    public float idleRoamRadius = 5f;  

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
        // Rigidbody and UI setup runs for everyone
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"EnemyPlayer {gameObject.name} is missing a Rigidbody! AI movement will not work.");
        }

        if (nameText != null) nameText.text = enemyName;
        UpdateHealthUI(currentHealth.Value);
        
        // Unparent the UI so it doesn't spin with the physics ball
        if (uiContainer != null)
        {
            // Zero out any pre-existing local offsets in the prefab
            // This ensures our 'uiOffset' variable is the ONLY thing that determines its height
            if (uiContainer.TryGetComponent<RectTransform>(out var rect))
            {
                rect.anchoredPosition3D = Vector3.zero;
            }
            else
            {
                uiContainer.transform.localPosition = Vector3.zero;
            }

            // Sync with current world position before unparenting to avoid origin jumps
            uiContainer.transform.position = transform.position;
            
            // Maintain world scale when unparenting
            uiContainer.transform.SetParent(null, true);
        }
        
        SetUIVisibility(false); 
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            FindNearestPlayer();
        }

        // Subscribe to health changes to update UI on all clients
        currentHealth.OnValueChanged += OnHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
        
        // Clean up unparented UI
        if (uiContainer != null)
        {
            Destroy(uiContainer);
        }
    }

    private void OnHealthChanged(float previousValue, float newValue)
    {
        UpdateHealthUI(newValue);
    }

    private void FindNearestPlayer()
    {
        if (!IsServer) return;

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
        // Always keep the UI position synced with the enemy, even if hidden/camera-less
        if (uiContainer != null)
        {
            // Position follow logic
            uiContainer.transform.position = transform.position + uiOffset;

            // Only billboard and fade if visible
            if (uiContainer.activeSelf)
            {
                if (cameraTransform == null || !cameraTransform.gameObject.activeInHierarchy)
                {
                    BallController localPlayer = GetLocalPlayer();
                    if (localPlayer != null)
                    {
                        Camera cam = localPlayer.GetComponentInChildren<Camera>(false);
                        if (cam != null) cameraTransform = cam.transform;
                    }
                }

                if (cameraTransform != null)
                {
                    // Perfectly parallel to camera
                    uiContainer.transform.rotation = cameraTransform.rotation;
                }
                else
                {
                    // Fallback to upright rotation if no camera found yet
                    uiContainer.transform.rotation = Quaternion.identity;
                }

                if (nameText != null)
                {
                    Color targetColor = isTargeted ? targetTextColor : normalTextColor;
                    nameText.color = Color.Lerp(nameText.color, targetColor, Time.deltaTime * colorFadeSpeed);
                }
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
        // Only the server runs AI logic
        if (!IsServer) return;

        if (playerTransform == null || Time.frameCount % 60 == 0)
        {
            FindNearestPlayer();
        }

        if (playerTransform == null || rb == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= chaseRange)
        {
            timeSinceLastTarget = 0f;
            isWandering = false;

            Vector3 directionToPlayer = playerTransform.position - transform.position;
            directionToPlayer.y = 0;
            directionToPlayer.Normalize();

            if (distanceToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
            {
                PerformDashAttack(directionToPlayer);
            }
            else if (!isDashing && distanceToPlayer > attackRange - 1f)
            {
                rb.AddForce(directionToPlayer * moveSpeed);
            }
        }
        else if (!isDashing)
        {
            timeSinceLastTarget += Time.fixedDeltaTime;

            if (timeSinceLastTarget >= idleReturnDelay && homeBase != null)
            {
                HandleIdleWander();
            }
            else
            {
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, new Vector3(0, rb.linearVelocity.y, 0), Time.fixedDeltaTime * 2f);
            }
        }
    }

    private void HandleIdleWander()
    {
        if (!isWandering || Vector3.Distance(transform.position, currentWanderTarget) < 1f)
        {
            Vector2 randomCircle = Random.insideUnitCircle * idleRoamRadius;
            currentWanderTarget = homeBase.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            isWandering = true;
        }

        Vector3 directionToWander = currentWanderTarget - transform.position;
        directionToWander.y = 0;
        
        if (directionToWander.magnitude > 0.5f)
        {
            directionToWander.Normalize();
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
        
        Invoke(nameof(ResetDash), 0.5f);
    }

    private void ResetDash()
    {
        isDashing = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // On collision with player, server deals damage
        BallController player = collision.gameObject.GetComponent<BallController>();
        if (isDashing && player != null)
        {
            player.TakeDamage(Mathf.RoundToInt(attackDamage));
        }
    }

    public void SetUIVisibility(bool isVisible)
    {
        if (uiContainer != null && uiContainer.activeSelf != isVisible)
        {
            uiContainer.SetActive(isVisible);
        }
    }

    /// <summary>
    /// Clients call this to request the server to damage this enemy.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float amount)
    {
        TakeDamage(amount);
    }

    public void TakeDamage(float amount)
    {
        if (!IsServer)
        {
            TakeDamageServerRpc(amount);
            return;
        }

        currentHealth.Value -= amount;
        Debug.Log($"{gameObject.name} took {amount:F1} damage! Remaining Health: {currentHealth.Value:F1}");
        
        if (currentHealth.Value <= 0)
        {
            Die();
        }
    }

    private void UpdateHealthUI(float currentVal)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentVal;
        }
    }

    private void Die()
    {
        if (!IsServer) return;

        Debug.Log($"{gameObject.name} has been defeated!");
        
        // Despawn networked object
        if (IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
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

