using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public enum BossState
{
    Idle,
    Approach,
    Circle,
    WindingUp,
    Charging,
    SlamJump,
    SlamFall,
    Retreat,
    Tethering,
    LeapAscend,
    LeapDescend,
    Shockwaving
}

public class BossController : NetworkBehaviour
{
    // ─── Preset ───────────────────────────────────────────────────
    [Header("Preset (Optional)")]
    [Tooltip("Drag a BossPreset asset here. Use context menu 'Load From Preset' to apply, or it auto-loads on spawn.")]
    public BossPreset preset;

    // ─── Identity ─────────────────────────────────────────────────
    [Header("Identity")]
    public string bossName = "Boss";

    // ─── Health ───────────────────────────────────────────────────
    [Header("Health")]
    [Min(1f)] public float maxHealth = 500f;
    [Tooltip("0 = full knockback from player hits, 1 = completely immovable")]
    [Range(0f, 1f)] public float knockbackResistance = 0.9f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        500f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // ─── Movement ─────────────────────────────────────────────────
    [Header("Movement")]
    [Range(0f, 1f)] public float aggression = 0.7f;
    [Min(1f)] public float moveSpeed = 10f;
    [Range(2f, 30f)] public float preferredDistance = 8f;
    [Range(5f, 60f)] public float chaseRange = 30f;

    // ─── Charge Attack ────────────────────────────────────────────
    [Header("Charge Attack")]
    public bool enableCharge = true;
    [Range(5f, 80f)] public float chargeForce = 35f;
    [Range(0f, 3f)] public float chargeWindup = 0.8f;
    [Range(0.5f, 15f)] public float chargeCooldown = 4f;
    [Range(0f, 10f)] public float chargeDamage = 3f;

    // ─── Circling ─────────────────────────────────────────────────
    [Header("Circling")]
    public bool enableCircling = true;
    [Range(0f, 1f)] public float orbitTendency = 0.3f;
    [Min(1f)] public float orbitSpeed = 6f;
    [Range(3f, 25f)] public float orbitRadius = 10f;

    // ─── Ground Slam ──────────────────────────────────────────────
    [Header("Ground Slam")]
    public bool enableSlam = true;
    [Range(5f, 50f)] public float slamJumpForce = 20f;
    [Range(0.5f, 20f)] public float slamCooldown = 8f;
    [Range(2f, 20f)] public float slamRadius = 8f;
    [Range(0f, 10f)] public float slamDamage = 2f;
    [Range(0f, 30f)] public float slamKnockback = 10f;

    // ─── Rage Mode ────────────────────────────────────────────────
    [Header("Rage Mode")]
    public bool enableRage = true;
    [Range(0f, 1f)] public float rageThreshold = 0.3f;
    [Range(1f, 3f)] public float rageSpeedMultiplier = 1.5f;
    [Range(1f, 3f)] public float rageDamageMultiplier = 1.5f;
    [Range(0.1f, 1f)] public float rageCooldownMultiplier = 0.6f;

    // ─── Vortex (Gravity Pull) ──────────────────────────────────
    [Header("Vortex (Gravity Pull)")]
    public bool enableVortex = false;
    [Range(1f, 25f)] public float vortexForce = 8f;
    [Range(3f, 30f)] public float vortexRadius = 12f;
    public bool vortexOnlyWhileCircling = false;

    // ─── Shockwave (Radial Knockback) ────────────────────────────
    [Header("Shockwave (Radial Knockback)")]
    public bool enableShockwave = false;
    [Range(5f, 40f)] public float shockwaveForce = 15f;
    [Range(3f, 25f)] public float shockwaveRadius = 10f;
    [Range(2f, 30f)] public float shockwaveCooldown = 10f;
    [Range(0f, 10f)] public float shockwaveDamage = 1f;

    // ─── Tether (Chain Pull) ─────────────────────────────────────
    [Header("Tether (Chain Pull)")]
    public bool enableTether = false;
    [Range(1f, 25f)] public float tetherForce = 12f;
    [Range(1f, 10f)] public float tetherDuration = 3f;
    [Range(5f, 30f)] public float tetherCooldown = 12f;
    [Range(5f, 40f)] public float tetherMaxRange = 20f;

