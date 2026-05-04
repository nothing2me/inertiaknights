using System.Collections;

/// <summary>
/// Abstract base class for all cutscene steps.
/// Subclasses are serialized inline in CutsceneData via [SerializeReference].
/// Each step receives a CutsceneContext containing the local player, camera, and named scene points.
/// </summary>
[System.Serializable]
public abstract class CutsceneStep
{
    public abstract IEnumerator Execute(CutsceneContext ctx);
}
