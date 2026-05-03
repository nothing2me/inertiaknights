using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Full-screen class selection overlay built entirely in code.
/// Three class cards (Light, Healer, Tank) with stats, descriptions, and select buttons.
/// </summary>
public class ClassSelectionUI : MonoBehaviour
{
    private PlayerClassManager manager;
    private CanvasGroup canvasGroup;
    private Toggle aiToggle;

    // ─── Colours & Style ──────────────────────────────────────────
    private static readonly Color BG_COLOR          = new Color(0.85f, 0.78f, 0.65f, 0.95f); // Parchment
    private static readonly Color CARD_BG           = new Color(0.75f, 0.65f, 0.5f, 0.95f); // Darker parchment/wood
    private static readonly Color CARD_HOVER        = new Color(0.8f, 0.7f, 0.55f, 1f);
    private static readonly Color LIGHT_ACCENT      = new Color(0.8f, 0.2f, 0.2f); // Crimson red
    private static readonly Color HEALER_ACCENT     = new Color(0.2f, 0.6f, 0.3f); // Forest green
    private static readonly Color TANK_ACCENT       = new Color(0.2f, 0.3f, 0.8f); // Royal blue
    private static readonly Color BTN_TEXT_COLOR    = new Color(0.9f, 0.85f, 0.7f); // Light text for buttons
    private static readonly Color STAT_BAR_BG       = new Color(0.5f, 0.4f, 0.3f, 0.8f);

    // ─── Public API ───────────────────────────────────────────────

    public void Initialize(PlayerClassManager mgr)
    {
        manager = mgr;
        BuildUI();

        // Unlock cursor for menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        canvasGroup.alpha = 0f;
        StartCoroutine(FadeIn());
    }

    private System.Collections.IEnumerator FadeIn()
    {
        float t = 0;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / 0.25f);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    public void Hide()
    {
        // Re-lock cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        StartCoroutine(FadeOutAndDestroy());
    }

