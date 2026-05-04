#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// BallsOfBabel → Add Title + Settings Icon
/// Works on whatever scene is CURRENTLY OPEN in the editor.
/// Only ADDS objects — never deletes or moves existing ones.
/// </summary>
public static class MainMenuAddTitleAndIcon
{
    private const string SprSettings = "Assets/Artsystack - Fantasy RPG GUI/Resources/Sprites/flaticon/white/64/setting_1_64.png";

    private static readonly Color GoldTitle = new Color(1.00f, 0.80f, 0.20f);
    private static readonly Color SubColor  = new Color(0.93f, 0.81f, 0.62f);
    private static readonly Color DivColor  = new Color(0.55f, 0.55f, 0.55f, 0.85f);

    [MenuItem("BallsOfBabel/Add Title + Settings Icon to Open Scene", priority = 12)]
    public static void AddTitleAndIcon()
    {
        // ── Find canvas in the ACTIVE scene — never opens or closes scenes ────
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error", "No Canvas found in the open scene.\nMake sure MainMenu.unity is open.", "OK");
            return;
        }

        var root = canvas.transform;

        // ── Remove previous patch objects if they exist ───────────────────────
        DestroyIfExists(root, "BallsOfBabel_Title");
        DestroyIfExists(root, "Btn_Settings");

        // ═════════════════════════════════════════════════════════════════════
        // 1.  "Balls of Babel" title group
        //     Positioned in the CENTER-RIGHT area so it doesn't overlap buttons
        // ═════════════════════════════════════════════════════════════════════
        var titleRoot = MakeRect("BallsOfBabel_Title", root,
            new Vector2(0.28f, 0.52f),   // sits to the right of the left button panel
            new Vector2(0.97f, 0.92f));

        var titleCG = titleRoot.AddComponent<CanvasGroup>();
        titleCG.blocksRaycasts = false;
        titleCG.interactable   = false;

        // Large gold title
        var titleTxt = MakeLabel("Title_Text", titleRoot.transform,
            "Balls of Babel", 88, FontStyles.Bold, GoldTitle,
            TextAlignmentOptions.Center);
        Anchor(titleTxt, new Vector2(0f, 0.48f), new Vector2(1f, 1f));
        var tmp = titleTxt.GetComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false; // Disable raycast on text
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 36;
        tmp.fontSizeMax = 100;

        // Divider
        var div = MakeImage("Title_Divider", titleRoot.transform, DivColor);
        Anchor(div, new Vector2(0.04f, 0.38f), new Vector2(0.96f, 0.42f));
        div.GetComponent<Image>().raycastTarget = false; // Disable raycast on divider

        // Subtitle
        var sub = MakeLabel("Title_Subtitle", titleRoot.transform,
            "A Multiplayer Ball Physics Experience", 26, FontStyles.Normal,
            SubColor, TextAlignmentOptions.Center);
        Anchor(sub, new Vector2(0f, 0.05f), new Vector2(1f, 0.36f));
        sub.GetComponent<TextMeshProUGUI>().raycastTarget = false; // Disable raycast on subtitle

        // ═════════════════════════════════════════════════════════════════════
        // 2.  Settings gear — bottom-right corner of the canvas
        // ═════════════════════════════════════════════════════════════════════
        var gearGO = new GameObject("Btn_Settings");
        gearGO.transform.SetParent(root, false);
        gearGO.transform.SetAsLastSibling();   // draw on top of everything

        var gRT = gearGO.AddComponent<RectTransform>();
        gRT.anchorMin = new Vector2(0.945f, 0.012f);
        gRT.anchorMax = new Vector2(0.988f, 0.092f);
        gRT.offsetMin = gRT.offsetMax = Vector2.zero;

        var gImg = gearGO.AddComponent<Image>();
        gImg.preserveAspect = true;
        gImg.color = new Color(1, 1, 1, 0.82f);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SprSettings);
        if (sprite != null)
            gImg.sprite = sprite;
        else
            Debug.LogWarning("[AddTitle] Settings sprite not found at: " + SprSettings);

        var gBtn    = gearGO.AddComponent<Button>();
        var colors  = gBtn.colors;
        colors.normalColor      = new Color(1, 1, 1, 0.75f);
        colors.highlightedColor = Color.white;
        colors.pressedColor     = new Color(0.7f, 0.7f, 0.7f);
        gBtn.colors = colors;

        // Wire to MainMenuRPGSetup.OnSettings if the component exists
        var setup = canvas.GetComponent<MainMenuRPGSetup>();
        if (setup != null)
        {
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
                gBtn.onClick,
                (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
                    typeof(UnityEngine.Events.UnityAction), setup,
                    typeof(MainMenuRPGSetup).GetMethod(nameof(MainMenuRPGSetup.OnSettings))));
        }

        // ── Mark dirty and save ───────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        // Auto-save
        EditorSceneManager.SaveScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        AssetDatabase.Refresh();
        Debug.Log("[AddTitle] ✅ 'Balls of Babel' title + settings gear added to scene.");
        EditorUtility.DisplayDialog("Done!",
            "'Balls of Babel' title and ⚙ settings icon added!\n\nEnter Play Mode to preview.", "Nice!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static void DestroyIfExists(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }

    static GameObject MakeRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    static void Anchor(GameObject go, Vector2 min, Vector2 max)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static GameObject MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = color;
        return go;
    }

    static GameObject MakeLabel(string name, Transform parent, string text,
        float size, FontStyles style, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text      = text;
        t.fontSize  = size;
        t.fontStyle = style;
        t.color     = color;
        t.alignment = align;
        return go;
    }
}
#endif
