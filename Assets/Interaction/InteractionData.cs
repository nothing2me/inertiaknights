using UnityEngine;
using UnityEngine.Events;

public enum InteractionType { Dialogue, Shop, Cutscene, Custom }
public enum InteractionMode { PressToInteract, AutoDetect }

[System.Serializable]
public struct DialogueLine
{
    public string speakerName;
    public Sprite portrait;
    [TextArea(2, 5)] public string text;
    public bool autoAdvance;
    public float autoAdvanceDelay;
}

[CreateAssetMenu(fileName = "NewInteractionData", menuName = "Interaction/InteractionData")]
public class InteractionData : ScriptableObject
{
    [Header("What Happens")]
    public InteractionType type = InteractionType.Dialogue;

    [Header("How It Triggers")]
    public InteractionMode mode = InteractionMode.PressToInteract;
    [Tooltip("Radius for both the proximity prompt and the AutoDetect sphere collider.")]
    public float interactRadius = 3f;
    [Tooltip("Only layers in this mask can trigger AutoDetect. Set to Player layer.")]
    public LayerMask triggerLayer;
    [Tooltip("Shown only in PressToInteract mode.")]
    public string promptText = "Press E to interact";

    [Header("Multiplayer")]
    [Tooltip("Broadcasts this cutscene to ALL connected clients via server RPC.")]
    public bool isGroupCutscene = false;

    [Header("Behaviour")]
    [Tooltip("Disables this interactable after first use.")]
    public bool oneShot = false;

    [Header("Content — Dialogue (quick, no CutsceneData needed)")]
    public DialogueLine[] dialogueLines;

    [Header("Content — Cutscene")]
    public CutsceneData cutscene;

    [Header("Content — Custom")]
    public UnityEvent onCustomInteract;
}