    // ─── Leap Strike ─────────────────────────────────────────────
    [Header("Leap Strike")]
    public bool enableLeap = false;
    [Range(5f, 60f)] public float leapForce = 25f;
    [Range(0.3f, 3f)] public float leapArc = 1f;
    [Range(3f, 20f)] public float leapCooldown = 6f;
    [Range(0f, 10f)] public float leapDamage = 2f;
    [Range(0f, 25f)] public float leapKnockback = 8f;
    [Range(2f, 15f)] public float leapRadius = 6f;

    // ─── Personality ──────────────────────────────────────────────
    [Header("Personality")]
    [Range(0f, 1f)] public float erraticness = 0.1f;
    [Range(1f, 20f)] public float idleRoamRadius = 5f;

    // ─── UI ───────────────────────────────────────────────────────
    [Header("UI")]
    public GameObject uiContainer;
    public Vector3 uiOffset = new Vector3(0, 1.2f, 0);
    public Slider healthSlider;
    public TextMeshProUGUI nameText;
    public Color normalTextColor = Color.white;
    public Color rageTextColor = new Color(1f, 0.3f, 0f);
    public float colorFadeSpeed = 5f;

    // ─── Private State ────────────────────────────────────────────
    private BossState state = BossState.Idle;
    private Rigidbody rb;
    private Transform playerTransform;
    private BallController playerController;
    private Transform cameraTransform;
    private Transform homeBase;

    private float lastChargeTime = -100f;
    private float lastSlamTime = -100f;
    private float windupTimer = 0f;
    private float orbitAngle = 0f;
    private float nextDecisionTime = 0f;
    private float erraticTimer = 0f;
    private Vector3 erraticDir = Vector3.zero;
    private Vector3 chargeDirection = Vector3.forward;

    private float lastShockwaveTime = -100f;
    private float lastTetherTime = -100f;
    private float lastLeapTime = -100f;
    private BallController tetheredPlayer = null;
    private float tetherTimer = 0f;

    private Vector3 currentWanderTarget;
    private bool isWandering = false;
    private float timeSinceLastTarget = 0f;

    private bool IsRaging => enableRage && rageThreshold > 0f && currentHealth.Value <= maxHealth * rageThreshold;
    private float SpeedMult => IsRaging ? rageSpeedMultiplier : 1f;
    private float DamageMult => IsRaging ? rageDamageMultiplier : 1f;
    private float CooldownMult => IsRaging ? rageCooldownMultiplier : 1f;

    // ─── Lifecycle ────────────────────────────────────────────────

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (nameText != null) nameText.text = bossName;
        UpdateHealthUI(currentHealth.Value);

        if (uiContainer != null)
        {
            if (uiContainer.TryGetComponent<RectTransform>(out var rect))
                rect.anchoredPosition3D = Vector3.zero;
            else
                uiContainer.transform.localPosition = Vector3.zero;

            uiContainer.transform.position = transform.position;
            uiContainer.transform.SetParent(null, true);
        }

