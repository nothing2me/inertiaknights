using System.Collections;
using UnityEngine;

/// <summary>
/// Freezes or unfreezes all active AI (EnemyPlayer and BossController) in the scene.
/// This works on the host/server (where AI FixedUpdate actually runs).
/// On pure clients the flag is set but has no effect since AI has if (!IsServer) return.
/// Always pair freeze=true with freeze=false, or rely on CutscenePlayer.Cleanup() as a safety net.
/// </summary>
[System.Serializable]
public class FreezeAIStep : CutsceneStep
{
    [Tooltip("True = freeze all AI. False = unfreeze.")]
    public bool freeze = true;

    public override IEnumerator Execute(CutsceneContext ctx)
    {
        if (freeze)
            AIFreezeManager.FreezeAll();
        else
            AIFreezeManager.UnfreezeAll();

        yield break;
    }
}
