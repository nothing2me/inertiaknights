using System.Collections;
using UnityEngine;

[System.Serializable]
public class WaitStep : CutsceneStep
{
    [Tooltip("Seconds to pause before executing the next step.")]
    public float duration = 1f;

    public override IEnumerator Execute(CutsceneContext ctx)
    {
        yield return new WaitForSeconds(duration);
    }
}
