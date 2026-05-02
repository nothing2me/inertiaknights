using TMPro;
using UnityEngine;

/// <summary>
/// Singleton HUD overlay for the "Press E to interact" prompt.
/// Show() is called by InteractableController when the player is in range.
/// Hide() is called when the player leaves range or after triggering.
/// Place on a DontDestroyOnLoad Canvas in the scene.
/// </summary>
public class InteractPromptHUD : MonoBehaviour
{
    public static InteractPromptHUD Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI label;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

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

        // --- Auto-center Prompt near the bottom ---
        if (panel != null)
        {
            RectTransform rt = panel.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.2f); // bottom center
                rt.anchorMax = new Vector2(0.5f, 0.2f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        panel?.SetActive(false);
    }

    public void Show(string text)
    {
        if (label != null) label.text = text;
        panel?.SetActive(true);
    }

    public void Hide()
    {
        panel?.SetActive(false);
    }
}
