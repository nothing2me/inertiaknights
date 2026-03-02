using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject settingsPanel;
    public Toggle fullscreenToggle;
    public Toggle vsyncToggle;

    private bool isSettingsActive = false;

    void Start()
    {
        // First, apply current settings to the UI toggles so they match the actual game state
        // We do this BEFORE the panel is disabled, as Unity UI can sometimes glitch out
        // and fail to render checkmarks if their state is modified while inactive.
        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        }

        if (vsyncToggle != null)
        {
            // vSyncCount of 1 means enabled, 0 means disabled
            vsyncToggle.SetIsOnWithoutNotify(QualitySettings.vSyncCount > 0);
        }

        // Ensure menu is closed when starting
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    void Update()
    {
        // Listen for the Escape Key
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleSettingsMenu();
        }
    }

    public void ToggleSettingsMenu()
    {
        if (settingsPanel == null) return;

        isSettingsActive = !isSettingsActive;
        settingsPanel.SetActive(isSettingsActive);

        if (isSettingsActive)
        {
            // Pause the game when settings are open
            Time.timeScale = 0f;
        }
        else
        {
            // Unpause the game when settings are closed
            Time.timeScale = 1f;
        }
    }

    // Called by the Fullscreen UI Toggle Checkbox Event
    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        Debug.Log("Fullscreen set to: " + isFullscreen);
    }

    // Called by the VSync UI Toggle Checkbox Event
    public void SetVSync(bool useVSync)
    {
        // 1 = Sync to monitor's refresh rate (cures tearing)
        // 0 = Don't sync (uncapped framerate, tearing might occur)
        QualitySettings.vSyncCount = useVSync ? 1 : 0;
        Debug.Log("VSync set to: " + useVSync);
    }
}
