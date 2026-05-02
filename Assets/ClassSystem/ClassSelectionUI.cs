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
        // Root RectTransform stretches full-screen
        RectTransform root = gameObject.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        // Background overlay
        Image bg = gameObject.AddComponent<Image>();
        bg.color = BG_COLOR;
        bg.raycastTarget = true;

        // Canvas group for fade
        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Content root for scaling
        GameObject contentGO = CreateChild("Content", root);
        RectTransform contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = Vector2.zero;
        contentRT.anchorMax = Vector2.one;
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;
        contentRT.localScale = new Vector3(0.75f, 0.75f, 1f); // Scale down by 25%

        // ── Title ──
        GameObject titleGO = CreateChild("Title", contentRT);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0, 60f); // Moved up to be just above the frames
        titleRT.sizeDelta = new Vector2(800, 80);

        TextMeshProUGUI titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "CHOOSE THY CLASS";
        titleText.fontSize = 48; // Scaled down text size
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.black;
        titleText.fontStyle = FontStyles.Bold;

        // ── Subtitle ──
        GameObject subtitleGO = CreateChild("Subtitle", contentRT);
        RectTransform subRT = subtitleGO.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0.5f, 1f);
        subRT.anchorMax = new Vector2(0.5f, 1f);
        subRT.pivot = new Vector2(0.5f, 1f);
        subRT.anchoredPosition = new Vector2(0, 10f); // Moved up proportionally
        subRT.sizeDelta = new Vector2(800, 40);

        TextMeshProUGUI subText = subtitleGO.AddComponent<TextMeshProUGUI>();
        subText.text = "Each class changes how you play. Choose wisely.";
        subText.fontSize = 24;
        subText.alignment = TextAlignmentOptions.Center;
        subText.color = Color.black;

        // ── Card Container (horizontal layout) ──
        GameObject containerGO = CreateChild("CardContainer", contentRT);
        RectTransform containerRT = containerGO.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.pivot = new Vector2(0.5f, 0.5f);
        containerRT.anchoredPosition = new Vector2(0, -20f);
        containerRT.sizeDelta = new Vector2(1200, 600); // Larger to fit big text

        HorizontalLayoutGroup hlg = containerGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(10, 10, 10, 10);

        // ── Build Each Card ──
        BuildClassCard(containerRT, PlayerClassType.Light,  LIGHT_ACCENT,
            "⚡ LIGHT",
            "Speed demon with critical strikes.\nGrapple to surfaces, rescue dash\nwhen falling, and chance to deal\nmassive crit damage.",
            new StatBar[]
            {
                new StatBar("Speed",   0.95f, LIGHT_ACCENT),
                new StatBar("Health",  0.35f, new Color(1f,0.3f,0.3f)),
                new StatBar("Damage",  0.85f, new Color(1f,0.8f,0.2f)),
                new StatBar("Utility", 0.70f, new Color(0.6f,0.8f,1f)),
            });

        BuildClassCard(containerRT, PlayerClassType.Healer, HEALER_ACCENT,
            "💚 HEALER",
            "Team lifeline with heal beam.\nAOE burst heal, health on kill,\nand an Uber meter that grants\nimmortality or ally teleport.",
            new StatBar[]
            {
                new StatBar("Speed",   0.60f, LIGHT_ACCENT),
                new StatBar("Health",  0.55f, new Color(1f,0.3f,0.3f)),
                new StatBar("Damage",  0.35f, new Color(1f,0.8f,0.2f)),
                new StatBar("Utility", 1.00f, new Color(0.6f,0.8f,1f)),
            });

        BuildClassCard(containerRT, PlayerClassType.Tank,   TANK_ACCENT,
            "🛡️ TANK",
            "Immovable fortress with shield HP.\nHeavy slam kills light enemies,\nbubble shield protects the team.\nSlow but nearly unkillable.",
            new StatBar[]
            {
                new StatBar("Speed",   0.30f, LIGHT_ACCENT),
                new StatBar("Health",  1.00f, new Color(1f,0.3f,0.3f)),
                new StatBar("Damage",  0.65f, new Color(1f,0.8f,0.2f)),
                new StatBar("Utility", 0.75f, new Color(0.6f,0.8f,1f)),
            });

        // ── AI Team Toggle (Placed to the right of the cards) ──
        GameObject toggleGO = CreateChild("AIToggle", contentRT);
        RectTransform toggleRT = toggleGO.GetComponent<RectTransform>();
        toggleRT.anchorMin = new Vector2(0.5f, 0.5f);
        toggleRT.anchorMax = new Vector2(0.5f, 0.5f);
        toggleRT.pivot = new Vector2(0f, 0.5f); // Left pivot so it flows rightwards
        toggleRT.anchoredPosition = new Vector2(620f, -20f); // 1200 / 2 = 600 + 20 spacing
        toggleRT.sizeDelta = new Vector2(250, 200);

        VerticalLayoutGroup toggleVLG = toggleGO.AddComponent<VerticalLayoutGroup>();
        toggleVLG.childAlignment = TextAnchor.MiddleCenter;
        toggleVLG.spacing = 15;
        toggleVLG.childControlWidth = false;
        toggleVLG.childControlHeight = false;

        // The checkbox background
        GameObject bgGO = CreateChild("Background", toggleRT);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.6f, 0.5f, 0.4f);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.sizeDelta = new Vector2(50, 50);

        // The checkmark
        GameObject checkmarkGO = CreateChild("Checkmark", bgRT);
        RectTransform checkmarkRT = checkmarkGO.GetComponent<RectTransform>();
        checkmarkRT.anchorMin = Vector2.zero;
        checkmarkRT.anchorMax = Vector2.one;
        checkmarkRT.offsetMin = new Vector2(6, 6);
        checkmarkRT.offsetMax = new Vector2(-6, -6);
        Image checkmarkImg = checkmarkGO.AddComponent<Image>();
        checkmarkImg.color = LIGHT_ACCENT;

        // The text
        GameObject labelGO = CreateChild("Label", toggleRT);
        TextMeshProUGUI labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = "Summon AI Knights?\n<size=16>(Warning: Mostly useless)</size>";
        labelTMP.fontSize = 24;
        labelTMP.color = Color.black;
        labelTMP.alignment = TextAlignmentOptions.Center;

        // The toggle component
        aiToggle = toggleGO.AddComponent<Toggle>();
        aiToggle.targetGraphic = bgImg;
        aiToggle.graphic = checkmarkImg;
        aiToggle.isOn = false;

        // Ensure title and subtitle render ON TOP of everything else just in case
        titleGO.transform.SetAsLastSibling();
        subtitleGO.transform.SetAsLastSibling();
    }

    // ─── Card Builder ─────────────────────────────────────────────

    private struct StatBar
    {
        public string label;
        public float  fill; // 0-1
        public Color  color;
        public StatBar(string l, float f, Color c) { label = l; fill = f; color = c; }
    }

    private void BuildClassCard(RectTransform parent, PlayerClassType classType,
        Color accent, string title, string description, StatBar[] stats)
    {
        // Card root
        GameObject cardGO = CreateChild($"Card_{classType}", parent);
        Image cardBG = cardGO.AddComponent<Image>();
        cardBG.color = CARD_BG;

        VerticalLayoutGroup vlg = cardGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(15, 15, 15, 15);

        // ── Accent bar at top ──
        GameObject accentBar = CreateChild("Accent", cardGO.GetComponent<RectTransform>());
        Image accentImg = accentBar.AddComponent<Image>();
        accentImg.color = accent;
        LayoutElement accentLE = accentBar.AddComponent<LayoutElement>();
        accentLE.preferredHeight = 4;
        accentLE.flexibleWidth = 1;

        // ── Title ──
        GameObject titleGO = CreateChild("Title", cardGO.GetComponent<RectTransform>());
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = title;
        titleTMP.fontSize = 40;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = accent;
        titleTMP.fontStyle = FontStyles.Bold;
        LayoutElement titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 40;

        // ── Description ──
        GameObject descGO = CreateChild("Desc", cardGO.GetComponent<RectTransform>());
        TextMeshProUGUI descTMP = descGO.AddComponent<TextMeshProUGUI>();
        descTMP.text = description;
        descTMP.fontSize = 20;
        descTMP.alignment = TextAlignmentOptions.Center;
        descTMP.color = Color.black;
        LayoutElement descLE = descGO.AddComponent<LayoutElement>();
        descLE.preferredHeight = 130;

        // ── Spacer ──
        GameObject spacer1 = CreateChild("Spacer", cardGO.GetComponent<RectTransform>());
        LayoutElement sp1 = spacer1.AddComponent<LayoutElement>();
        sp1.preferredHeight = 5;

        // ── Stat Bars ──
        foreach (var stat in stats)
        {
            BuildStatBar(cardGO.GetComponent<RectTransform>(), stat);
        }

        // ── Spacer ──
        GameObject spacer2 = CreateChild("Spacer2", cardGO.GetComponent<RectTransform>());
        LayoutElement sp2 = spacer2.AddComponent<LayoutElement>();
        sp2.flexibleHeight = 1;

        // ── Select Button ──
        GameObject btnGO = CreateChild("SelectBtn", cardGO.GetComponent<RectTransform>());
        Image btnImg = btnGO.AddComponent<Image>();
        btnImg.color = accent;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = accent;
        cb.highlightedColor = Color.Lerp(accent, Color.white, 0.3f);
        cb.pressedColor = Color.Lerp(accent, Color.black, 0.2f);
        btn.colors = cb;

        LayoutElement btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 60;

        // Button text
        GameObject btnTextGO = CreateChild("Text", btnGO.GetComponent<RectTransform>());
        RectTransform btnTextRT = btnTextGO.GetComponent<RectTransform>();
        btnTextRT.anchorMin = Vector2.zero;
        btnTextRT.anchorMax = Vector2.one;
        btnTextRT.offsetMin = Vector2.zero;
        btnTextRT.offsetMax = Vector2.zero;

        TextMeshProUGUI btnText = btnTextGO.AddComponent<TextMeshProUGUI>();
        btnText.text = "SELECT";
        btnText.fontSize = 28;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = BTN_TEXT_COLOR;
        btnText.fontStyle = FontStyles.Bold;

        // Wire up button
        PlayerClassType captured = classType;
        btn.onClick.AddListener(() => {
            if (manager != null) manager.SelectClass(captured, aiToggle != null ? aiToggle.isOn : false);
        });
    }

    private void BuildStatBar(RectTransform parent, StatBar stat)
    {
        // Row container
        GameObject rowGO = CreateChild($"Stat_{stat.label}", parent);
        LayoutElement rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 32;

        // Label
        GameObject labelGO = CreateChild("Label", rowGO.GetComponent<RectTransform>());
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0, 0);
        labelRT.anchorMax = new Vector2(0.35f, 1);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        TextMeshProUGUI labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = stat.label;
        labelTMP.fontSize = 18;
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        labelTMP.color = Color.black;

        // Bar background
        GameObject barBG = CreateChild("BarBG", rowGO.GetComponent<RectTransform>());
        RectTransform barBGRT = barBG.GetComponent<RectTransform>();
        barBGRT.anchorMin = new Vector2(0.37f, 0.2f);
        barBGRT.anchorMax = new Vector2(1f, 0.8f);
        barBGRT.offsetMin = Vector2.zero;
        barBGRT.offsetMax = Vector2.zero;

        Image barBGImg = barBG.AddComponent<Image>();
        barBGImg.color = STAT_BAR_BG;

        // Bar fill
        GameObject barFill = CreateChild("Fill", barBGRT);
        RectTransform fillRT = barFill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(stat.fill, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        Image fillImg = barFill.AddComponent<Image>();
        fillImg.color = stat.color;
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