        SetUIVisibility(false);
    }

    public override void OnNetworkSpawn()
    {
        if (preset != null)
            LoadFromPreset(preset);

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            FindNearestPlayer();
        }

        currentHealth.OnValueChanged += OnHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
        if (uiContainer != null) Destroy(uiContainer);
    }

    // ─── Preset System ────────────────────────────────────────────

    [ContextMenu("Load From Preset")]
    public void LoadFromPresetMenu()
    {
        if (preset != null) LoadFromPreset(preset);
        else Debug.LogWarning("No preset assigned.");
    }

    public void LoadFromPreset(BossPreset p)
    {
        bossName = p.bossName;
        maxHealth = p.maxHealth;
        knockbackResistance = p.knockbackResistance;
        aggression = p.aggression;
        moveSpeed = p.moveSpeed;
        preferredDistance = p.preferredDistance;
        chaseRange = p.chaseRange;
        enableCharge = p.enableCharge;
        chargeForce = p.chargeForce;
        chargeWindup = p.chargeWindup;
        chargeCooldown = p.chargeCooldown;
        chargeDamage = p.chargeDamage;
        enableCircling = p.enableCircling;
        orbitTendency = p.orbitTendency;
        orbitSpeed = p.orbitSpeed;
        orbitRadius = p.orbitRadius;
        enableSlam = p.enableSlam;
        slamJumpForce = p.slamJumpForce;
        slamCooldown = p.slamCooldown;
        slamRadius = p.slamRadius;
        slamDamage = p.slamDamage;
        slamKnockback = p.slamKnockback;
        enableRage = p.enableRage;
        rageThreshold = p.rageThreshold;
        rageSpeedMultiplier = p.rageSpeedMultiplier;
        rageDamageMultiplier = p.rageDamageMultiplier;
        rageCooldownMultiplier = p.rageCooldownMultiplier;
        enableVortex = p.enableVortex;
        vortexForce = p.vortexForce;
        vortexRadius = p.vortexRadius;
        vortexOnlyWhileCircling = p.vortexOnlyWhileCircling;
        enableShockwave = p.enableShockwave;
        shockwaveForce = p.shockwaveForce;
        shockwaveRadius = p.shockwaveRadius;
        shockwaveCooldown = p.shockwaveCooldown;
        shockwaveDamage = p.shockwaveDamage;
        enableTether = p.enableTether;
        tetherForce = p.tetherForce;
        tetherDuration = p.tetherDuration;
        tetherCooldown = p.tetherCooldown;
        tetherMaxRange = p.tetherMaxRange;
        enableLeap = p.enableLeap;
        leapForce = p.leapForce;
        leapArc = p.leapArc;
        leapCooldown = p.leapCooldown;
        leapDamage = p.leapDamage;
        leapKnockback = p.leapKnockback;
        leapRadius = p.leapRadius;
        erraticness = p.erraticness;
        idleRoamRadius = p.idleRoamRadius;

        if (nameText != null) nameText.text = bossName;
    }

#if UNITY_EDITOR
    [ContextMenu("Save Current Settings To New Preset")]
    private void SaveToNewPreset()
    {
        BossPreset p = ScriptableObject.CreateInstance<BossPreset>();
        CopyToPreset(p);

        string folder = "Assets/BossPresets";
        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);

        string path = $"{folder}/{bossName}_Preset.asset";
        path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(path);
        UnityEditor.AssetDatabase.CreateAsset(p, path);
        UnityEditor.AssetDatabase.SaveAssets();
        preset = p;
        Debug.Log($"Boss preset saved to {path}");
    }

    [ContextMenu("Overwrite Assigned Preset")]
    private void OverwritePreset()
    {
        if (preset == null) { Debug.LogWarning("No preset assigned to overwrite."); return; }
        CopyToPreset(preset);
        UnityEditor.EditorUtility.SetDirty(preset);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"Preset '{preset.name}' overwritten.");
    }

    private void CopyToPreset(BossPreset p)
    {
        p.bossName = bossName;
        p.maxHealth = maxHealth;
        p.knockbackResistance = knockbackResistance;
        p.aggression = aggression;
        p.moveSpeed = moveSpeed;
        p.preferredDistance = preferredDistance;
        p.chaseRange = chaseRange;
        p.enableCharge = enableCharge;
        p.chargeForce = chargeForce;
        p.chargeWindup = chargeWindup;
        p.chargeCooldown = chargeCooldown;
        p.chargeDamage = chargeDamage;
        p.enableCircling = enableCircling;
        p.orbitTendency = orbitTendency;
        p.orbitSpeed = orbitSpeed;
        p.orbitRadius = orbitRadius;
        p.enableSlam = enableSlam;
        p.slamJumpForce = slamJumpForce;
        p.slamCooldown = slamCooldown;
        p.slamRadius = slamRadius;
        p.slamDamage = slamDamage;
        p.slamKnockback = slamKnockback;
        p.enableRage = enableRage;
        p.rageThreshold = rageThreshold;
        p.rageSpeedMultiplier = rageSpeedMultiplier;
        p.rageDamageMultiplier = rageDamageMultiplier;
        p.rageCooldownMultiplier = rageCooldownMultiplier;
        p.enableVortex = enableVortex;
        p.vortexForce = vortexForce;
        p.vortexRadius = vortexRadius;
        p.vortexOnlyWhileCircling = vortexOnlyWhileCircling;
        p.enableShockwave = enableShockwave;
        p.shockwaveForce = shockwaveForce;
        p.shockwaveRadius = shockwaveRadius;
        p.shockwaveCooldown = shockwaveCooldown;
        p.shockwaveDamage = shockwaveDamage;
        p.enableTether = enableTether;
        p.tetherForce = tetherForce;
        p.tetherDuration = tetherDuration;
        p.tetherCooldown = tetherCooldown;
        p.tetherMaxRange = tetherMaxRange;
        p.enableLeap = enableLeap;
        p.leapForce = leapForce;
        p.leapArc = leapArc;
        p.leapCooldown = leapCooldown;
        p.leapDamage = leapDamage;
        p.leapKnockback = leapKnockback;
        p.leapRadius = leapRadius;
        p.erraticness = erraticness;
        p.idleRoamRadius = idleRoamRadius;
    }
