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
