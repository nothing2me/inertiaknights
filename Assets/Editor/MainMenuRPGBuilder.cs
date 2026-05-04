#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// BallsOfBabel → Build RPG Main Menu
/// ─────────────────────────────────────────────────────────────────────────────
/// Rebuilds MainMenu.unity to match the Artsystack Fantasy RPG GUI demo layout:
///   • Dark textured background
///   • Top horizontal tab bar
///   • Left-side vertical menu (Host Game / Join Game / Quit)
///   • Large centered "Balls of Babel" title with divider line
///   • Bottom icon row with settings gear in the bottom-right corner
///   • IP input panel (hidden, slides in on Join Game click)
///   • All NetworkManagerUI SerializeFields wired automatically
/// ─────────────────────────────────────────────────────────────────────────────
/// </summary>
public static class MainMenuRPGBuilder
{
    // ── Artsystack asset paths ───────────────────────────────────────────────
    private const string ArtBase   = "Assets/Artsystack - Fantasy RPG GUI/Resources/Prefabs";
    private const string SprBase   = "Assets/Artsystack - Fantasy RPG GUI/Resources/Sprites";
    private const string FontBase  = "Assets/Artsystack - Fantasy RPG GUI/Resources/Font";
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";

    // Prefabs
    private const string PrefabBackground  = ArtBase + "/Background/Background.prefab";
    private const string PrefabBackground2 = ArtBase + "/Background/Background-2.prefab";
    private const string PrefabTopBar      = ArtBase + "/Common/Layouts/Top-Bar.prefab";
    private const string PrefabBottomBar   = ArtBase + "/Common/Layouts/Bottom-Bar.prefab";
    private const string PrefabBtnGreen    = ArtBase + "/Common/Buttons/Button-Green.prefab";
    private const string PrefabBtnOrange   = ArtBase + "/Common/Buttons/Button-Orange.prefab";
    private const string PrefabBtnGrey     = ArtBase + "/Common/Buttons/Button-Grey.prefab";
    private const string PrefabInputField  = ArtBase + "/Common/Elements/InputField.prefab";
    private const string PrefabPanelTitle  = ArtBase + "/Common/Elements/Panel-Title.prefab";

    // Sprites
    private const string SprSettingsIcon = SprBase + "/flaticon/white/64/setting_1_64.png";
    private const string SprTrophyIcon   = SprBase + "/flaticon/white/64/trophy_64.png";
    private const string SprProfileIcon  = SprBase + "/flaticon/white/64/profile_64.png";
    private const string SprGlobeIcon    = SprBase + "/flaticon/white/64/World_Wide_Web_64.png";

    // Font (MedievalSharp or Kurale — whichever exists)
    private const string FontPathMedieval = FontBase + "/MedievalSharp-Book.ttf";

    // Colors
    private static readonly Color GoldTitle  = new Color(1.00f, 0.80f, 0.20f);
    private static readonly Color GoldText   = new Color(0.93f, 0.81f, 0.62f);
    private static readonly Color SubTitle   = new Color(0.93f, 0.81f, 0.62f);
    private static readonly Color StatusGold = new Color(1.00f, 0.85f, 0.40f);
    private static readonly Color DividerCol = new Color(0.55f, 0.55f, 0.55f, 0.8f);
    private static readonly Color MenuBG     = new Color(0.06f, 0.04f, 0.02f, 0.0f);

    // ────────────────────────────────────────────────────────────────────────

