#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// BallsOfBabel → Patch: Title + Settings Icon
/// Adds the "Balls of Babel" title and settings gear to the existing scene
/// without touching anything the user has already adjusted.
/// </summary>
public static class MainMenuRPGPatch
{
    private const string SprSettings = "Assets/Artsystack - Fantasy RPG GUI/Resources/Sprites/flaticon/white/64/setting_1_64.png";
    private static readonly Color GoldTitle = new Color(1.00f, 0.80f, 0.20f);
    private static readonly Color SubColor  = new Color(0.93f, 0.81f, 0.62f);
    private static readonly Color DivColor  = new Color(0.55f, 0.55f, 0.55f, 0.85f);

    [MenuItem("BallsOfBabel/Patch: Title + Settings Icon", priority = 11)]
    public static void Patch()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);

        // Find the canvas
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("[Patch] No Canvas found in MainMenu.unity!"); return; }
        var root = canvas.transform;

        // ── Remove old Panel-Title ("You ready?" banner) ──────────────────────
        var oldTitle = root.Find("Panel-Title");
        if (oldTitle != null) Object.DestroyImmediate(oldTitle.gameObject);

        // ── Remove old TitleGroup if it somehow exists ────────────────────────
        var oldGroup = root.Find("TitleGroup");
        if (oldGroup != null) Object.DestroyImmediate(oldGroup.gameObject);
        var oldCenter = root.Find("CenterPanel");
        if (oldCenter != null)
        {
            var og = oldCenter.Find("TitleGroup");
            if (og != null) Object.DestroyImmediate(og.gameObject);
        }

        // ── Create "Balls of Babel" title in center-right area ────────────────
        // Anchored: x 0.24–0.98, y 0.55–0.92  (right of the left button panel)
        var titleGO = new GameObject("BallsOfBabel_Title");
        titleGO.transform.SetParent(root, false);
        // Make sure it sits above buttons in draw order
        titleGO.transform.SetSiblingIndex(root.childCount - 1);

        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.24f, 0.45f);
        titleRT.anchorMax = new Vector2(0.98f, 0.93f);
        titleRT.offsetMin = titleRT.offsetMax = Vector2.zero;

        var titleCG = titleGO.AddComponent<CanvasGroup>();
        titleCG.alpha = 1f;
        titleCG.blocksRaycasts = false;
        titleCG.interactable   = false;

        // Main title text
        var txtGO = new GameObject("Title_Text");
        txtGO.transform.SetParent(titleGO.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0f, 0.45f);
        txtRT.anchorMax = new Vector2(1f, 1f);
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "Balls of Babel";
        tmp.fontSize  = 88;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = GoldTitle;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 40;
        tmp.fontSizeMax = 100;

        // Divider line
        var divGO = new GameObject("Title_Divider");
        divGO.transform.SetParent(titleGO.transform, false);
        var divRT = divGO.AddComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0.05f, 0.38f);
        divRT.anchorMax = new Vector2(0.95f, 0.42f);
        divRT.offsetMin = divRT.offsetMax = Vector2.zero;
        divGO.AddComponent<Image>().color = DivColor;

        // Subtitle
        var subGO = new GameObject("Title_Subtitle");
        subGO.transform.SetParent(titleGO.transform, false);
        var subRT = subGO.AddComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0f, 0.05f);
        subRT.anchorMax = new Vector2(1f, 0.36f);
        subRT.offsetMin = subRT.offsetMax = Vector2.zero;
        var subTMP = subGO.AddComponent<TextMeshProUGUI>();
        subTMP.text      = "A Multiplayer Ball Physics Experience";
        subTMP.fontSize  = 28;
        subTMP.color     = SubColor;
        subTMP.alignment = TextAlignmentOptions.Center;

        // ── Settings gear icon — bottom-right corner ──────────────────────────
        // Remove old one if it exists
        var oldSettings = root.Find("Btn_Settings");
        if (oldSettings != null) Object.DestroyImmediate(oldSettings.gameObject);

        var settingsGO = new GameObject("Btn_Settings");
        settingsGO.transform.SetParent(root, false);
        settingsGO.transform.SetAsLastSibling();

        var sRT = settingsGO.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0.945f, 0.01f);
        sRT.anchorMax = new Vector2(0.985f, 0.09f);
        sRT.offsetMin = sRT.offsetMax = Vector2.zero;

        var sImg = settingsGO.AddComponent<Image>();
        sImg.color          = new Color(1, 1, 1, 0.80f);
        sImg.preserveAspect = true;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SprSettings);
        if (sprite != null) sImg.sprite = sprite;
        else Debug.LogWarning("[Patch] Settings sprite not found: " + SprSettings);

        var sBtn    = settingsGO.AddComponent<Button>();
        var colors  = sBtn.colors;
        colors.normalColor      = new Color(1, 1, 1, 0.75f);
        colors.highlightedColor = Color.white;
        colors.pressedColor     = new Color(0.8f, 0.8f, 0.8f);
        sBtn.colors = colors;

        // Wire settings button to MainMenuRPGSetup if it exists
        var setup = canvas.GetComponent<MainMenuRPGSetup>();
        if (setup != null)
        {
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
                sBtn.onClick,
                (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
                    typeof(UnityEngine.Events.UnityAction), setup,
                    typeof(MainMenuRPGSetup).GetMethod(nameof(MainMenuRPGSetup.OnSettings))));
        }

        // ── Save ──────────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();

        Debug.Log("[Patch] ✅ Title + Settings icon added!");
        EditorUtility.DisplayDialog("Patched!", 
            "'Balls of Babel' title and settings icon added to the scene.\n\nEnter Play Mode to preview.", "Done!");
    }
}
#endif
