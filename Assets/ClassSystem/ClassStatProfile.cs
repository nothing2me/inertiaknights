using UnityEngine;

/// <summary>
/// Immutable stat block applied to a BallController when the player picks a class.
/// </summary>
[System.Serializable]
public struct ClassStatProfile
{
    public float speed;
    public float jumpForce;
    public float dashForce;
    public float dashCooldown;
    public int   maxHealth;
    public float damageMultiplier;

    // Light-only
    public float critChance;        // 0-1
    public float critDamagePercent;  // fraction of enemy max HP dealt on crit

    // Tank-only
    public float maxShieldHealth;
    public float wallBouncePenalty;  // extra velocity lost on wall hits (0-1, higher = more punishing)

    // ─── Factory Methods ─────────────────────────────────────────

    public static ClassStatProfile LightProfile() => new ClassStatProfile
    {
        speed             = 28f,
        jumpForce         = 6f,
        dashForce         = 25f,
        dashCooldown      = 0.7f,
        maxHealth         = 2,
        damageMultiplier  = 2.5f,
        critChance        = 0.20f,
        critDamagePercent = 0.15f,
        maxShieldHealth   = 0f,
        wallBouncePenalty = 0f
    };

    public static ClassStatProfile HealerProfile() => new ClassStatProfile
    {
        speed             = 20f,
        jumpForce         = 5f,
        dashForce         = 20f,
        dashCooldown      = 1f,
        maxHealth         = 4,
        damageMultiplier  = 1.2f,
        critChance        = 0f,
        critDamagePercent = 0f,
        maxShieldHealth   = 0f,
        wallBouncePenalty = 0f
    };

    public static ClassStatProfile TankProfile() => new ClassStatProfile
    {
        speed             = 14f,
        jumpForce         = 4f,
        dashForce         = 12f,
        dashCooldown      = 1.5f,
        maxHealth         = 5,
        damageMultiplier  = 1.8f,
        critChance        = 0f,
        critDamagePercent = 0f,
        maxShieldHealth   = 5f,
        wallBouncePenalty = 0.45f   // loses 45% extra velocity on wall hits
    };

    public static ClassStatProfile ForClass(PlayerClassType t)
    {
        switch (t)
        {
            case PlayerClassType.Light:  return LightProfile();
            case PlayerClassType.Healer: return HealerProfile();
            case PlayerClassType.Tank:   return TankProfile();
            default:                     return LightProfile();
        }
    }
}
