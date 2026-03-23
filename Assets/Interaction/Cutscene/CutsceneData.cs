using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCutscene", menuName = "Interaction/CutsceneData")]
public class CutsceneData : ScriptableObject
{
    [Tooltip("Must be unique across the project — used as the network key for group broadcasts.")]
    public string cutsceneName;

    [Tooltip("Steps executed in order. Right-click to add step types via the custom editor.")]
    [SerializeReference]
    public List<CutsceneStep> steps = new List<CutsceneStep>();
}
