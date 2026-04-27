using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Lives on the Ball prefab. After network spawn it shows the class-selection GUI,
/// applies the chosen stat profile to BallController, and enables the matching
/// ability component. All heavy networking (NetworkVariables / RPCs) lives on
/// BallController so this can be a plain MonoBehaviour added at runtime.
/// </summary>
public class PlayerClassManager : MonoBehaviour
{
    // ─── Cached references ────────────────────────────────────────
    private BallController ball;
    private ClassSelectionUI selectionUI;

    // ─── Ability components (added dynamically) ───────────────────
    private GrappleAbility   grapple;
    private HealerAbility    healer;
    private TankAbility      tank;

    // ─── State ────────────────────────────────────────────────────
    public  PlayerClassType  ActiveClass { get; private set; } = PlayerClassType.None;
    public  bool             IsClassChosen => ActiveClass != PlayerClassType.None;
    private bool             initialized = false;

    // ─── Public API ───────────────────────────────────────────────

    /// <summary>Called by ClassSelectionUI when the player clicks a class card.</summary>
    public void SelectClass(PlayerClassType classType, bool spawnBots = false)
    {
        if (IsClassChosen) return; // one-shot
        ActiveClass = classType;

        // Tell server
        ball.SetPlayerClassServerRpc((int)classType);

        if (spawnBots && ball.IsOwner)
        {
            ball.SpawnBotTeamServerRpc((int)classType);
        }

        // Apply stats
        ClassStatProfile profile = ClassStatProfile.ForClass(classType);
        ApplyProfile(profile);

        // Enable the right ability component
        switch (classType)
        {
            case PlayerClassType.Light:
                grapple.enabled = true;
                break;
            case PlayerClassType.Healer:
                healer.enabled = true;
                break;
            case PlayerClassType.Tank:
                tank.enabled = true;
                break;
        }

        // Unlock movement
        ball.movementLocked = false;

        // Hide the selection UI
        if (selectionUI != null)
        {
            selectionUI.Hide();
        }

        Debug.Log($"[ClassSystem] Selected class: {classType}");
    }

    // ─── Lifecycle ────────────────────────────────────────────────

    public void Initialize(BallController controller)
    {
        if (initialized) return;
        initialized = true;
        ball = controller;

        // Lock movement until class is chosen
        ball.movementLocked = true;

        // Add ability components (all start disabled)
        grapple = gameObject.AddComponent<GrappleAbility>();
        grapple.enabled = false;

        healer = gameObject.AddComponent<HealerAbility>();
        healer.enabled = false;

        tank = gameObject.AddComponent<TankAbility>();
        tank.enabled = false;

        // Create the selection UI (only for local human player)
        if (ball.IsOwner && ball.NetworkObject.IsPlayerObject)
        {
            StartCoroutine(ShowSelectionUIDelayed());
        }
        else if (!ball.NetworkObject.IsPlayerObject)
        {
            // Unlock movement immediately for bots
            ball.movementLocked = false;
        }
    }

    private IEnumerator ShowSelectionUIDelayed()
    {
        // Wait a beat so the scene settles (camera, spawn, etc.)
        yield return new WaitForSeconds(0.5f);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ClassSystem] No Canvas found in scene for class selection UI!");
            yield break;
        }

        GameObject uiGO = new GameObject("ClassSelectionUI");
        uiGO.transform.SetParent(canvas.transform, false);
        selectionUI = uiGO.AddComponent<ClassSelectionUI>();
        selectionUI.Initialize(this);
    }

    // ─── Stat Application ─────────────────────────────────────────

    private void ApplyProfile(ClassStatProfile p)
    {
        ball.speed            = p.speed;
        ball.jumpForce        = p.jumpForce;
        ball.dashForce        = p.dashForce;
        ball.dashCooldown     = p.dashCooldown;
        ball.maxHealth        = p.maxHealth;
        ball.damageMultiplier = p.damageMultiplier;

        // Light crit
        ball.critChance        = p.critChance;
        ball.critDamagePercent = p.critDamagePercent;

        // Light rescue dash
        ball.hasRescueDash = (ActiveClass == PlayerClassType.Light);

        // Tank shield
        ball.maxShieldHealth = p.maxShieldHealth;
        ball.wallBouncePenalty = p.wallBouncePenalty;
        if (p.maxShieldHealth > 0f && ball.IsOwner)
        {
            ball.SetShieldHealthServerRpc(p.maxShieldHealth);
        }

        // Reset health to new max
        if (ball.IsOwner)
        {
            ball.ResetHealthToMaxServerRpc(p.maxHealth);
        }
    }

    void OnDestroy()
    {
        if (selectionUI != null)
            Destroy(selectionUI.gameObject);
    }
}