    [MenuItem("BallsOfBabel/Build RPG Main Menu", priority = 10)]
    public static void BuildMenu()
    {
        if (!EditorUtility.DisplayDialog(
            "Build RPG Main Menu",
            "This will REPLACE the existing Canvas UI in MainMenu.unity with the Artsystack RPG theme.\n\nA backup → MainMenu_backup.unity will be saved first.\n\nProceed?",
            "Yes, build it!", "Cancel"))
            return;

        // ── Backup ────────────────────────────────────────────────────────────
        string backup = ScenePath.Replace(".unity", "_backup.unity");
        AssetDatabase.DeleteAsset(backup);
        AssetDatabase.CopyAsset(ScenePath, backup);
        Debug.Log($"[RPGBuilder] Backup saved → {backup}");

        // ── Open scene ────────────────────────────────────────────────────────
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Remove old Canvas objects
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<Canvas>() != null ||
                root.name.ToLower().Contains("canvas") ||
                root.name.ToLower().Contains("networkmanager") == false && root.name.ToLower().Contains("ui"))
            {
                Object.DestroyImmediate(root);
            }
        }

        EnsureEventSystem();

        // ── Root Canvas ───────────────────────────────────────────────────────
        var canvasGO = new GameObject("Canvas_MainMenu");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── [1] Background ────────────────────────────────────────────────────
        var bg = Spawn(PrefabBackground, canvasGO.transform, "Background_RPG");
        if (bg != null) Stretch(bg);

        var bg2 = Spawn(PrefabBackground2, canvasGO.transform, "Background2_RPG");
        if (bg2 != null) Stretch(bg2);

        // ── [2] Top Tab Bar ───────────────────────────────────────────────────
        var topBar = Spawn(PrefabTopBar, canvasGO.transform, "TopBar_RPG");
        // TopBar prefab already has correct anchors; leave as-is

        // ── [3] Master Panels ─────────────────────────────────────────────────
        var gamePanel = MakeRect("GamePanel", canvasGO.transform, Vector2.zero, Vector2.one);
        var charPanel = MakeRect("CharacterPanel", canvasGO.transform, Vector2.zero, Vector2.one);
        var storePanel = MakeRect("StorePanel", canvasGO.transform, Vector2.zero, Vector2.one);

        // Hide Char & Store by default
        charPanel.SetActive(false);
        storePanel.SetActive(false);

        // ── [3.1] Store Panel Content ──────────────────────────────────────────
        var storeBG = storePanel.AddComponent<Image>();
        storeBG.color = new Color(0, 0, 0, 0.85f);
        MakeLabel("StoreTitle", storePanel.transform, "ITEM STORE", 80, FontStyles.Bold, GoldTitle, TextAlignmentOptions.Center);
        var storeSub = MakeLabel("StoreSub", storePanel.transform, "Coming Soon...", 32, FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
        SetAnchors(storeSub, new Vector2(0, 0.4f), new Vector2(1, 0.5f));

        // ── [3.2] Character Panel Content ──────────────────────────────────────
        var charBG = charPanel.AddComponent<Image>();
        charBG.color = new Color(0.05f, 0.15f, 0.45f, 0.85f); // Deep blue tint
        MakeLabel("CharTitle", charPanel.transform, "CHARACTER", 80, FontStyles.Bold, GoldTitle, TextAlignmentOptions.Center);
        var charSub = MakeLabel("CharSub", charPanel.transform, "Coming Soon...", 32, FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
        SetAnchors(charSub, new Vector2(0, 0.4f), new Vector2(1, 0.5f));

        // ── [4] Left Menu Panel (Inside GamePanel) ───────────────────────────
        // Transparent host for the left-side buttons (like the demo's Play/Save Game/etc.)
        var leftPanel = MakeRect("LeftMenuPanel", gamePanel.transform,
            new Vector2(0f, 0.08f), new Vector2(0.22f, 0.92f));
        leftPanel.AddComponent<CanvasGroup>(); // for fade-in

        // ── [3a] "PLAY" header label (styled like the demo tab) ───────────────
        var playLbl = MakeLabel("Lbl_Play", leftPanel.transform, "Play",
            40, FontStyles.Normal, GoldTitle, TextAlignmentOptions.Center);
        SetAnchors(playLbl, new Vector2(0f, 0.80f), new Vector2(1f, 0.95f));
        // Gold underline diamond decoration (simple image line)
        var divLbl = MakeImage("PlayDivider", leftPanel.transform, DividerCol);
        SetAnchors(divLbl, new Vector2(0.1f, 0.775f), new Vector2(0.9f, 0.785f));

        // ── [3b] HOST GAME ────────────────────────────────────────────────────
        var hostBtnGO  = SpawnButton(PrefabBtnGreen,  leftPanel.transform, "Btn_Host",    "Host Game");
        var joinBtnGO  = SpawnButton(PrefabBtnOrange, leftPanel.transform, "Btn_Join",    "Join Game");
        var quitBtnGO  = SpawnButton(PrefabBtnGrey,   leftPanel.transform, "Btn_Quit",    "Quit");

        SetAnchors(hostBtnGO, new Vector2(0.02f, 0.60f), new Vector2(0.98f, 0.73f));
        SetAnchors(joinBtnGO, new Vector2(0.02f, 0.43f), new Vector2(0.98f, 0.56f));
        SetAnchors(quitBtnGO, new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.21f));

        // Fade-in via CanvasGroup (alpha starts at 0; MainMenuRPGSetup animates them)
        var hostCG = GetOrAdd<CanvasGroup>(hostBtnGO); hostCG.alpha = 0f;
        var joinCG = GetOrAdd<CanvasGroup>(joinBtnGO); joinCG.alpha = 0f;
        var quitCG = GetOrAdd<CanvasGroup>(quitBtnGO); quitCG.alpha = 0f;

        // ── [3c] Player Name input (below Play label) ─────────────────────────
        var nameInputGO = SpawnInput(PrefabInputField, leftPanel.transform, "NameInput", "Enter Your Name…");
        SetAnchors(nameInputGO, new Vector2(0.02f, 0.755f), new Vector2(0.98f, 0.795f));
        var nameField = nameInputGO.GetComponentInChildren<TMP_InputField>()
                     ?? nameInputGO.GetComponent<TMP_InputField>();

        // ── [5] Center: Big Title (Inside GamePanel) ─────────────────────────
        var centerPanel = MakeRect("CenterPanel", gamePanel.transform,
            new Vector2(0.22f, 0.08f), new Vector2(1.0f, 0.92f));

        // Title group (for fade-in)
        var titleGroup   = MakeRect("TitleGroup", centerPanel.transform,
            new Vector2(0.02f, 0.45f), new Vector2(0.98f, 0.90f));
        var titleCG = titleGroup.AddComponent<CanvasGroup>();
        titleCG.alpha = 0f;
        titleCG.blocksRaycasts = false;
        titleCG.interactable   = false;

        // "Balls of Babel" — big medieval-style text
        var titleTextGO = MakeLabel("TitleText", titleGroup.transform,
            "Balls of Babel", 96, FontStyles.Bold, GoldTitle, TextAlignmentOptions.Center);
        Stretch(titleTextGO);

        // Horizontal divider line under title
        var divGO = MakeImage("TitleDivider", titleGroup.transform, DividerCol);
        SetAnchors(divGO, new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.34f));

        // Subtitle
        var subTextGO = MakeLabel("SubtitleText", titleGroup.transform,
            "A Multiplayer Ball Physics Experience", 32, FontStyles.Normal, SubTitle, TextAlignmentOptions.Center);
        SetAnchors(subTextGO, new Vector2(0f, 0.05f), new Vector2(1f, 0.30f));

        // ── [6] IP Panel (hidden by default — shown on Join click) ────────────
        var ipPanel = MakeRect("IPPanel", gamePanel.transform,
            new Vector2(0.22f, 0.02f), new Vector2(0.75f, 0.14f));
        var ipBG = ipPanel.AddComponent<Image>();
        ipBG.color = new Color(0.04f, 0.03f, 0.01f, 0.90f);

        var ipInputGO = SpawnInput(PrefabInputField, ipPanel.transform, "IPInput", "Enter IP : Port  (blank = LAN search)…");
        SetAnchors(ipInputGO, new Vector2(0.01f, 0.08f), new Vector2(0.73f, 0.92f));
        var ipField = ipInputGO.GetComponentInChildren<TMP_InputField>()
                   ?? ipInputGO.GetComponent<TMP_InputField>();

        var connectBtnGO = SpawnButton(PrefabBtnOrange, ipPanel.transform, "Btn_Connect", "Connect");
        SetAnchors(connectBtnGO, new Vector2(0.74f, 0.08f), new Vector2(0.99f, 0.92f));

        ipPanel.SetActive(false);

        // ── [7] Status text ───────────────────────────────────────────────────
        var statusGO = MakeLabel("StatusText", gamePanel.transform,
            "", 22, FontStyles.Normal, StatusGold, TextAlignmentOptions.Center);
        SetAnchors(statusGO, new Vector2(0.22f, 0.02f), new Vector2(0.78f, 0.08f));
        var statusTMP = statusGO.GetComponent<TextMeshProUGUI>();

        // ── [7] Bottom Bar ────────────────────────────────────────────────────
        var bottomBar = Spawn(PrefabBottomBar, canvasGO.transform, "BottomBar_RPG");

        // ── [8] Bottom-left icon row (globe, profile, trophy, settings) ────────
        // These match the four icons shown in the demo bottom-left
        var iconRow = MakeRect("BottomIconRow", canvasGO.transform,
            new Vector2(0.01f, 0.01f), new Vector2(0.20f, 0.085f));

        MakeIconBtn("Btn_Globe",    iconRow.transform, SprGlobeIcon,   null);
        MakeIconBtn("Btn_Profile",  iconRow.transform, SprProfileIcon, null);
        MakeIconBtn("Btn_Trophy",   iconRow.transform, SprTrophyIcon,  null);

        // ── [9] Settings gear — BOTTOM RIGHT corner ───────────────────────────
        var settingsBtnGO = MakeIconBtn("Btn_Settings", canvasGO.transform, SprSettingsIcon, null);
        SetAnchors(settingsBtnGO, new Vector2(0.95f, 0.01f), new Vector2(1.00f, 0.085f));

        // ── [10] Attach NetworkManagerUI ──────────────────────────────────────
        // Keep any existing NetworkManager root; add NMUI to the canvas
        var nmui = canvasGO.AddComponent<NetworkManagerUI>();
        SetField(nmui, "hostButton",      GetBtn(hostBtnGO));
        SetField(nmui, "clientButton",    GetBtn(joinBtnGO));
        SetField(nmui, "nameInputField",  nameField);
        SetField(nmui, "ipInputField",    ipField);
        SetField(nmui, "statusText",      statusTMP);

        var nd = Object.FindAnyObjectByType<NetworkDiscovery>();
        if (nd != null) SetField(nmui, "networkDiscovery", nd);

        // ── [11] Attach MainMenuRPGSetup ──────────────────────────────────────
        var setup = canvasGO.AddComponent<MainMenuRPGSetup>();
        SetField(setup, "networkManagerUI", nmui);
        SetField(setup, "titleGroup",       titleCG);
        SetField(setup, "buttonGroups",     new CanvasGroup[] { hostCG, joinCG, quitCG });
        SetField(setup, "joinPanel",        ipPanel);
        SetField(setup, "nameInputField",   nameField);
        SetField(setup, "ipInputField",     ipField);
        SetField(setup, "statusText",       statusTMP);

        // Assign the new Panels
        SetField(setup, "gamePanel",      gamePanel);
        SetField(setup, "characterPanel", charPanel);
        SetField(setup, "storePanel",     storePanel);

        // ── [12] Wire button onClick ──────────────────────────────────────────
        WireBtn(hostBtnGO,    setup, nameof(MainMenuRPGSetup.OnHostGame));
        WireBtn(joinBtnGO,    setup, nameof(MainMenuRPGSetup.OnJoinGame));
        WireBtn(connectBtnGO, setup, nameof(MainMenuRPGSetup.OnConnectWithIP));
        WireBtn(quitBtnGO,    setup, nameof(MainMenuRPGSetup.OnQuit));
        WireBtn(settingsBtnGO, setup, nameof(MainMenuRPGSetup.OnSettings));

        // ── Save ──────────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[RPGBuilder] ✅ RPG Main Menu built and saved!");
        EditorUtility.DisplayDialog("Done!",
            "RPG Main Menu built successfully!\n\nOpen MainMenu.unity and enter Play Mode to preview.", "Sweet!");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helper Methods
    // ══════════════════════════════════════════════════════════════════════════

    static GameObject Spawn(string path, Transform parent, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning($"[RPGBuilder] Missing prefab: {path}"); return null; }
        var go = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        go.name = name;
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static GameObject MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    static void SetAnchors(GameObject go, Vector2 min, Vector2 max)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static GameObject MakeImage(string name, Transform parent, Color color)
    {
        var go = MakeRect(name, parent, Vector2.zero, Vector2.one);
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    static GameObject MakeLabel(string name, Transform parent, string text, float size,
        FontStyles style, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.fontStyle = style;
        tmp.color     = color;
        tmp.alignment = align;
        return go;
    }

    static GameObject SpawnButton(string prefabPath, Transform parent, string name, string label)
    {
        var go = Spawn(prefabPath, parent, name);
        if (go == null)
        {
            // Fallback: plain button
            go = MakeRect(name, parent, Vector2.zero, Vector2.one);
            go.AddComponent<Image>().color = new Color(0.2f, 0.55f, 0.25f);
            go.AddComponent<Button>();
            var lbl = new GameObject("Label");
            lbl.transform.SetParent(go.transform, false);
            var t = lbl.AddComponent<TextMeshProUGUI>();
            t.text = label; t.alignment = TextAlignmentOptions.Center;
            Stretch(lbl);
        }
        // Set the button label text
        foreach (var tmp in go.GetComponentsInChildren<TextMeshProUGUI>())
        {
            if (tmp.transform != go.transform)   // skip root if it has one
            {
                tmp.text = label;
                break;
            }
        }
        return go;
    }

    static GameObject SpawnInput(string prefabPath, Transform parent, string name, string placeholder)
    {
        var go = Spawn(prefabPath, parent, name);
        if (go == null)
        {
            go = MakeRect(name, parent, Vector2.zero, Vector2.one);
            go.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        }
        foreach (var tmp in go.GetComponentsInChildren<TextMeshProUGUI>())
        {
            if (tmp.gameObject.name.ToLower().Contains("placeholder"))
            { tmp.text = placeholder; break; }
        }
        return go;
    }

    static GameObject MakeIconBtn(string name, Transform parent, string spritePath, System.Action onClick)
    {
        var go = MakeRect(name, parent, Vector2.zero, Vector2.one);
        var btn = go.AddComponent<Button>();
        var img = go.AddComponent<Image>();
        img.color = Color.white;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite != null) img.sprite = sprite;
        img.preserveAspect = true;
        // Add color tint on normal state
        var colors = btn.colors;
        colors.normalColor      = new Color(1, 1, 1, 0.75f);
        colors.highlightedColor = Color.white;
        btn.colors = colors;
        return go;
    }

    static Button GetBtn(GameObject go)
        => go?.GetComponent<Button>() ?? go?.GetComponentInChildren<Button>();

    static T GetOrAdd<T>(GameObject go) where T : Component
        => go.GetComponent<T>() ?? go.AddComponent<T>();

    static void SetField(object obj, string field, object value)
    {
        if (obj == null || value == null) return;
        var f = obj.GetType().GetField(field,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(obj, value);
        else Debug.LogWarning($"[RPGBuilder] Field '{field}' not found on {obj.GetType().Name}");
    }

    static void WireBtn(GameObject btnGO, MainMenuRPGSetup setup, string method)
    {
        var btn = GetBtn(btnGO);
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
            typeof(UnityEngine.Events.UnityAction), setup,
            typeof(MainMenuRPGSetup).GetMethod(method));
        UnityEventTools.AddVoidPersistentListener(btn.onClick, action);
    }

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }
}
#endif
