using System.Collections;
using UnityEngine;

/// <summary>
/// Locks or unlocks the local player's movement input actions.
/// Always pair a lock=true step with a lock=false step.
/// CutscenePlayer.Cleanup() will force-unlock as a safety net if the cutscene ends early.
/// </summary>
[System.Serializable]
public class LockPlayerStep : CutsceneStep
{
    [Tooltip("True = disable input (lock player). False = re-enable input (unlock player).")]
    public bool lockPlayer = true;

    public override IEnumerator Execute(CutsceneContext ctx)
    {
        if (ctx?.player == null) yield break;

        ctx.player.isCutsceneLocked = lockPlayer;

        if (lockPlayer)
        {
            ctx.player.moveAction.Disable();
            ctx.player.jumpAction.Disable();
            ctx.player.dashAction.Disable();
            ctx.player.groundSlamAction.Disable();
            ctx.player.attackAction.Disable();
            ctx.player.brakeAction.Disable();
        }
        else
        {
            ctx.player.moveAction.Enable();
            ctx.player.jumpAction.Enable();
            ctx.player.dashAction.Enable();
            ctx.player.groundSlamAction.Enable();
            ctx.player.attackAction.Enable();
            ctx.player.brakeAction.Enable();
        }
    }
}
