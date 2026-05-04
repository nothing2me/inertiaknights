using UnityEngine;

[CreateAssetMenu(fileName = "NewBossPreset", menuName = "InertiaKnights/Boss Preset")]
public class BossPreset : ScriptableObject
{
    [Header("Identity")]
    public string bossName = "Boss";

    [Header("Health")]
    [Min(1f)] public float maxHealth = 500f;
    [Tooltip("0 = full knockback from player hits, 1 = completely immovable")]
    [Range(0f, 1f)] public float knockbackResistance = 0.9f;

    [Header("Movement")]
    [Range(0f, 1f)] public float aggression = 0.7f;
    [Min(1f)] public float moveSpeed = 10f;
    [Range(2f, 30f)] public float preferredDistance = 8f;
    [Range(5f, 60f)] public float chaseRange = 30f;

    [Header("Charge Attack")]
    public bool enableCharge = true;
    [Range(5f, 80f)] public float chargeForce = 35f;
    [Range(0f, 3f)] public float chargeWindup = 0.8f;
    [Range(0.5f, 15f)] public float chargeCooldown = 4f;
    [Range(0f, 10f)] public float chargeDamage = 3f;

    [Header("Circling")]
    public bool enableCircling = true;
    [Range(0f, 1f)] public float orbitTendency = 0.3f;
    [Min(1f)] public float orbitSpeed = 6f;
    [Range(3f, 25f)] public float orbitRadius = 10f;

    [Header("Ground Slam")]
    public bool enableSlam = true;
    [Range(5f, 50f)] public float slamJumpForce = 20f;
    [Range(0.5f, 20f)] public float slamCooldown = 8f;
    [Range(2f, 20f)] public float slamRadius = 8f;
    [Range(0f, 10f)] public float slamDamage = 2f;
    [Range(0f, 30f)] public float slamKnockback = 10f;

    [Header("Rage Mode")]
    public bool enableRage = true;
    [Range(0f, 1f)] public float rageThreshold = 0.3f;
    [Range(1f, 3f)] public float rageSpeedMultiplier = 1.5f;
    [Range(1f, 3f)] public float rageDamageMultiplier = 1.5f;
    [Range(0.1f, 1f)] public float rageCooldownMultiplier = 0.6f;

    [Header("Vortex (Gravity Pull)")]
    public bool enableVortex = false;
    [Range(1f, 25f)] public float vortexForce = 8f;
    [Range(3f, 30f)] public float vortexRadius = 12f;
    public bool vortexOnlyWhileCircling = false;

    [Header("Shockwave (Radial Knockback)")]
    public bool enableShockwave = false;
    [Range(5f, 40f)] public float shockwaveForce = 15f;
    [Range(3f, 25f)] public float shockwaveRadius = 10f;
    [Range(2f, 30f)] public float shockwaveCooldown = 10f;
    [Range(0f, 10f)] public float shockwaveDamage = 1f;

    [Header("Tether (Chain Pull)")]
    public bool enableTether = false;
    [Range(1f, 25f)] public float tetherForce = 12f;
    [Range(1f, 10f)] public float tetherDuration = 3f;
    [Range(5f, 30f)] public float tetherCooldown = 12f;
    [Range(5f, 40f)] public float tetherMaxRange = 20f;

    [Header("Leap Strike")]
    public bool enableLeap = false;
    [Range(5f, 60f)] public float leapForce = 25f;
    [Range(0.3f, 3f)] public float leapArc = 1f;
    [Range(3f, 20f)] public float leapCooldown = 6f;
    [Range(0f, 10f)] public float leapDamage = 2f;
    [Range(0f, 25f)] public float leapKnockback = 8f;
    [Range(2f, 15f)] public float leapRadius = 6f;

    [Header("Personality")]
    [Range(0f, 1f)] public float erraticness = 0.1f;
    [Range(1f, 20f)] public float idleRoamRadius = 5f;
}
