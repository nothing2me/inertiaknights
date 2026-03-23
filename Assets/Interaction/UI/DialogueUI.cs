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

    [Header("Typewriter")]
    [SerializeField] private float charsPerSecond = 30f;

    private Coroutine _typewriterCoroutine;
    private string _currentFullText;
    private bool _typewriterDone;
    private Action _onConfirm;
    private bool _isShowing;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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
            continuePrompt?.SetActive(true);
            return;
        }

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
        continuePrompt?.SetActive(true);
    }

    private void Update()
    {
        // Route interact key from the local player to Confirm()
        if (!_isShowing) return;
        foreach (var b in FindObjectsByType<BallController>(FindObjectsSortMode.None))
        {
            if (b.IsOwner && b.interactAction.triggered)
            {
                Confirm();
                break;
            }
        }
    }
}
