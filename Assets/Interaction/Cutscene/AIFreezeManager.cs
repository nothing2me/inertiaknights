using UnityEngine;

/// <summary>
/// Static helper that sets isFrozen on all active EnemyPlayer and BossController instances.
/// Effective on the host/server where AI FixedUpdate actually runs.
/// CutscenePlayer.Cleanup() calls UnfreezeAll() as a safety net.
/// </summary>
public static class AIFreezeManager
{
    public static void FreezeAll()
    {
        foreach (var e in Object.FindObjectsByType<EnemyPlayer>(FindObjectsSortMode.None))
            e.isFrozen = true;
        foreach (var b in Object.FindObjectsByType<BossController>(FindObjectsSortMode.None))
            b.isFrozen = true;
    }

    public static void UnfreezeAll()
    {
        foreach (var e in Object.FindObjectsByType<EnemyPlayer>(FindObjectsSortMode.None))
            e.isFrozen = false;
        foreach (var b in Object.FindObjectsByType<BossController>(FindObjectsSortMode.None))
            b.isFrozen = false;
    }
}
