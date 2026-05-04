using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class SettingsMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject settingsPanel;
    // Removed graphics toggles

    private bool isSettingsActive = false;

    void Start()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
            
            // Rebuild settings panel to just show controls
            RebuildSettingsPanel();
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
        settingsPanel.SetActive(isSettingsActive);

        // Manage cursor state and timescale
        if (isSettingsActive)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
        }
    }

    private void RebuildSettingsPanel()
    {
        // 1. Destroy existing graphical toggles or sliders to ensure NO graphics function
        var toggles = settingsPanel.GetComponentsInChildren<UnityEngine.UI.Toggle>(true);
        foreach (var t in toggles)
        {
            Destroy(t.gameObject);
        }

        // 2. Clear out existing text components that might be labels for the toggles (but keep the background)
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
