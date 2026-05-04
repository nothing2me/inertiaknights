using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Healer class abilities:
///   Q (hold) = Heal Beam — locks onto nearest ally, heals over time, fills Uber meter.
///   R        = AOE Heal Burst — heals all allies in radius (cooldown).
///   F        = Activate Uber (when meter full) — toggles between Teleport and Immortal modes.
///
/// Health-on-kill is handled by BallController checking the class type.
/// </summary>
public class HealerAbility : MonoBehaviour
{
    [Header("Heal Beam")]
    public float beamRange = 18f;
    public float healPerSecond = 2f;
    public float uberFillRate = 8f;  // meter units per second while healing
    public float maxUberMeter = 100f;
    public Color beamColor = new Color(0.3f, 1f, 0.5f, 0.7f);

    [Header("AOE Heal")]
    public float aoeHealAmount = 3f;
    public float aoeRadius = 12f;
    public float aoeCooldown = 10f;

    [Header("Uber — Immortal Mode")]
    public float immortalDuration = 10f;

    [Header("Uber — Teleport Mode")]
    public float teleportRange = 100f; // max range to teleport ally

    // ─── State ────────────────────────────────────────────────────
    private BallController ball;
    private Rigidbody rb;
    private LineRenderer beamLine;

    private bool isBeaming = false;
    private BallController beamTarget = null;
    private float uberMeter = 0f;
    private float lastAOETime = -100f;
    private bool uberReady = false;

    public enum UberMode { Immortal, Teleport }
    private UberMode currentUberMode = UberMode.Immortal;

    // ─── Input ────────────────────────────────────────────────────
    private InputAction beamAction;
    private InputAction aoeAction;
    private InputAction uberAction;
    private InputAction uberToggleAction;

    void OnEnable()
    {
        ball = GetComponent<BallController>();
        rb = GetComponent<Rigidbody>();

        // Create beam line renderer
        if (beamLine == null)
        {
            GameObject beamGO = new GameObject("HealBeam");
            beamGO.transform.SetParent(transform);
            beamLine = beamGO.AddComponent<LineRenderer>();
            beamLine.startWidth = 0.15f;
            beamLine.endWidth = 0.08f;
            beamLine.material = new Material(Shader.Find("Sprites/Default"));
            beamLine.startColor = beamColor;
            beamLine.endColor = beamColor;
            beamLine.positionCount = 2;
            beamLine.enabled = false;
        }

        beamAction       = new InputAction("HealBeam", InputActionType.Button, "<Keyboard>/q");
        aoeAction        = new InputAction("AOEHeal",  InputActionType.Button, "<Keyboard>/r");
        uberAction       = new InputAction("Uber",     InputActionType.Button, "<Keyboard>/f");
        uberToggleAction = new InputAction("UberMode", InputActionType.Button, "<Keyboard>/tab");

        beamAction.Enable();
        aoeAction.Enable();
        uberAction.Enable();
        uberToggleAction.Enable();
    }

    void OnDisable()
    {
        beamAction?.Disable();
        aoeAction?.Disable();
        uberAction?.Disable();
        uberToggleAction?.Disable();
        StopBeam();
    }

    void Update()
    {
        if (ball == null || !ball.IsOwner) return;

        // ── Heal Beam (hold Q) ──
        if (beamAction.IsPressed())
        {
            if (!isBeaming) StartBeam();
            UpdateBeam();
        }
        else if (isBeaming)
        {
            StopBeam();
        }

        // ── AOE Heal (R) ──
        if (aoeAction.WasPressedThisFrame() && Time.time >= lastAOETime + aoeCooldown)
        {
            PerformAOEHeal();
        }

        // ── Toggle Uber Mode (Tab) ──
        if (uberToggleAction.WasPressedThisFrame())
        {
            currentUberMode = currentUberMode == UberMode.Immortal
                ? UberMode.Teleport
                : UberMode.Immortal;
            Debug.Log($"[Healer] Uber mode set to: {currentUberMode}");
        }

        // ── Activate Uber (F) ──
        if (uberAction.WasPressedThisFrame() && uberReady)
        {
            ActivateUber();
        }

        // Update uber ready state
        uberReady = uberMeter >= maxUberMeter;

        // Sync uber meter to NetworkVariable for UI
        if (ball.IsOwner)
        {
            ball.SetUberMeterServerRpc(uberMeter);
        }
    }

    // ─── Heal Beam ────────────────────────────────────────────────

    private void StartBeam()
    {
        isBeaming = true;
        beamLine.enabled = true;
    }

    private void StopBeam()
    {
        isBeaming = false;
        beamTarget = null;
        if (beamLine != null) beamLine.enabled = false;
    }

    private void UpdateBeam()
    {
        // Find nearest ally
        BallController nearest = FindNearestAlly();

        if (nearest == null || Vector3.Distance(transform.position, nearest.transform.position) > beamRange)
        {
            beamTarget = null;
            beamLine.enabled = false;
            return;
        }

        beamTarget = nearest;
        beamLine.enabled = true;
        beamLine.SetPosition(0, transform.position);
        beamLine.SetPosition(1, beamTarget.transform.position);

        // Heal the target
        float healAmount = healPerSecond * Time.deltaTime;
        ball.HealTargetServerRpc(beamTarget.NetworkObjectId, healAmount);

        // Fill uber meter
        uberMeter = Mathf.Min(uberMeter + uberFillRate * Time.deltaTime, maxUberMeter);
    }

    // ─── AOE Heal ─────────────────────────────────────────────────

    private void PerformAOEHeal()
    {
        lastAOETime = Time.time;

        Collider[] hits = Physics.OverlapSphere(transform.position, aoeRadius);
        foreach (var hit in hits)
        {
            BallController ally = hit.GetComponent<BallController>();
            if (ally != null && ally != ball && ally.IsSpawned)
            {
                ball.HealTargetServerRpc(ally.NetworkObjectId, aoeHealAmount);
            }
        }

        Debug.Log("[Healer] AOE Heal burst!");
    }

    // ─── Uber ─────────────────────────────────────────────────────

    private void ActivateUber()
    {
        uberMeter = 0f;
        uberReady = false;

        if (currentUberMode == UberMode.Immortal)
        {
            // Grant immortality to self + beam target
            ulong targetId = beamTarget != null ? beamTarget.NetworkObjectId : 0;
            ball.ActivateImmortalUberServerRpc(targetId, immortalDuration);
            Debug.Log("[Healer] UBER ACTIVATED — IMMORTALITY!");
        }
        else // Teleport
        {
            if (beamTarget != null)
            {
                ball.TeleportAllyServerRpc(beamTarget.NetworkObjectId);
                Debug.Log("[Healer] UBER ACTIVATED — TELEPORT ALLY!");
            }
            else
            {
                Debug.Log("[Healer] No beam target to teleport!");
                uberMeter = maxUberMeter; // refund if no target
            }
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private BallController FindNearestAlly()
    {
        BallController[] players = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        BallController nearest = null;
        float closestDist = Mathf.Infinity;

        foreach (var p in players)
        {
            if (p == ball) continue; // skip self
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                nearest = p;
            }
        }

        return nearest;
    }

    /// <summary>Called by BallController when this player kills an enemy (health on kill).</summary>
    public void OnEnemyKilled()
    {
        if (ball != null && ball.IsOwner)
        {
            // Heal self for 1 HP on kill
            ball.HealSelfServerRpc(1f);
        }
    }

    void OnDestroy()
    {
        if (beamLine != null) Destroy(beamLine.gameObject);
    }
}
