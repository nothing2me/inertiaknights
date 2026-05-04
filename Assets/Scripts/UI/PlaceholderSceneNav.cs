using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple navigation helper attached to placeholder scenes (Store, Character).
/// Provides a "Back to Menu" callback for the UI button.
/// </summary>
public class PlaceholderSceneNav : MonoBehaviour
{
    private const string MainMenuScene = "MainMenu";

    /// <summary>
    /// Called by the "Back to Menu" button in the placeholder scene.
    /// </summary>
    public void BackToMenu()
    {
        SceneManager.LoadScene(MainMenuScene);
    }
}
