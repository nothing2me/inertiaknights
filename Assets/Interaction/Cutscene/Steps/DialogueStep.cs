using System.Collections;
using UnityEngine;

/// <summary>
/// Shows a single line of dialogue via DialogueUI.
/// Set autoAdvance=true for group cutscenes to keep all clients in sync.
/// </summary>
[System.Serializable]
public class DialogueStep : CutsceneStep
{
    public DialogueLine line;

    public override IEnumerator Execute(CutsceneContext ctx)
    {
        bool confirmed = false;
        DialogueUI.Instance?.ShowLine(line, () => confirmed = true);

        if (line.autoAdvance)
            yield return new WaitForSeconds(Mathf.Max(0.1f, line.autoAdvanceDelay));
        else
            yield return new WaitUntil(() => confirmed);
    }
}
