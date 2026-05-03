using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Balls of Babel — Main Menu Controller.
/// Handles:
///   • Staggered fade-in: Title first → 3 s wait → everything else
///   • Tab navigation: Game / Store / Character with indicator line
///   • Button callbacks: Host, Join, Quit, Settings
/// </summary>
public class MainMenuRPGSetup : MonoBehaviour
{

    // ─── Fade-In ─────────────────────────────────────────────────────────
    [Header("── Fade-In ──")]
    [Tooltip("The 'Balls of Babel' title container. Auto-found by name if blank.")]
    [SerializeField] private CanvasGroup titleGroup;

    [Tooltip("All other top-level UI elements that fade in AFTER the title.")]
    [SerializeField] private CanvasGroup[] secondaryGroups;

    [SerializeField] private float titleFadeDuration    = 1.2f;
    [SerializeField] private float waitAfterTitle       = 0.0f;
    [SerializeField] private float elementFadeDuration  = 0.5f;
    [SerializeField] private float elementStaggerDelay  = 0.12f;

    // ─── Tab Navigation ──────────────────────────────────────────────────
    [Header("── Tab Navigation ──")]
    [Tooltip("Tab buttons — auto-discovered from the TopBar if blank.")]
    [SerializeField] private Button tabGame;
    [SerializeField] private Button tabStore;
    [SerializeField] private Button tabCharacter;

    [Tooltip("Underline indicator that shows which tab is active. Created automatically if blank.")]
    [SerializeField] private RectTransform tabIndicator;

    [Header("── Panels ──")]
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("── Input Fields ──")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TMP_InputField ipInputField;

    [Header("── Feedback ──")]
    [SerializeField] private TextMeshProUGUI statusText;

    private const string SceneMainMenu  = "MainMenu";
    private const string SceneStore     = "StoreScene";
    private const string SceneCharacter = "CharacterScene";
    private const string SceneSettings  = "SettingsScene";
    private const string SceneGame      = "MainScene";

    private static readonly Color IndicatorColor = new Color(1f, 0.80f, 0.20f, 1f); // gold

    // Captured target layout from the prefab's Selected_line
    private Vector2 targetAnchorMin = new Vector2(0.1f, -0.05f);
    private Vector2 targetAnchorMax = new Vector2(0.9f,  0.0f);
    private Vector2 targetOffsetMin = Vector2.zero;
    private Vector2 targetOffsetMax = Vector2.zero;
    private float targetHeight = 4f;

    // =====================================================================
    //  Lifecycle
    // =====================================================================

    private void Awake()
    {

        // Hide panels at start
        if (joinPanel != null)    joinPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (statusText != null)    statusText.text = string.Empty;

        // ── Auto-discover title group ────────────────────────────────────
        if (titleGroup == null)
        {
            var titleGO = FindInCanvas("BallsOfBabel_Title");
            if (titleGO != null)
                titleGroup = GetOrAddCanvasGroup(titleGO.gameObject);
        }

        // ── Auto-discover secondary groups ───────────────────────────────
        if (secondaryGroups == null || secondaryGroups.Length == 0)
            AutoDiscoverSecondaryGroups();

        // ── Auto-discover and wire tab buttons ───────────────────────────
        AutoDiscoverTabs();
        WireTabListeners();
        EnsureIndicator();

        // ── Auto-discover and wire menu buttons ─────────────────────────
        AutoWireMenuButtons();

        // ── Ensure decorative elements don't block input ────────────────
        FixRaycasts();
    }

