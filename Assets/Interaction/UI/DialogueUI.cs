using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton dialogue canvas.
/// ShowLine() starts a typewriter effect. Player presses interactAction to skip or confirm.
/// First press skips to full text. Second press calls onConfirm and hides the panel.
/// Place on a DontDestroyOnLoad Canvas in the scene.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [Header("References — wire these in the Inspector")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private GameObject continuePrompt; // e.g. "▼ Press E"
    [SerializeField] private Transform choicesContainer;
    [SerializeField] private GameObject choiceButtonPrefab;

    [Header("Typewriter")]
    [SerializeField] private float charsPerSecond = 30f;

    private DialogueTree _currentTree;
    private DialogueNode _currentNode;

    private Coroutine _typewriterCoroutine;
    private string _currentFullText;
    private bool _typewriterDone;
    private Action _onConfirm;
    private bool _isShowing;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
        else
            Debug.LogWarning($"[DialogueUI] {name} is not a root object! DontDestroyOnLoad skipped.");

        // --- Auto-configure Canvas Scaling ---
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // --- Auto-center the Dialogue Panel at the bottom ---
        if (panel != null)
        {
            RectTransform rt = panel.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0f); // Bottom center
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f); // Bottom center pivot
                rt.anchoredPosition = new Vector2(0, 40); // 40 units of padding from the bottom edge
                // Provide a reasonably large default size
                rt.sizeDelta = new Vector2(1000, 300); 
            }

            // --- Rounded Corners and Stroke ---
            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.type = Image.Type.Sliced;
                // Generate a 128x128 rounded rect with 32px radius and 4px stroke
                panelImage.sprite = CreateRoundedStrokeSprite(128, 128, 32, 4, new Color(0.1f, 0.1f, 0.1f, 0.95f), Color.white);
                panelImage.color = Color.white; // Set to white so we see our exact generated colors
            }
        }

        // --- Ensure Continue Prompt is Inside the Frame ---
        if (continuePrompt != null && panel != null)
        {
            RectTransform promptRt = continuePrompt.GetComponent<RectTransform>();
            if (promptRt != null)
            {
                promptRt.SetParent(panel.transform, false);
                promptRt.anchorMin = new Vector2(1f, 0f); // Bottom right
                promptRt.anchorMax = new Vector2(1f, 0f);
                promptRt.pivot = new Vector2(1f, 0f);
                promptRt.anchoredPosition = new Vector2(-30, 20); // Pad from the bottom right corner
            }
            
            // Make the prompt text larger as requested
            TextMeshProUGUI promptTMP = continuePrompt.GetComponentInChildren<TextMeshProUGUI>();
            if (promptTMP != null)
            {
                // Force a larger sizeDelta so the text has room to be big
                RectTransform promptRT = promptTMP.GetComponent<RectTransform>();
                if (promptRT != null) promptRT.sizeDelta = new Vector2(400, 100);

                promptTMP.enableAutoSizing = false; // Stop it from shrinking to fit a tiny prefab box
                promptTMP.fontSize = 36;            // Even larger now
                promptTMP.color = Color.yellow; 
            }
        }

        panel?.SetActive(false);
    }

    /// <summary>Display a line and call onConfirm when the player confirms it.</summary>
    public void ShowLine(DialogueLine line, Action onConfirm)
    {
        _onConfirm = onConfirm;
        _currentFullText = line.text;
        _typewriterDone = false;
        _isShowing = true;

        panel?.SetActive(true);
        continuePrompt?.SetActive(false);

        if (speakerNameText != null) speakerNameText.text = line.speakerName;
        if (portraitImage != null)
        {
            portraitImage.sprite = line.portrait;
            portraitImage.gameObject.SetActive(line.portrait != null);
        }

        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        _typewriterCoroutine = StartCoroutine(TypewriterRoutine());
    }

    /// <summary>Starts a branching dialogue from a JSON TextAsset.</summary>
    public void ShowDialogueTree(TextAsset jsonAsset, Action onConfirm)
    {
        if (jsonAsset == null) return;
        _currentTree = JsonUtility.FromJson<DialogueTree>(jsonAsset.text);
        if (_currentTree == null || _currentTree.nodes == null || _currentTree.nodes.Count == 0) return;

        // Find the start node
        _currentNode = _currentTree.nodes.Find(n => n.id == "start") ?? _currentTree.nodes[0];
        
        DisplayNode(_currentNode, onConfirm);
    }

    private void DisplayNode(DialogueNode node, Action onConfirm)
    {
        _currentNode = node;
        DialogueLine line = new DialogueLine()
        {
            speakerName = node.speakerName,
            text = node.text
        };
        ShowLine(line, onConfirm);
    }

    private void ShowChoices()
    {
        if (choicesContainer == null || choiceButtonPrefab == null) return;
        if (_currentNode == null || _currentNode.choices == null || _currentNode.choices.Count == 0) return;

        continuePrompt?.SetActive(false);

        foreach (var choice in _currentNode.choices)
        {
            GameObject btnObj = Instantiate(choiceButtonPrefab, choicesContainer);
            var btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = choice.text;

            var btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                // Capture the choice locally for the lambda
                string nextId = choice.nextNodeId;
                btn.onClick.AddListener(() => SelectChoice(nextId));
            }
        }
    }

    private void SelectChoice(string nextNodeId)
    {
        // Clear choices
        foreach (Transform child in choicesContainer) Destroy(child.gameObject);

        if (string.IsNullOrEmpty(nextNodeId))
        {
            Hide();
            return;
        }

        DialogueNode nextNode = _currentTree.nodes.Find(n => n.id == nextNodeId);
        if (nextNode != null)
        {
            DisplayNode(nextNode, _onConfirm);
        }
        else
        {
            Hide();
        }
    }

    /// <summary>Force-hide the panel (called by CutscenePlayer.Cleanup).</summary>
    public void Hide()
    {
        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        panel?.SetActive(false);
        _isShowing = false;
        _onConfirm = null;
    }

    /// <summary>
    /// Called when the player presses the interact key while a line is showing.
    /// First call: skip typewriter to full text.
    /// Second call: confirm and hide.
    /// </summary>
    public void Confirm()
    {
        if (!_isShowing) return;

        if (!_typewriterDone)
        {
            // Skip to full text
            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
            if (bodyText != null) bodyText.text = _currentFullText;
            _typewriterDone = true;

            if (_currentNode != null && _currentNode.choices != null && _currentNode.choices.Count > 0)
                ShowChoices();
            else
                continuePrompt?.SetActive(true);

            return;
        }

        // If we have choices, we MUST pick one; Confirm key does nothing
        if (_currentNode != null && _currentNode.choices != null && _currentNode.choices.Count > 0)
            return;

        // Advance to next line
        var cb = _onConfirm;
        _onConfirm = null;
        _isShowing = false;
        panel?.SetActive(false);
        cb?.Invoke();
    }

    private IEnumerator TypewriterRoutine()
    {
        if (bodyText == null) { _typewriterDone = true; yield break; }

        bodyText.text = "";
        float interval = charsPerSecond > 0 ? 1f / charsPerSecond : 0f;

        foreach (char c in _currentFullText)
        {
            bodyText.text += c;
            yield return new WaitForSeconds(interval);
        }

        _typewriterDone = true;
        
        if (_currentNode != null && _currentNode.choices != null && _currentNode.choices.Count > 0)
            ShowChoices();
        else
            continuePrompt?.SetActive(true);
    }

    private void Update()
    {
        // Route interact key from the local player to Confirm()
        if (!_isShowing) return;

        // Fallback: Check hardcoded E key in case the Input Action isn't bound correctly
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
        {
            Confirm();
            return;
        }

        foreach (var b in FindObjectsByType<BallController>(FindObjectsSortMode.None))
        {
            if (b.IsOwner && b.interactAction != null && b.interactAction.triggered)
            {
                Confirm();
                break;
            }
        }
    }

    private Sprite CreateRoundedStrokeSprite(int width, int height, int radius, int strokeWidth, Color fillColor, Color strokeColor)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Max(0, radius - x, x - (width - radius));
                float dy = Mathf.Max(0, radius - y, y - (height - radius));
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > radius) pixels[y * width + x] = Color.clear;
                else if (dist > radius - strokeWidth) pixels[y * width + x] = strokeColor;
                else pixels[y * width + x] = fillColor;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}
