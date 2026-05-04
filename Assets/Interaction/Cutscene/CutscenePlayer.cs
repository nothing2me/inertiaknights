using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton coroutine runner for cutscenes.
/// Executes CutsceneData steps sequentially.
/// Guarantees cleanup (player unlock, camera restore, AI unfreeze) even if a step throws.
/// Place on a persistent GameObject in the scene.
/// </summary>
public class CutscenePlayer : MonoBehaviour
{
    public static CutscenePlayer Instance { get; private set; }
    public bool IsPlaying { get; private set; }

    public event Action<CutsceneData> OnCutsceneStart;
    public event Action<CutsceneData> OnCutsceneEnd;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Play a cutscene with a pre-built context (solo path).</summary>
    public void Play(CutsceneData data, CutsceneContext ctx)
    {
        if (data == null) { Debug.LogError("[CutscenePlayer] CutsceneData is null."); return; }
        if (IsPlaying)
        {
            Debug.LogWarning($"[CutscenePlayer] Already playing. Ignoring '{data.cutsceneName}'.");
            return;
        }

        // Warn if group cutscene contains non-auto-advance dialogue (will desync clients)
        if (data.steps != null)
        {
            foreach (var step in data.steps)
            {
                if (step is DialogueStep ds && !ds.line.autoAdvance)
                    Debug.LogWarning($"[CutscenePlayer] '{data.cutsceneName}' has a non-auto-advance DialogueStep. This WILL desync group listeners.");
            }
        }

        StartCoroutine(RunCutscene(data, ctx));
    }

    /// <summary>Play a cutscene by name — used by group broadcast ClientRpc path.</summary>
    public void PlayByName(string cutsceneName)
    {
        var data = CutsceneRegistry.Find(cutsceneName);
        if (data == null) return;

        BallController localPlayer = null;
        foreach (var b in FindObjectsByType<BallController>(FindObjectsSortMode.None))
            if (b.IsOwner) { localPlayer = b; break; }

        if (localPlayer == null)
        {
            Debug.LogError("[CutscenePlayer] PlayByName: no local player found.");
            return;
        }

        Play(data, new CutsceneContext(localPlayer, null));
    }

    private IEnumerator RunCutscene(CutsceneData data, CutsceneContext ctx)
    {
        IsPlaying = true;
        OnCutsceneStart?.Invoke(data);

        yield return RunSteps(data, ctx);

        // Always runs — the safety net
        Cleanup(ctx);
        IsPlaying = false;
        OnCutsceneEnd?.Invoke(data);
    }

    private IEnumerator RunSteps(CutsceneData data, CutsceneContext ctx)
    {
        if (data.steps == null) yield break;

        foreach (var step in data.steps)
        {
            if (step == null) continue;

            IEnumerator exec = null;
            try { exec = step.Execute(ctx); }
            catch (Exception e) { Debug.LogError($"[CutscenePlayer] Step setup error: {e}"); continue; }

            // Manual MoveNext loop so we can catch per-step exceptions
            while (true)
            {
                bool hasNext = false;
                try { hasNext = exec.MoveNext(); }
                catch (Exception e) { Debug.LogError($"[CutscenePlayer] Step runtime error: {e}"); break; }
                if (!hasNext) break;
                yield return exec.Current;
            }
        }
    }

    private void Cleanup(CutsceneContext ctx)
    {
        if (ctx?.player != null)
        {
            ctx.player.isCutsceneLocked = false;
            ctx.player.moveAction.Enable();
            ctx.player.jumpAction.Enable();
            ctx.player.dashAction.Enable();
            ctx.player.groundSlamAction.Enable();
            ctx.player.attackAction.Enable();
            ctx.player.brakeAction.Enable();
        }

        if (ctx?.camera != null && ctx.player != null)
        {
            ctx.camera.SetTarget(ctx.player.transform);
            ctx.camera.SnapToTarget();
        }

        DialogueUI.Instance?.Hide();
        AIFreezeManager.UnfreezeAll();
    }
}
