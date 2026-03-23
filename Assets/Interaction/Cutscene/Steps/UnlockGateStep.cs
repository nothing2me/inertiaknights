using System.Collections;
using UnityEngine;

/// <summary>
/// Unlocks a BossGate via ServerRpc (works from any client).
/// Find the gate by its GameObject name or tag.
/// </summary>
[System.Serializable]
public class UnlockGateStep : CutsceneStep
{
    [Tooltip("Exact name of the BossGate GameObject. Checked first.")]
    public string gateName;
    [Tooltip("Tag of the BossGate GameObject. Used if gateName is empty.")]
    public string gateTag;

    public override IEnumerator Execute(CutsceneContext ctx)
    {
        BossGate gate = FindGate();
        if (gate == null)
        {
            Debug.LogWarning($"[UnlockGateStep] BossGate not found (name='{gateName}', tag='{gateTag}').");
            yield break;
        }

        gate.UnlockServerRpc();
    }

    private BossGate FindGate()
    {
        if (!string.IsNullOrEmpty(gateName))
        {
            var go = GameObject.Find(gateName);
            if (go != null) return go.GetComponent<BossGate>();
        }
        if (!string.IsNullOrEmpty(gateTag))
        {
            var go = GameObject.FindGameObjectWithTag(gateTag);
            if (go != null) return go.GetComponent<BossGate>();
        }
        return null;
    }
}