    /// <summary>
    /// Disables Raycast Target on decorative elements like the title, 
    /// indicator, and backgrounds to ensure they don't block button clicks.
    /// </summary>
    private void FixRaycasts()
    {
        // 1. Disable raycasts on the title group
        if (titleGroup != null)
        {
            titleGroup.blocksRaycasts = false;
            titleGroup.interactable = false;
            // Also disable raycast on all child images just in case
            foreach (var tImg in titleGroup.GetComponentsInChildren<Image>(true))
            {
                tImg.raycastTarget = false;
            }
        }

        // 2. Disable raycasts on the gold indicator
        if (tabIndicator != null)
        {
            var iImg = tabIndicator.GetComponent<Image>();
            if (iImg != null) iImg.raycastTarget = false;
        }

        // 3. Scan for any "glow", "flare", or "overlay" objects that might be blocking
        foreach (var gImg in GetComponentsInChildren<Image>(true))
        {
            string n = gImg.gameObject.name.ToLower();
            if (n.Contains("glow") || n.Contains("flare") || n.Contains("overlay") || n.Contains("shadow"))
            {
                gImg.raycastTarget = false;
            }
        }
        
        // 4. Ensure backgrounds don't block
        var bg = FindInCanvas("Background_RPG");
        if (bg != null)
        {
            var bImg = bg.GetComponent<Image>();
            if (bImg != null) bImg.raycastTarget = false;
        }
        var bg2 = FindInCanvas("Background2_RPG");
        if (bg2 != null)
        {
            var bImg2 = bg2.GetComponent<Image>();
            if (bImg2 != null) bImg2.raycastTarget = false;
        }
    }


    private static bool hasPlayedFadeIn = false;

    private void Start()
    {
        if (!hasPlayedFadeIn)
        {
            hasPlayedFadeIn = true;
            StartCoroutine(FadeInSequence());
        }
        else
        {
            if (titleGroup != null) titleGroup.alpha = 1f;
            if (secondaryGroups != null)
            {
                foreach (var g in secondaryGroups)
                {
                    if (g != null) g.alpha = 1f;
                }
            }
        }

        // Game tab is active by default
        MoveIndicatorTo(tabGame);
    }

    // =====================================================================
    #region Auto-Discovery
    // =====================================================================

    /// <summary>
    /// Collects CanvasGroups on every top-level child of the Canvas except
    /// the title and any hidden panels (joinPanel, settingsPanel).
    /// </summary>
    private void AutoDiscoverSecondaryGroups()
    {
        var groups = new List<CanvasGroup>();

        foreach (Transform child in transform)
        {
            if (child.name == "BallsOfBabel_Title") continue;
            if (child.name.Contains("Background")) continue; // Backgrounds should always be visible

            // Skip hidden panels
            if (joinPanel != null && child.gameObject == joinPanel) continue;
            if (settingsPanel != null && child.gameObject == settingsPanel) continue;

            groups.Add(GetOrAddCanvasGroup(child.gameObject));
        }

        secondaryGroups = groups.ToArray();
    }

    /// <summary>
    /// Searches the TopBar hierarchy for buttons whose TMP labels contain
    /// "Game", "Store", or "Character" (case-insensitive).
    /// </summary>
    private void AutoDiscoverTabs()
    {
        // Only auto-discover if at least one tab is unassigned
        if (tabGame != null && tabStore != null && tabCharacter != null) return;

        // Try multiple common names for the top bar
        Transform topBar = FindInCanvas("TopBar_RPG")
                        ?? FindInCanvas("TopBar")
                        ?? FindInCanvas("Top-Bar")
                        ?? FindInCanvas("Tabs-Horizontal");

        if (topBar == null) return;

        foreach (var btn in topBar.GetComponentsInChildren<Button>(true))
        {
            string objName = btn.gameObject.name.ToLower();
            string text = "";
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) text = label.text.ToLower().Trim();

            if (tabGame == null && (text.Contains("game") || text.Contains("home") || text.Contains("play") || objName.Contains("game") || objName.Contains("home")))
                tabGame = btn;
            else if (tabStore == null && (text.Contains("store") || text.Contains("shop") || objName.Contains("store")))
                tabStore = btn;
            else if (tabCharacter == null && (text.Contains("character") || text.Contains("hero") || text.Contains("char") || objName.Contains("character")))
                tabCharacter = btn;
        }

