using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to any GameObject (NPC, enemy, door, pickup, etc.) to make it interactable.
/// Drop an InteractionData ScriptableObject into the 'data' slot to define what happens.
/// 
/// PressToInteract: shows a prompt HUD when in range; fires on the local player's interactAction.
/// AutoDetect:      creates a trigger SphereCollider at runtime; fires automatically on entry (IsOwner filtered).
///
/// For group cutscenes, this calls GroupCutsceneBroadcaster → ServerRpc → ClientRpc on all clients.
/// </summary>
public class InteractableController : MonoBehaviour
{
    [SerializeField] public InteractionData data;

    private bool _usedOneShot = false;
    private bool _initialized = false;
    private BallController _localPlayer;
    private bool _inRange = false;

    private void Start()
    {
        if (data == null)
        {
            Debug.LogWarning($"[InteractableController] {name}: No InteractionData assigned.");
            return;
        }

        if (data.mode == InteractionMode.AutoDetect)
        {
            var col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = data.interactRadius;
            // Layer filtering is done in code (IsOwner check) for reliability
        }

        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized || data == null) return;
        if (data.mode != InteractionMode.PressToInteract) return;
        if (_usedOneShot) return;

        if (_localPlayer == null) _localPlayer = FindLocalPlayer();
        if (_localPlayer == null) return;

        float dist = Vector3.Distance(transform.position, _localPlayer.transform.position);
        bool nowInRange = dist <= data.interactRadius;

        if (nowInRange != _inRange)
        {
            _inRange = nowInRange;
            if (_inRange)
                InteractPromptHUD.Instance?.Show(data.promptText);
            else
                InteractPromptHUD.Instance?.Hide();
        }

        if (_inRange && _localPlayer.interactAction.triggered)
            Trigger();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (data == null || data.mode != InteractionMode.AutoDetect) return;
        if (_usedOneShot) return;

        // Only fire for the local, owning player — not enemies, remote players, or physics objects
        BallController ball = other.GetComponent<BallController>();
        if (ball == null || !ball.IsOwner) return;

        _localPlayer = ball;
        Trigger();
    }

    private void OnDisable()
    {
        // Hide prompt if we get disabled while the player is in range
        if (_inRange) InteractPromptHUD.Instance?.Hide();
        _inRange = false;
    }

    private void Trigger()
    {
        if (data.oneShot)
        {
            _usedOneShot = true;
            InteractPromptHUD.Instance?.Hide();
            _inRange = false;
        }

        switch (data.type)
        {
            case InteractionType.Dialogue:
                StartCoroutine(PlayQuickDialogue());
                break;

            case InteractionType.Cutscene:
                PlayCutscene();
                break;

            case InteractionType.Shop:
                Debug.Log("[InteractableController] Shop type — not yet implemented.");
                break;

            case InteractionType.Custom:
                data.onCustomInteract?.Invoke();
                break;
        }
    }

    private IEnumerator PlayQuickDialogue()
    {
        if (_localPlayer == null || data.dialogueLines == null) yield break;
        _localPlayer.interactAction.Disable();

        foreach (var line in data.dialogueLines)
        {
            bool confirmed = false;
            DialogueUI.Instance?.ShowLine(line, () => confirmed = true);

            if (line.autoAdvance)
                yield return new WaitForSeconds(Mathf.Max(0.1f, line.autoAdvanceDelay));
            else
                yield return new WaitUntil(() => confirmed);
        }

        DialogueUI.Instance?.Hide();
        _localPlayer.interactAction.Enable();
    }

    private void PlayCutscene()
    {
        if (data.cutscene == null)
        {
            Debug.LogError($"[InteractableController] {name}: InteractionData.type is Cutscene but no CutsceneData assigned.");
            return;
        }

        if (data.isGroupCutscene)
        {
            if (GroupCutsceneBroadcaster.Instance == null)
                Debug.LogError("[InteractableController] GroupCutsceneBroadcaster not found in scene!");
            else
                GroupCutsceneBroadcaster.Instance.RequestGroupCutscene(data.cutscene.cutsceneName);
        }
        else
        {
            if (CutscenePlayer.Instance == null)
                Debug.LogError("[InteractableController] CutscenePlayer not found in scene!");
            else
                CutscenePlayer.Instance.Play(data.cutscene, BuildContext());
        }
    }

    private CutsceneContext BuildContext()
    {
        if (_localPlayer == null) _localPlayer = FindLocalPlayer();
        return new CutsceneContext(_localPlayer, transform);
    }

    private static BallController FindLocalPlayer()
    {
        foreach (var b in Object.FindObjectsByType<BallController>(FindObjectsSortMode.None))
            if (b.IsOwner) return b;
        return null;
    }
}
