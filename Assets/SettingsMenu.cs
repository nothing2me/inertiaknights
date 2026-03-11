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
        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);

        if (vsyncToggle != null)
            vsyncToggle.SetIsOnWithoutNotify(QualitySettings.vSyncCount > 0);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ToggleSettingsMenu();
    }

    public void ToggleSettingsMenu()
    {
        if (settingsPanel == null) return;

        isSettingsActive = !isSettingsActive;
        settingsPanel.SetActive(isSettingsActive);

        if (isSettingsActive)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    public void SetVSync(bool useVSync)
    {
        QualitySettings.vSyncCount = useVSync ? 1 : 0;
    }
}
