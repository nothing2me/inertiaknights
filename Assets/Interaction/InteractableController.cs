using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

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
        if (_localPlayer == null) 
        {
            // Only log once to avoid console spam
            if (Time.frameCount % 1000 == 0)
                Debug.LogWarning($"[InteractableController] {name}: Local player (BallController) not found. Prompt will not show.");
            return;
        }

        float dist = Vector3.Distance(transform.position, _localPlayer.transform.position);
        bool nowInRange = dist <= data.interactRadius;

        if (nowInRange != _inRange)
        {
            _inRange = nowInRange;
            if (_inRange)
            {
                if (InteractPromptHUD.Instance == null)
                {
                    Debug.Log("[InteractableController] InteractPromptHUD missing at runtime. Attempting to create a temporary one...");
                    CreateRuntimeHUD();
                }

                if (InteractPromptHUD.Instance != null)
                {
                    Debug.Log($"[InteractableController] Showing prompt: {data.promptText}");
                    InteractPromptHUD.Instance.Show(data.promptText);
                }
                else
                {
                    Debug.LogWarning("[InteractableController] Failed to create or find InteractPromptHUD. Prompt cannot be shown.");
                }
            }
            else
            {
                InteractPromptHUD.Instance?.Hide();
            }
        }

        // Trigger interaction on press
        bool inputTriggered = (_localPlayer.interactAction != null && _localPlayer.interactAction.triggered);
        
        // Fallback: Check hardcoded E key if the action isn't triggering
        if (!inputTriggered && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            inputTriggered = true;

        if (_inRange && inputTriggered)
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
        Debug.Log($"[InteractableController] {name} Trigger() called for {data.type}");
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
                // Hide the prompt once we interact
                InteractPromptHUD.Instance?.Hide();
                // Load the scene specified in the InteractionData additively
                string sceneName = string.IsNullOrEmpty(data.storeSceneName) ? "StoreScene" : data.storeSceneName;
                SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
                // Lock the player's movement while the store is open
                if (_localPlayer != null) _localPlayer.movementLocked = true;
                break;

            case InteractionType.DialogueTree:
                DialogueUI.Instance?.ShowDialogueTree(data.jsonDialogueTree, null);
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

    private void CreateRuntimeHUD()
    {
        // This creates a basic fallback HUD if the designer hasn't placed one in the scene
        GameObject canvasGO = new GameObject("RuntimeInteractionCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        GameObject panelGO = new GameObject("InteractPromptPanel", typeof(RectTransform));
        panelGO.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.2f);
        panelRT.anchorMax = new Vector2(0.5f, 0.2f);
        panelRT.sizeDelta = new Vector2(400, 50);

        GameObject labelGO = new GameObject("PromptLabel", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        labelGO.transform.SetParent(panelGO.transform, false);
        TMPro.TextMeshProUGUI label = labelGO.GetComponent<TMPro.TextMeshProUGUI>();
        label.text = "Press E to Interact";
        label.fontSize = 24;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.color = Color.white;

        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.sizeDelta = Vector2.zero;

        InteractPromptHUD hud = canvasGO.AddComponent<InteractPromptHUD>();
        
        // Use reflection to set private fields since we are at runtime and don't want to mess with SerializedObjects
        var fieldPanel = typeof(InteractPromptHUD).GetField("panel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fieldLabel = typeof(InteractPromptHUD).GetField("label", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (fieldPanel != null) fieldPanel.SetValue(hud, panelGO);
        if (fieldLabel != null) fieldLabel.SetValue(hud, label);
    }

    private static BallController FindLocalPlayer()
    {
        foreach (var b in Object.FindObjectsByType<BallController>(FindObjectsSortMode.None))
            if (b.IsOwner) return b;
        
        // Fallback for single-player editor testing if Netcode isn't started
        var fallback = Object.FindFirstObjectByType<BallController>();
        return fallback;
    }

    private void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.interactRadius);
    }
}