        Debug.Log($"[MainMenuRPG] AutoDiscoverTabs Result -> Game: {tabGame?.name}, Store: {tabStore?.name}, Character: {tabCharacter?.name}");
    }

    private void WireTabListeners()
    {
        if (tabGame != null)      tabGame.onClick.AddListener(OnTabGame);
        if (tabStore != null)     tabStore.onClick.AddListener(OnTabStore);
        if (tabCharacter != null) tabCharacter.onClick.AddListener(OnTabCharacter);
    }

    /// <summary>
    /// Finds the existing 'Selected_line' from the prefab or creates one if missing.
    /// Captures its layout so it slides perfectly.
    /// </summary>
    private void EnsureIndicator()
    {
        if (tabIndicator != null) return;

        // 1) Try to find an existing "Selected_line" in the top bar
        Transform topBar = FindInCanvas("TopBar_RPG") ?? FindInCanvas("TopBar") ?? FindInCanvas("Tabs-Horizontal");
        if (topBar != null)
        {
            var allLines = topBar.GetComponentsInChildren<Transform>(true);
            foreach (var line in allLines)
            {
                // Check for "Selected_lin" to catch cases where the name was truncated in the hierarchy
                if (line.name.Contains("Selected_lin") || line.name == "TabIndicator")
                {
                    if (tabIndicator == null)
                    {
                        tabIndicator = line.GetComponent<RectTransform>();
                        tabIndicator.gameObject.SetActive(true);

                        // Ensure indicator doesn't block clicks
                        var foundImg = tabIndicator.GetComponent<Image>();
                        if (foundImg != null) foundImg.raycastTarget = false;
                        
                        // Capture the original design values from the prefab!
                        targetAnchorMin = tabIndicator.anchorMin;
                        targetAnchorMax = tabIndicator.anchorMax;
                        targetOffsetMin = tabIndicator.offsetMin;
                        targetOffsetMax = tabIndicator.offsetMax;
                        targetHeight = tabIndicator.sizeDelta.y;

                        Debug.Log($"[MainMenuRPG] Successfully found indicator: {line.name} under {line.parent.name}");
                    }
                    else if (line != tabIndicator)
                    {
                        // Destroy duplicate Selected_line objects under other tabs
                        Destroy(line.gameObject);
                    }
                }
            }
        }

        if (tabIndicator != null) return;

        // 2) Fallback if none found
        var go = new GameObject("TabIndicator");
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 4);

        var fallbackImg = go.AddComponent<Image>();
        fallbackImg.color = IndicatorColor;
        fallbackImg.raycastTarget = false;

        tabIndicator = rt;
    }

    /// <summary>
    /// Finds menu buttons by name OR by label text, then wires their
    /// onClick callbacks at runtime so they work even without the editor builder.
    /// </summary>
    private void AutoWireMenuButtons()
    {
        WireButtonByNameOrLabel("Btn_Quit",     "quit",       OnQuit);
        WireButtonByNameOrLabel("Btn_Host",     "host",       OnHostGame);
        WireButtonByNameOrLabel("Btn_Join",     "join",       OnJoinGame);
        WireButtonByNameOrLabel("Btn_Connect",  "connect",    OnConnectWithIP);
        WireButtonByNameOrLabel("Btn_Settings", "settings",   OnSettings);
    }

    private void WireButtonByNameOrLabel(string objectName, string labelKeyword,
        UnityEngine.Events.UnityAction callback)
    {
        Button btn = null;

        // 1) Try exact GameObject name
        var t = FindInCanvas(objectName);
        if (t != null)
            btn = t.GetComponent<Button>() ?? t.GetComponentInChildren<Button>();

        // 2) Fallback: scan ALL buttons in the canvas for a matching TMP label
        if (btn == null)
        {
            foreach (var candidate in GetComponentsInChildren<Button>(true))
            {
                var label = candidate.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null && label.text.ToLower().Trim().Contains(labelKeyword))
                {
                    btn = candidate;
                    break;
                }
            }
        }

        if (btn == null) return;

        btn.onClick.AddListener(callback);
        Debug.Log($"[MainMenu] Wired '{btn.gameObject.name}' → {callback.Method.Name}");
    }

    #endregion

    // =====================================================================
    #region Fade-In Animation
    // =====================================================================

    private IEnumerator FadeInSequence()
    {
        // ── Hide everything instantly ────────────────────────────────────
        if (titleGroup != null) titleGroup.alpha = 0f;

        if (secondaryGroups != null)
        {
            foreach (var g in secondaryGroups)
                if (g != null) g.alpha = 0f;
        }

        // ── Phase 1: Title fades in ──────────────────────────────────────
        if (titleGroup != null)
            yield return StartCoroutine(FadeGroup(titleGroup, 0f, 1f, titleFadeDuration));

        // ── Phase 2: Wait ────────────────────────────────────────────────
        if (waitAfterTitle > 0f)
            yield return new WaitForSeconds(waitAfterTitle);

        // ── Phase 3: Everything else staggers in ─────────────────────────
        if (secondaryGroups != null)
        {
            foreach (var g in secondaryGroups)
            {
                if (g == null) continue;
                StartCoroutine(FadeGroup(g, 0f, 1f, elementFadeDuration));
                yield return new WaitForSeconds(elementStaggerDelay);
            }
        }
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        group.alpha = to;
    }

    #endregion

    // =====================================================================
    #region Tab Navigation
    // =====================================================================

    public void OnTabGame()
    {
        Debug.Log("[MainMenuRPG] OnTabGame Clicked!");
        MoveIndicatorTo(tabGame);
        if (SceneManager.GetActiveScene().name != SceneMainMenu)
        {
            StartCoroutine(LoadSceneDelayed(SceneMainMenu, 0.25f));
        }
    }

    public void OnTabStore()
    {
        Debug.Log("[MainMenuRPG] OnTabStore Clicked!");
        MoveIndicatorTo(tabStore);
        StartCoroutine(LoadSceneDelayed(SceneStore, 0.25f));
    }

    public void OnTabCharacter()
    {
        Debug.Log("[MainMenuRPG] OnTabCharacter Clicked!");
        MoveIndicatorTo(tabCharacter);
        StartCoroutine(LoadSceneDelayed(SceneCharacter, 0.25f));
    }

    public void LoadSceneDelayedExplicit(string sceneName, float delay)
    {
        StartCoroutine(LoadSceneDelayed(sceneName, delay));
    }

    private IEnumerator LoadSceneDelayed(string sceneName, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        SceneManager.LoadScene(sceneName);
    }

    private Coroutine slideCoroutine;

    /// <summary>
    /// Positions the gold indicator line directly beneath the given tab button with a smooth slide animation.
    /// </summary>
    private void MoveIndicatorTo(Button tab)
    {
        if (tabIndicator == null || tab == null) return;

        var tabRT = tab.GetComponent<RectTransform>();
        if (tabRT == null) return;

        tabIndicator.gameObject.SetActive(true);

        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);

        if (gameObject.activeInHierarchy)
        {
            slideCoroutine = StartCoroutine(SlideRoutine(tabRT));
        }
        else
        {
            // Fallback if the object is inactive
            tabIndicator.SetParent(tabRT, false);
            tabIndicator.anchorMin = targetAnchorMin;
            tabIndicator.anchorMax = targetAnchorMax;
            tabIndicator.offsetMin = targetOffsetMin;
            tabIndicator.offsetMax = targetOffsetMax;
            tabIndicator.sizeDelta = new Vector2(tabIndicator.sizeDelta.x, targetHeight);
        }
    }

    private IEnumerator SlideRoutine(RectTransform targetRT)
    {
        // 1. Reparent to the target tab while keeping current world position
        tabIndicator.SetParent(targetRT, true);

        // 2. Capture the starting local layout values (representing old world position relative to new parent)
        Vector2 startAnchorMin = tabIndicator.anchorMin;
        Vector2 startAnchorMax = tabIndicator.anchorMax;
        Vector2 startOffsetMin = tabIndicator.offsetMin;
        Vector2 startOffsetMax = tabIndicator.offsetMax;

        // 3. Define the target layout values using captured prefab properties
        Vector2 endAnchorMin = targetAnchorMin;
        Vector2 endAnchorMax = targetAnchorMax;
        Vector2 endOffsetMin = targetOffsetMin;
        Vector2 endOffsetMax = targetOffsetMax;

        // 4. Slide duration
        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            // Smooth step for an ease-in ease-out effect
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            tabIndicator.anchorMin = Vector2.Lerp(startAnchorMin, endAnchorMin, t);
            tabIndicator.anchorMax = Vector2.Lerp(startAnchorMax, endAnchorMax, t);
            tabIndicator.offsetMin = Vector2.Lerp(startOffsetMin, endOffsetMin, t);
            tabIndicator.offsetMax = Vector2.Lerp(startOffsetMax, endOffsetMax, t);

            // Keep height locked
            tabIndicator.sizeDelta = new Vector2(tabIndicator.sizeDelta.x, targetHeight);

            yield return null;
        }

        // 5. Final snap to ensure exact positioning
        tabIndicator.anchorMin = endAnchorMin;
        tabIndicator.anchorMax = endAnchorMax;
        tabIndicator.offsetMin = endOffsetMin;
        tabIndicator.offsetMax = endOffsetMax;
        tabIndicator.sizeDelta = new Vector2(tabIndicator.sizeDelta.x, targetHeight);
    }

    #endregion

    // =====================================================================
    #region Button Callbacks
    // =====================================================================

    /// <summary>HOST GAME — sets auto-host flag, transfers name, loads MainScene.</summary>
    public void OnHostGame()
    {
        SetStatus(string.Empty);
        TransferPlayerName();
        NetworkManagerUI.AutoStartAsHost  = true;
        NetworkManagerUI.AutoStartAsClient = false;
        NetworkManagerUI.AutoConnectIP    = "";
        Debug.Log("[MainMenuRPG] Queueing Host → loading MainScene.");
        SceneManager.LoadScene(SceneGame);
    }

    /// <summary>JOIN GAME — toggles the IP panel so user can enter an address or just hit Connect for LAN.</summary>
    public void OnJoinGame()
    {
        if (joinPanel == null)
        {
            // No join panel — go straight to LAN search
            TransferPlayerName();
            NetworkManagerUI.AutoStartAsClient = true;
            NetworkManagerUI.AutoStartAsHost   = false;
            NetworkManagerUI.AutoConnectIP     = "";
            Debug.Log("[MainMenuRPG] Queueing LAN Join → loading MainScene.");
            SceneManager.LoadScene(SceneGame);
            return;
        }
        bool nowVisible = !joinPanel.activeSelf;
        joinPanel.SetActive(nowVisible);
        if (!nowVisible) SetStatus(string.Empty);
    }

    /// <summary>Connect button inside the IP panel — sets auto-client flag with IP, loads MainScene.</summary>
    public void OnConnectWithIP()
    {
        TransferPlayerName();
        string ip = "";
        if (ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text))
            ip = ipInputField.text.Trim();

        NetworkManagerUI.AutoStartAsClient = true;
        NetworkManagerUI.AutoStartAsHost   = false;
        NetworkManagerUI.AutoConnectIP     = ip;
        Debug.Log($"[MainMenuRPG] Queueing Client (IP='{ip}') → loading MainScene.");
        SceneManager.LoadScene(SceneGame);
    }

    /// <summary>QUIT — closes the application.</summary>
    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>SETTINGS — loads the settings scene.</summary>
    public void OnSettings()
    {
        Debug.Log("[MainMenuRPG] OnSettings Clicked!");
        StartCoroutine(LoadSceneDelayed(SceneSettings, 0.25f));
    }

    #endregion

    // =====================================================================
    #region Private Helpers
    // =====================================================================

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    /// <summary>
    /// Transfers the entered player name to NetworkManagerUI.LocalPlayerName
    /// so it persists across the scene transition.
    /// </summary>
    private void TransferPlayerName()
    {
        if (nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
            NetworkManagerUI.LocalPlayerName = nameInputField.text.Trim();
    }

    /// <summary>
    /// Finds a Transform by name anywhere under this Canvas.
    /// </summary>
    private Transform FindInCanvas(string objectName)
    {
        return FindChildRecursive(transform, objectName);
    }

    private static Transform FindChildRecursive(Transform parent, string nameContains)
    {
        foreach (Transform child in parent)
        {
            if (child.name == nameContains) return child;
            var found = FindChildRecursive(child, nameContains);
            if (found != null) return found;
        }
        return null;
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    #endregion
}
