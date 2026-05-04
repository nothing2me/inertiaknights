using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drop this on any Button to make it quit the application.
/// Automatically wires itself — no Inspector setup needed.
/// </summary>
[RequireComponent(typeof(Button))]
public class QuitButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(DoQuit);
    }

    public void DoQuit()
    {
        Debug.Log("[QuitButton] Quitting application...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