    private System.Collections.IEnumerator FadeOutAndDestroy()
    {
        float t = 0;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.25f);
            yield return null;
        }
        Destroy(gameObject);
    }

    // ─── UI Construction ──────────────────────────────────────────

    private void BuildUI()
    {
        // Root RectTransform stretches full-screen for background dimming
        RectTransform root = gameObject.GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        // Background overlay (invisible but blocks clicks)
        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = true;

        // Canvas group for fade
        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // ── Main Frame ──
        GameObject frameGO = CreateChild("MainFrame", root);
        RectTransform frameRT = frameGO.GetComponent<RectTransform>();
        frameRT.anchorMin = new Vector2(0.5f, 0.5f);
        frameRT.anchorMax = new Vector2(0.5f, 0.5f);
        frameRT.pivot = new Vector2(0.5f, 0.5f);
        frameRT.anchoredPosition = new Vector2(0, -300f); // Moved down slightly
        frameRT.sizeDelta = new Vector2(600, 350);

        Image frameBG = frameGO.AddComponent<Image>();
        frameBG.color = CARD_BG;

        // ── Title ──
        GameObject titleGO = CreateChild("Title", frameRT);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0, -20f);
        titleRT.sizeDelta = new Vector2(500, 50);

        TextMeshProUGUI titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "CHOOSE THY CLASS";
        titleText.fontSize = 36;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.black;
        titleText.fontStyle = FontStyles.Bold;

        // ── Button Container ──
        GameObject containerGO = CreateChild("ButtonContainer", frameRT);
        RectTransform containerRT = containerGO.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.pivot = new Vector2(0.5f, 0.5f);
        containerRT.anchoredPosition = new Vector2(0, -10f);
        containerRT.sizeDelta = new Vector2(550, 200);

        HorizontalLayoutGroup hlg = containerGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // ── Fetch Sprites ──
        BallController ball = manager.GetComponent<BallController>();
        BillboardSpriteAnimator animator = ball.GetComponentInChildren<BillboardSpriteAnimator>(true);

        Texture2D texLight = animator != null ? animator.lightClassSpritesheet : null;
        Texture2D texHealer = animator != null ? animator.healerClassSpritesheet : null;
        Texture2D texTank = animator != null ? animator.tankClassSpritesheet : null;

        // ── Build Buttons ──
        BuildClassButton(containerRT, PlayerClassType.Light, texLight, LIGHT_ACCENT, "LIGHT");
        BuildClassButton(containerRT, PlayerClassType.Healer, texHealer, HEALER_ACCENT, "HEALER");
        BuildClassButton(containerRT, PlayerClassType.Tank, texTank, TANK_ACCENT, "TANK");

        // ── AI Team Toggle ──
        GameObject toggleGO = CreateChild("AIToggle", frameRT);
        RectTransform toggleRT = toggleGO.GetComponent<RectTransform>();
        toggleRT.anchorMin = new Vector2(0.5f, 0f);
        toggleRT.anchorMax = new Vector2(0.5f, 0f);
        toggleRT.pivot = new Vector2(0.5f, 0f);
        toggleRT.anchoredPosition = new Vector2(0f, 10f);
        toggleRT.sizeDelta = new Vector2(250, 40);

        HorizontalLayoutGroup toggleHLG = toggleGO.AddComponent<HorizontalLayoutGroup>();
        toggleHLG.childAlignment = TextAnchor.MiddleCenter;
        toggleHLG.spacing = 10;
        toggleHLG.childControlWidth = false;
        toggleHLG.childControlHeight = false;

        GameObject bgGO = CreateChild("Background", toggleRT);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.6f, 0.5f, 0.4f);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.sizeDelta = new Vector2(30, 30);

        GameObject checkmarkGO = CreateChild("Checkmark", bgRT);
        RectTransform checkmarkRT = checkmarkGO.GetComponent<RectTransform>();
        checkmarkRT.anchorMin = Vector2.zero;
        checkmarkRT.anchorMax = Vector2.one;
        checkmarkRT.offsetMin = new Vector2(4, 4);
        checkmarkRT.offsetMax = new Vector2(-4, -4);
        Image checkmarkImg = checkmarkGO.AddComponent<Image>();
        checkmarkImg.color = Color.white;

        GameObject labelGO = CreateChild("Label", toggleRT);
        TextMeshProUGUI labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = "Summon AI Bots";
        labelTMP.fontSize = 20;
        labelTMP.color = Color.black;
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;

        aiToggle = toggleGO.AddComponent<Toggle>();
        aiToggle.targetGraphic = bgImg;
        aiToggle.graphic = checkmarkImg;
        aiToggle.isOn = false;
    }

    private void BuildClassButton(RectTransform parent, PlayerClassType classType, Texture2D tex, Color accent, string name)
    {
        GameObject btnGO = CreateChild($"Btn_{classType}", parent);
        Image btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.85f, 0.8f, 0.7f, 1f);

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = Color.Lerp(Color.white, accent, 0.3f);
        cb.pressedColor = accent;
        btn.colors = cb;

        // Image container
        GameObject imgGO = CreateChild("Sprite", btnGO.GetComponent<RectTransform>());
        RectTransform imgRT = imgGO.GetComponent<RectTransform>();
        imgRT.anchorMin = new Vector2(0.1f, 0.2f);
        imgRT.anchorMax = new Vector2(0.9f, 0.9f);
        imgRT.offsetMin = Vector2.zero;
        imgRT.offsetMax = Vector2.zero;

        if (tex != null)
        {
            RawImage rawImg = imgGO.AddComponent<RawImage>();
            rawImg.texture = tex;
            // Slice the idle forward frame: col 3, row 0 in a 4x3 sheet
            // UVs: x=0.75, y=0.666, w=0.25, h=0.333
            rawImg.uvRect = new Rect(0.75f, 2f / 3f, 0.25f, 1f / 3f);
        }

        // Label at bottom
        GameObject labelGO = CreateChild("Label", btnGO.GetComponent<RectTransform>());
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0f);
        labelRT.anchorMax = new Vector2(1f, 0.2f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        TextMeshProUGUI labelText = labelGO.AddComponent<TextMeshProUGUI>();
        labelText.text = name;
        labelText.fontSize = 24;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = accent;
        labelText.fontStyle = FontStyles.Bold;

        // Wire click
        PlayerClassType captured = classType;
        btn.onClick.AddListener(() => {
            if (manager != null) manager.SelectClass(captured, aiToggle != null ? aiToggle.isOn : false);
        });
    }

    // ─── Helpers ──────────────────────────────────────────────────

    private GameObject CreateChild(string name, RectTransform parent)
    {
        return CreateChild(name, (Transform)parent);
    }

    private GameObject CreateChild(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
}