#endif

    // ─── AI State Machine (Server Only) ───────────────────────────

    void FixedUpdate()
    {
        if (!IsServer) return;
        if (rb == null) return;

        preCollisionVelocity = rb.linearVelocity;
        preCollisionAngular = rb.angularVelocity;

        if (playerTransform == null || Time.frameCount % 60 == 0)
            FindNearestPlayer();

        if (playerTransform == null)
        {
            HandleIdle();
            return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        if (dist > chaseRange)
        {
            timeSinceLastTarget += Time.fixedDeltaTime;
            if (timeSinceLastTarget > 3f && homeBase != null)
                HandleIdle();
            else
                Decelerate();
            return;
        }

        timeSinceLastTarget = 0f;
        isWandering = false;
        UpdateErratic();
        UpdateVortex();
        UpdateTether();

        switch (state)
        {
            case BossState.Idle:
            case BossState.Approach:
            case BossState.Circle:
            case BossState.Retreat:
                DecideAction(dist);
                ExecuteMovement(dist);
                break;
            case BossState.WindingUp:
                UpdateWindup();
                break;
            case BossState.Charging:
                break;
            case BossState.SlamJump:
                UpdateSlamJump();
                break;
            case BossState.SlamFall:
                break;
            case BossState.Tethering:
                ExecuteMovement(dist);
                break;
            case BossState.LeapAscend:
                if (rb.linearVelocity.y <= 0f)
                    state = BossState.LeapDescend;
                break;
            case BossState.LeapDescend:
                break;
            case BossState.Shockwaving:
                break;
        }
    }

    private void DecideAction(float dist)
    {
        if (Time.time < nextDecisionTime) return;
        nextDecisionTime = Time.time + Random.Range(0.3f, 0.8f);

        bool canCharge = enableCharge && Time.time >= lastChargeTime + chargeCooldown * CooldownMult;
        bool canSlam = enableSlam && Time.time >= lastSlamTime + slamCooldown * CooldownMult;
        bool canShockwave = enableShockwave && Time.time >= lastShockwaveTime + shockwaveCooldown * CooldownMult;
        bool canTether = enableTether && tetheredPlayer == null && Time.time >= lastTetherTime + tetherCooldown * CooldownMult;
        bool canLeap = enableLeap && Time.time >= lastLeapTime + leapCooldown * CooldownMult;
        bool inAttackRange = dist <= preferredDistance * 1.5f;

        // Leap from long range — pounce before closing distance
        if (canLeap && dist > preferredDistance && dist < chaseRange * 0.8f && Random.value < aggression * 0.5f)
        {
            BeginLeap();
            return;
        }

        // Tether to reel them in when they try to keep distance
        if (canTether && dist > preferredDistance * 0.6f && Random.value < 0.35f)
        {
            BeginTether();
            return;
        }

        if (inAttackRange && canCharge && Random.value > orbitTendency)
        {
            BeginCharge();
            return;
        }

        if (inAttackRange && canSlam && Random.value < 0.3f)
        {
            BeginSlam();
            return;
        }

        // Shockwave when player is close — blast them back (off ledges!)
        if (canShockwave && dist < shockwaveRadius * 0.7f && Random.value < 0.4f)
        {
            PerformShockwave();
            return;
        }

        if (enableCircling && Random.value < orbitTendency)
            state = BossState.Circle;
        else if (dist > preferredDistance && Random.value < aggression)
            state = BossState.Approach;
        else if (dist < preferredDistance * 0.5f && Random.value > aggression)
            state = BossState.Retreat;
        else
            state = BossState.Approach;
    }

    private void ExecuteMovement(float dist)
    {
        Vector3 toPlayer = (playerTransform.position - transform.position);
        toPlayer.y = 0f;
        Vector3 dirToPlayer = toPlayer.normalized;
        float speed = moveSpeed * SpeedMult;

        Vector3 force = Vector3.zero;

        switch (state)
        {
            case BossState.Approach:
                force = dirToPlayer * speed;
                break;

            case BossState.Circle:
                orbitAngle += orbitSpeed * Time.fixedDeltaTime / orbitRadius;
                Vector3 orbitOffset = new Vector3(Mathf.Cos(orbitAngle), 0f, Mathf.Sin(orbitAngle)) * orbitRadius;
                Vector3 orbitTarget = playerTransform.position + orbitOffset;
                Vector3 toOrbit = (orbitTarget - transform.position);
                toOrbit.y = 0f;
                force = toOrbit.normalized * speed;
                break;

            case BossState.Retreat:
                force = -dirToPlayer * speed * 0.7f;
                break;
        }

        if (erraticness > 0f)
            force += erraticDir * (speed * erraticness);

        rb.AddForce(force);
    }

    // ─── Charge Attack ────────────────────────────────────────────

    private void BeginCharge()
    {
        state = BossState.WindingUp;
        windupTimer = chargeWindup;

        Vector3 toPlayer = playerTransform.position - transform.position;
        toPlayer.y = 0f;
        chargeDirection = toPlayer.normalized;

        // Brief brake during windup
        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x * 0.2f,
            rb.linearVelocity.y,
            rb.linearVelocity.z * 0.2f
        );
    }

    private void UpdateWindup()
    {
        windupTimer -= Time.fixedDeltaTime;
        if (windupTimer <= 0f)
        {
            state = BossState.Charging;
            lastChargeTime = Time.time;

            // Re-aim slightly toward current player position
            if (playerTransform != null)
            {
                Vector3 toPlayer = playerTransform.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.1f)
                    chargeDirection = Vector3.Lerp(chargeDirection, toPlayer.normalized, 0.5f).normalized;
            }

            rb.AddForce(chargeDirection * chargeForce * SpeedMult, ForceMode.Impulse);
            Invoke(nameof(EndCharge), 0.6f);
        }
    }

    private void EndCharge()
    {
        if (state == BossState.Charging)
            state = BossState.Approach;
    }

    // ─── Ground Slam ──────────────────────────────────────────────

    private void BeginSlam()
    {
        state = BossState.SlamJump;
        lastSlamTime = Time.time;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.3f, 0f, rb.linearVelocity.z * 0.3f);
        rb.AddForce(Vector3.up * slamJumpForce * SpeedMult, ForceMode.Impulse);
    }

    private void UpdateSlamJump()
    {
        // Wait until apex (falling)
        if (rb.linearVelocity.y <= 0f)
        {
            state = BossState.SlamFall;
            rb.AddForce(Vector3.down * slamJumpForce * 2f, ForceMode.Impulse);
        }
    }

    private void SlamLand()
    {
        state = BossState.Approach;
        float damage = slamDamage * DamageMult;

        Collider[] hits = Physics.OverlapSphere(transform.position, slamRadius);
        foreach (var hit in hits)
        {
            BallController player = hit.GetComponent<BallController>();
            if (player == null) continue;

            player.TakeDamage(Mathf.RoundToInt(damage));

            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                Vector3 push = (player.transform.position - transform.position).normalized;
                push.y = 0.5f;
                playerRb.AddForce(push.normalized * slamKnockback, ForceMode.Impulse);
            }
        }
    }

    // ─── Vortex (Passive Gravity Pull) ──────────────────────────────

    private void UpdateVortex()
    {
        if (!enableVortex) return;
        if (vortexOnlyWhileCircling && state != BossState.Circle) return;

        float r2 = vortexRadius * vortexRadius;
        BallController[] players = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            Vector3 diff = transform.position - p.transform.position;
            float dist2 = diff.sqrMagnitude;
            if (dist2 > r2 || dist2 < 1f) continue;

            float dist = Mathf.Sqrt(dist2);
            float strength = vortexForce * (1f - dist / vortexRadius) * SpeedMult;

            Rigidbody prb = p.GetComponent<Rigidbody>();
            if (prb != null)
                prb.AddForce(diff.normalized * strength, ForceMode.Force);
        }
    }

    // ─── Shockwave (Radial Knockback) ────────────────────────────

    private void PerformShockwave()
    {
        state = BossState.Shockwaving;
        lastShockwaveTime = Time.time;

        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x * 0.1f,
            rb.linearVelocity.y,
            rb.linearVelocity.z * 0.1f
        );

        float dmg = shockwaveDamage * DamageMult;

        Collider[] hits = Physics.OverlapSphere(transform.position, shockwaveRadius);
        foreach (var hit in hits)
        {
            BallController player = hit.GetComponent<BallController>();
            if (player == null) continue;

            if (dmg > 0f) player.TakeDamage(Mathf.RoundToInt(dmg));

            Rigidbody prb = player.GetComponent<Rigidbody>();
            if (prb != null)
            {
                Vector3 push = (player.transform.position - transform.position);
                push.y = 0f;
                push = push.normalized;
                push.y = 0.4f;
                prb.AddForce(push.normalized * shockwaveForce * SpeedMult, ForceMode.Impulse);
            }
        }

        Invoke(nameof(EndShockwave), 0.3f);
    }

    private void EndShockwave()
    {
        if (state == BossState.Shockwaving)
            state = BossState.Retreat;
    }

    // ─── Tether (Chain Pull) ─────────────────────────────────────

    private void BeginTether()
    {
        if (playerController == null) return;
        state = BossState.Tethering;
        lastTetherTime = Time.time;
        tetheredPlayer = playerController;
        tetherTimer = tetherDuration;
    }

    private void UpdateTether()
    {
        if (tetheredPlayer == null) return;

        tetherTimer -= Time.fixedDeltaTime;
        if (tetherTimer <= 0f || !tetheredPlayer.gameObject.activeSelf)
        {
            ReleaseTether();
            return;
        }

        Rigidbody prb = tetheredPlayer.GetComponent<Rigidbody>();
        if (prb == null) { ReleaseTether(); return; }

        Vector3 diff = transform.position - tetheredPlayer.transform.position;
        float dist = diff.magnitude;
        Vector3 pullDir = diff.normalized;

        float force = tetherForce * SpeedMult;
        if (dist > tetherMaxRange)
            force *= 3f;

        prb.AddForce(pullDir * force, ForceMode.Force);
    }

    private void ReleaseTether()
    {
        tetheredPlayer = null;
        if (state == BossState.Tethering)
            state = BossState.Approach;
    }

    // ─── Leap Strike ─────────────────────────────────────────────

    private void BeginLeap()
    {
        state = BossState.LeapAscend;
        lastLeapTime = Time.time;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.2f, 0f, rb.linearVelocity.z * 0.2f);

        Vector3 toPlayer = Vector3.zero;
        if (playerTransform != null)
        {
            toPlayer = playerTransform.position - transform.position;
            toPlayer.y = 0f;
        }

        Vector3 launchDir = (toPlayer.normalized + Vector3.up * leapArc).normalized;
        rb.AddForce(launchDir * leapForce * SpeedMult, ForceMode.Impulse);
    }

    private void LeapLand()
    {
        state = BossState.Approach;
        float dmg = leapDamage * DamageMult;

        Collider[] hits = Physics.OverlapSphere(transform.position, leapRadius);
        foreach (var hit in hits)
        {
            BallController player = hit.GetComponent<BallController>();
            if (player == null) continue;

            if (dmg > 0f) player.TakeDamage(Mathf.RoundToInt(dmg));

            Rigidbody prb = player.GetComponent<Rigidbody>();
            if (prb != null)
            {
                Vector3 push = (player.transform.position - transform.position).normalized;
                push.y = 0.5f;
                prb.AddForce(push.normalized * leapKnockback, ForceMode.Impulse);
            }
        }
    }

    // ─── Erratic Movement ─────────────────────────────────────────

    private void UpdateErratic()
    {
        if (erraticness <= 0f) return;

        erraticTimer -= Time.fixedDeltaTime;
        if (erraticTimer <= 0f)
        {
            erraticTimer = Random.Range(0.2f, 1f);
            Vector2 r = Random.insideUnitCircle;
            erraticDir = new Vector3(r.x, 0f, r.y);
        }
    }

    // ─── Idle / Wander ────────────────────────────────────────────

    private void HandleIdle()
    {
        if (homeBase == null) { Decelerate(); return; }

        if (!isWandering || Vector3.Distance(transform.position, currentWanderTarget) < 1f)
        {
            Vector2 r = Random.insideUnitCircle * idleRoamRadius;
            currentWanderTarget = homeBase.position + new Vector3(r.x, 0f, r.y);
            isWandering = true;
        }

        Vector3 dir = currentWanderTarget - transform.position;
        dir.y = 0f;
        if (dir.magnitude > 0.5f)
            rb.AddForce(dir.normalized * (moveSpeed * 0.4f));
    }

    private void Decelerate()
    {
        rb.linearVelocity = Vector3.Lerp(
            rb.linearVelocity,
            new Vector3(0f, rb.linearVelocity.y, 0f),
            Time.fixedDeltaTime * 2f
        );
    }

    // ─── Collision ────────────────────────────────────────────────

    private Vector3 preCollisionVelocity;
    private Vector3 preCollisionAngular;

    void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        bool hitGround = collision.gameObject.GetComponent<BallController>() == null;

        if (state == BossState.SlamFall && hitGround)
            SlamLand();

        if (state == BossState.LeapDescend && hitGround)
            LeapLand();

        if (state == BossState.Charging)
        {
            BallController player = collision.gameObject.GetComponent<BallController>();
            if (player != null)
                player.TakeDamage(Mathf.RoundToInt(chargeDamage * DamageMult));
        }

        if (knockbackResistance > 0f && collision.gameObject.GetComponent<BallController>() != null)
        {
            Vector3 target = Vector3.Lerp(rb.linearVelocity, preCollisionVelocity, knockbackResistance);
            Vector3 targetAng = Vector3.Lerp(rb.angularVelocity, preCollisionAngular, knockbackResistance);
            rb.linearVelocity = target;
            rb.angularVelocity = targetAng;
        }
    }

    // ─── Player Finding ───────────────────────────────────────────

    private void FindNearestPlayer()
    {
        if (!IsServer) return;

        BallController[] players = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        float closest = Mathf.Infinity;
        BallController found = null;

        foreach (var p in players)
        {
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < closest) { closest = d; found = p; }
        }

        if (found != null)
        {
            playerController = found;
            playerTransform = found.transform;
        }
    }

    // ─── Damage / Death ───────────────────────────────────────────

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TakeDamageServerRpc(float amount)
    {
        TakeDamage(amount);
    }

    public void TakeDamage(float amount)
    {
        if (!IsServer) { TakeDamageServerRpc(amount); return; }

        currentHealth.Value -= amount;
        if (currentHealth.Value <= 0f) Die();
    }

    private void Die()
    {
        if (!IsServer) return;
        Debug.Log($"[Boss] {bossName} has been defeated!");
        if (IsSpawned) GetComponent<NetworkObject>().Despawn();
        else Destroy(gameObject);
    }

    // ─── UI ───────────────────────────────────────────────────────

    void Update()
    {
        if (uiContainer == null) return;
        uiContainer.transform.position = transform.position + uiOffset;

        if (!uiContainer.activeSelf) return;

        if (cameraTransform == null || !cameraTransform.gameObject.activeInHierarchy)
        {
            BallController[] players = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                if (!p.IsOwner) continue;
                Camera cam = p.GetComponentInChildren<Camera>(false);
                if (cam != null) { cameraTransform = cam.transform; break; }
            }
        }

        if (cameraTransform != null)
            uiContainer.transform.rotation = cameraTransform.rotation;

        if (nameText != null)
        {
            Color target = IsRaging ? rageTextColor : normalTextColor;
            nameText.color = Color.Lerp(nameText.color, target, Time.deltaTime * colorFadeSpeed);
        }
    }

    private void OnHealthChanged(float prev, float next)
    {
        UpdateHealthUI(next);
    }

    private void UpdateHealthUI(float val)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = val;
        }
    }

    public void SetUIVisibility(bool visible)
    {
        if (uiContainer != null && uiContainer.activeSelf != visible)
            uiContainer.SetActive(visible);
    }

    public void SetTargeted(bool targeted) { }
    public void SetHomeBase(Transform t) { homeBase = t; }

    // ─── Gizmos ───────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, preferredDistance);

        if (slamJumpForce > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, slamRadius);
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, orbitRadius);

        if (vortexForce > 0f)
        {
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, vortexRadius);
        }

        if (shockwaveForce > 0f)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, shockwaveRadius);
        }

        if (leapForce > 0f)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, leapRadius);
        }

        if (homeBase != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(homeBase.position, idleRoamRadius);
        }
    }
}
