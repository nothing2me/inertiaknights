using System.Collections;
using UnityEngine.Events;

/// <summary>
/// Fires a UnityEvent from within a cutscene.
/// Use this to hook into any custom game logic without writing a new step type.
/// NOTE: UnityEvent targets must be scene objects — asset references are not supported by UnityEvent.
/// </summary>
[System.Serializable]
public class UnityEventStep : CutsceneStep
{
    public UnityEvent callback;

    public override IEnumerator Execute(CutsceneContext ctx)
    {
        callback?.Invoke();
        yield break;
    }
}
