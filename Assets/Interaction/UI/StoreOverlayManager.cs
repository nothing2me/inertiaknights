using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the Shop Overlay UI when loaded additively.
/// Handles background transparency, closing the shop, and unlocking the player.
/// </summary>
public class StoreOverlayManager : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private Image backgroundPanel;
    [SerializeField] [Range(0f, 1f)] private float backgroundOpacity = 0.75f;
    [SerializeField] private Button closeButton;

    private void Start()
    {
        // --- Setup Background Opacity ---
        if (backgroundPanel == null)
        {
            // Try to find a panel named "Background" or "Panel" if not assigned
            var bg = GameObject.Find("Background") ?? GameObject.Find("Panel");
            if (bg != null) backgroundPanel = bg.GetComponent<Image>();
        }

        if (backgroundPanel != null)
        {
            Color color = backgroundPanel.color;
            color.a = backgroundOpacity;
            backgroundPanel.color = color;
        }

        // --- Setup Close Button ---
        if (closeButton == null)
        {
            // Try to find a button named "CloseButton" or "BackButton"
            var btn = GameObject.Find("CloseButton") ?? GameObject.Find("BackButton") ?? GameObject.Find("Back");
            if (btn != null) closeButton = btn.GetComponent<Button>();
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseShop);
        }

        // Enable cursor when shop opens
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        // Allow closing with Escape key (New Input System)
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseShop();
        }
    }

    public void CloseShop()
    {
        // Unlock the local player's movement
        UnlockPlayer();

        // Restore cursor state if needed (usually back to locked for ball games)
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;

        // Unload the additive scene this object belongs to
        if (gameObject.scene.IsValid())
        {
            SceneManager.UnloadSceneAsync(gameObject.scene.name);
        }
        else
        {
            Debug.LogWarning("[StoreOverlayManager] Could not find a valid scene to unload.");
        }
    }

    private void UnlockPlayer()
    {
        // Find the local player using the same logic as InteractableController
        BallController[] balls = Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
        foreach (var ball in balls)
        {
            if (ball.IsOwner)
            {
                ball.movementLocked = false;
                break;
            }
        }
    }
}
