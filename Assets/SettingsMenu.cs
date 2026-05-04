using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class SettingsMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject settingsPanel;

    private bool isSettingsActive = false;
    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    void Start()
    {
        if (settingsPanel != null)
        {
            // Temporarily activate so TextMeshPro can generate its atlas without corrupting
            settingsPanel.SetActive(true);

            // Rebuild settings panel to just show controls
            RebuildSettingsPanel();

            // Force TMP to bake the text immediately
            var tmp = settingsPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.ForceMeshUpdate(true, true);

            // Ensure CanvasGroup exists for fading
            canvasGroup = settingsPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = settingsPanel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            settingsPanel.SetActive(false);
        }
    }

    void Update()
    {
        // Toggle settings menu with Escape key
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ToggleSettingsMenu();
    }

    public void ToggleSettingsMenu()
    {
        if (settingsPanel == null) return;

        isSettingsActive = !isSettingsActive;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        // Manage cursor state and timescale immediately
        if (isSettingsActive)
        {
            settingsPanel.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            fadeCoroutine = StartCoroutine(FadeMenu(1f));
        }
        else
        {
            Time.timeScale = 1f;
            
            // If we are not in the main menu, hide the cursor when settings are closed
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu")
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            fadeCoroutine = StartCoroutine(FadeMenu(0f));
        }
    }

    private System.Collections.IEnumerator FadeMenu(float targetAlpha)
    {
        if (canvasGroup == null) yield break;

        // Toggle raycasts immediately
        if (targetAlpha > 0f)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        float startAlpha = canvasGroup.alpha;
        float duration = 0.2f;
        float time = 0f;

        while (time < duration)
        {
            // Crucial: Use unscaledDeltaTime because Time.timeScale might be 0!
            time += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        if (targetAlpha == 0f)
            settingsPanel.SetActive(false);
    }

    private void RebuildSettingsPanel()
    {
        // 1. Destroy existing graphical toggles or sliders to ensure NO graphics function
        var toggles = settingsPanel.GetComponentsInChildren<UnityEngine.UI.Toggle>(true);
        foreach (var t in toggles)
        {
            Destroy(t.gameObject);
        }

        // Add a completely opaque black background first so it sits behind the text
        GameObject bgObj = new GameObject("PauseOpaqueBackground", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        bgObj.transform.SetParent(settingsPanel.transform, false);
        var bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one; // stretch across panel
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bgObj.GetComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.95f); // 95% black for high readability
        bgObj.transform.SetAsFirstSibling(); // Render behind everything else

        // 2. Clear out existing text components that might be labels for the toggles
        // We will just add a new TextMeshProUGUI on top.
        var controlsText = new GameObject("ControlsText").AddComponent<TextMeshProUGUI>();
        controlsText.transform.SetParent(settingsPanel.transform, false);
        
        // Stretch across the panel
        var rt = controlsText.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.05f);
        rt.anchorMax = new Vector2(0.95f, 0.95f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        controlsText.enableAutoSizing = true;
        controlsText.fontSizeMin = 14;
        controlsText.fontSizeMax = 50;
        controlsText.alignment = TextAlignmentOptions.Center;
        controlsText.color = Color.white;
        
        // Apply outline for extra readability
        var outline = controlsText.gameObject.GetComponent<UnityEngine.UI.Outline>();
        if (outline == null) outline = controlsText.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0, 0, 0, 1f);
        outline.effectDistance = new Vector2(2, -2);
        
        // Try to apply the medieval font if it exists
        Font customFont = Resources.Load<Font>("MedievalSharp-Bold");
        if (customFont != null)
        {
            controlsText.font = TMPro.TMP_FontAsset.CreateFontAsset(customFont);
        }

        controlsText.text = 
            "<b><size=150%>CONTROLS</size></b>\n\n" +
            "<b>Move:</b> W, A, S, D / Arrow Keys\n" +
            "<b>Look:</b> Mouse\n" +
            "<b>Jump:</b> Space\n" +
            "<b>Sprint:</b> Left Shift\n" +
            "<b>Attack:</b> Left Mouse Button / Enter\n" +
            "<b>Interact:</b> E\n" +
            "<b>Crouch:</b> C\n" +
            "<b>Previous Item:</b> 1\n" +
            "<b>Next Item:</b> 2";
    }
}
