#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// BallsOfBabel → Create Store & Character Scenes
/// ─────────────────────────────────────────────────
/// Creates two placeholder scenes with colored backgrounds and a "Back to Menu" button.
/// Automatically adds all required scenes to Build Settings.
/// </summary>
public static class CreatePlaceholderScenes
{
    private static readonly Color StoreColor     = Color.black;
    private static readonly Color CharacterColor = new Color(0.05f, 0.15f, 0.45f); // deep blue
    private static readonly Color SettingsColor  = new Color(0.15f, 0.15f, 0.15f); // dark grey
    private static readonly Color BtnBGColor     = new Color(0.25f, 0.25f, 0.25f, 0.9f);
    private static readonly Color GoldAccent     = new Color(1f, 0.80f, 0.20f);

    [MenuItem("BallsOfBabel/Create Settings Scene", priority = 16)]
    public static void CreateSettingsOnly()
    {
        if (!EditorUtility.DisplayDialog(
            "Create Settings Scene",
            "This will create:\n" +
            "  • SettingsScene.unity  (dark grey background)\n\n" +
            "With a '← Back to Menu' button.\n" +
            "It will be added to Build Settings.\n\nProceed?",
            "Create", "Cancel"))
            return;

        CreateScene("SettingsScene",  SettingsColor,  "SETTINGS");
        AddScenesToBuildSettings();

        // Re-open the MainMenu so the user is back where they started
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);

        EditorUtility.DisplayDialog("Done!",
            "Created SettingsScene.\n\n" +
            "It has been added to Build Settings.",
            "Nice!");
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Scene Builder
    // ─────────────────────────────────────────────────────────────────────

    private static void CreateScene(string sceneName, Color bgColor, string label)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // ── Camera background color ──────────────────────────────────────
        var cam = Object.FindAnyObjectByType<Camera>();
        if (cam != null)
        {
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = bgColor;
        }

        // ── Canvas ───────────────────────────────────────────────────────
        var canvasGO = new GameObject("Canvas_" + sceneName);
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Scene label (big centered text) ──────────────────────────────
        var labelGO = new GameObject("SceneLabel");
        labelGO.transform.SetParent(canvasGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.15f, 0.40f);
        labelRT.anchorMax = new Vector2(0.85f, 0.65f);
        labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;

        var tmp       = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 80;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        // ── Subtitle ─────────────────────────────────────────────────────
        var subGO = new GameObject("Subtitle");
        subGO.transform.SetParent(canvasGO.transform, false);
        var subRT = subGO.AddComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0.25f, 0.32f);
        subRT.anchorMax = new Vector2(0.75f, 0.40f);
        subRT.offsetMin = subRT.offsetMax = Vector2.zero;

        var subTMP       = subGO.AddComponent<TextMeshProUGUI>();
        subTMP.text      = "Coming Soon…";
        subTMP.fontSize  = 28;
        subTMP.fontStyle = FontStyles.Italic;
        subTMP.color     = new Color(1f, 1f, 1f, 0.5f);
        subTMP.alignment = TextAlignmentOptions.Center;

        // ── Back to Menu button ──────────────────────────────────────────
        var btnGO = new GameObject("Btn_BackToMenu");
        btnGO.transform.SetParent(canvasGO.transform, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.35f, 0.14f);
        btnRT.anchorMax = new Vector2(0.65f, 0.24f);
        btnRT.offsetMin = btnRT.offsetMax = Vector2.zero;

        var btnImg  = btnGO.AddComponent<Image>();
        btnImg.color = BtnBGColor;

        // Gold border outline (nested image)
        var borderGO = new GameObject("Border");
        borderGO.transform.SetParent(btnGO.transform, false);
        var borderRT = borderGO.AddComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero;
        borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = new Vector2(-2, -2);
        borderRT.offsetMax = new Vector2(2, 2);
        borderGO.transform.SetAsFirstSibling();
        var borderImg       = borderGO.AddComponent<Image>();
        borderImg.color     = GoldAccent;
        borderImg.raycastTarget = false;

        // Button label
        var btnLabel = new GameObject("Label");
        btnLabel.transform.SetParent(btnGO.transform, false);
        var btnLabelRT = btnLabel.AddComponent<RectTransform>();
        btnLabelRT.anchorMin = Vector2.zero;
        btnLabelRT.anchorMax = Vector2.one;
        btnLabelRT.offsetMin = btnLabelRT.offsetMax = Vector2.zero;

        var btnTMP       = btnLabel.AddComponent<TextMeshProUGUI>();
        btnTMP.text      = "← Back to Menu";
        btnTMP.fontSize  = 32;
        btnTMP.color     = Color.white;
        btnTMP.alignment = TextAlignmentOptions.Center;

        // ── Attach PlaceholderSceneNav + wire button ─────────────────────
        var nav = canvasGO.AddComponent<PlaceholderSceneNav>();
        var btn = btnGO.AddComponent<Button>();

        // Hover colors
        var colors = btn.colors;
        colors.normalColor      = BtnBGColor;
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 0.95f);
        colors.pressedColor     = new Color(0.2f, 0.2f, 0.2f, 1f);
        btn.colors = colors;

        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
            btn.onClick,
            (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), nav,
                typeof(PlaceholderSceneNav).GetMethod(nameof(PlaceholderSceneNav.BackToMenu))));

        // ── EventSystem ──────────────────────────────────────────────────
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // ── Save ─────────────────────────────────────────────────────────
        string path = "Assets/Scenes/" + sceneName + ".unity";
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.Refresh();
        Debug.Log($"[PlaceholderScenes] Created {path}");
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Build Settings
    // ─────────────────────────────────────────────────────────────────────

    private static void AddScenesToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        string[] required =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/MainScene.unity",
            "Assets/Scenes/StoreScene.unity",
            "Assets/Scenes/CharacterScene.unity",
            "Assets/Scenes/SettingsScene.unity"
        };

        foreach (var path in required)
        {
            bool found = false;
            foreach (var s in scenes)
            {
                if (s.path == path) { found = true; s.enabled = true; break; }
            }

            if (!found && System.IO.File.Exists(path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[PlaceholderScenes] Build Settings updated with all required scenes.");
    }
}
#endif
