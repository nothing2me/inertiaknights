#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click: adds MainMenuRPGSetup to Canvas_MainMenu in the current scene.
/// Menu: BallsOfBabel → Add MainMenuRPGSetup
/// </summary>
public static class AddMainMenuSetup
{
    [MenuItem("BallsOfBabel/Add MainMenuRPGSetup", priority = 20)]
    public static void Run()
    {
        // Find Canvas_MainMenu in the scene
        GameObject canvas = GameObject.Find("Canvas_MainMenu");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Not Found",
                "Could not find 'Canvas_MainMenu' in the current scene.\n\n" +
                "Make sure the MainMenu scene is open.", "OK");
            return;
        }

        // Check if it already has the component
        if (canvas.GetComponent<MainMenuRPGSetup>() != null)
        {
            EditorUtility.DisplayDialog("Already Added",
                "Canvas_MainMenu already has MainMenuRPGSetup attached!", "OK");
            return;
        }

        // Add the component
        var setup = canvas.AddComponent<MainMenuRPGSetup>();

        // Mark the scene dirty so it can be saved
        EditorSceneManager.MarkSceneDirty(canvas.scene);

        Debug.Log("[AddMainMenuSetup] ✅ MainMenuRPGSetup added to Canvas_MainMenu!");
        EditorUtility.DisplayDialog("Done!",
            "MainMenuRPGSetup has been added to Canvas_MainMenu!\n\n" +
            "Press Ctrl+S to save the scene.", "Great!");
    }
}
#endif
