using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Full-screen class selection overlay built entirely in code.
/// Three large floating class sprites that scale on hover and show a tooltip.
/// </summary>
public class ClassSelectionUI : MonoBehaviour
{
    private PlayerClassManager manager;
    private CanvasGroup canvasGroup;
    private Toggle aiToggle;

    private TextMeshProUGUI tooltipTitle;
    private TextMeshProUGUI tooltipDesc;
    private CanvasGroup tooltipGroup;
    private Coroutine tooltipCoroutine;

    // ─── Colours & Style ──────────────────────────────────────────
    private static readonly Color LIGHT_ACCENT      = new Color(0.8f, 0.2f, 0.2f); // Crimson red
    private static readonly Color HEALER_ACCENT     = new Color(0.2f, 0.6f, 0.3f); // Forest green
    private static readonly Color TANK_ACCENT       = new Color(0.2f, 0.3f, 0.8f); // Royal blue

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
        RectTransform root = gameObject.GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        // Canvas group for fade
        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // ── Card Container ──
        GameObject containerGO = CreateChild("CardContainer", root);
        RectTransform containerRT = containerGO.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.pivot = new Vector2(0.5f, 0.5f);
        containerRT.anchoredPosition = new Vector2(0, -150f);
        containerRT.sizeDelta = new Vector2(1800, 800);

        HorizontalLayoutGroup hlg = containerGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = -80; // Move them even closer together (negative spacing overlaps transparent edges)
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // ── Fetch Resources ──
        Texture2D texLight = Resources.Load<Texture2D>("knight_class_card");
        Texture2D texHealer = Resources.Load<Texture2D>("healer_class_card");
        Texture2D texTank = Resources.Load<Texture2D>("heavy_class_card");

        // ── Build Cards ──
        BuildClassCard(containerRT, PlayerClassType.Light, texLight);
        BuildClassCard(containerRT, PlayerClassType.Healer, texHealer);
        BuildClassCard(containerRT, PlayerClassType.Tank, texTank);

        // ── Tooltip Window ──
        GameObject ttGO = CreateChild("Tooltip", root);
        RectTransform ttRT = ttGO.GetComponent<RectTransform>();
        ttRT.anchorMin = new Vector2(0.5f, 0f);
        ttRT.anchorMax = new Vector2(0.5f, 0f);
        ttRT.pivot = new Vector2(0.5f, 0f);
        ttRT.anchoredPosition = new Vector2(0, 80f); // Moved tooltip up slightly to make room for larger cards
        ttRT.sizeDelta = new Vector2(600, 160);

        Image ttBG = ttGO.AddComponent<Image>();
        ttBG.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        
        tooltipGroup = ttGO.AddComponent<CanvasGroup>();
        tooltipGroup.alpha = 0f;

        GameObject titleGO = CreateChild("Title", ttRT);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1);
        titleRT.anchoredPosition = new Vector2(0, -10f);
        titleRT.sizeDelta = new Vector2(0, 40);

        tooltipTitle = titleGO.AddComponent<TextMeshProUGUI>();
        tooltipTitle.fontSize = 28;
        tooltipTitle.alignment = TextAlignmentOptions.Center;
        tooltipTitle.fontStyle = FontStyles.Bold;

        GameObject descGO = CreateChild("Desc", ttRT);
        RectTransform descRT = descGO.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0, 0);
        descRT.anchorMax = new Vector2(1, 1);
        descRT.offsetMin = new Vector2(20, 20);
        descRT.offsetMax = new Vector2(-20, -50);

        tooltipDesc = descGO.AddComponent<TextMeshProUGUI>();
        tooltipDesc.fontSize = 20;
        tooltipDesc.alignment = TextAlignmentOptions.TopLeft;
        tooltipDesc.color = Color.white;

        // ── AI Team Toggle ──
        GameObject toggleGO = CreateChild("AIToggle", root);
        RectTransform toggleRT = toggleGO.GetComponent<RectTransform>();
        toggleRT.anchorMin = new Vector2(1f, 0f);
        toggleRT.anchorMax = new Vector2(1f, 0f);
        toggleRT.pivot = new Vector2(1f, 0f);
        toggleRT.anchoredPosition = new Vector2(-20f, 20f);
        toggleRT.sizeDelta = new Vector2(250, 40);

        HorizontalLayoutGroup toggleHLG = toggleGO.AddComponent<HorizontalLayoutGroup>();
        toggleHLG.childAlignment = TextAnchor.MiddleRight;
        toggleHLG.spacing = 10;
        toggleHLG.childControlWidth = false;
        toggleHLG.childControlHeight = false;

        GameObject labelGO = CreateChild("Label", toggleRT);
        TextMeshProUGUI labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = "Summon AI Bots";
        labelTMP.fontSize = 20;
        labelTMP.color = Color.white;
        labelTMP.alignment = TextAlignmentOptions.MidlineRight;

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

        aiToggle = toggleGO.AddComponent<Toggle>();
        aiToggle.targetGraphic = bgImg;
        aiToggle.graphic = checkmarkImg;
        aiToggle.isOn = false;
    }

    private void BuildClassCard(RectTransform parent, PlayerClassType classType, Texture2D tex)
    {
        GameObject cardGO = CreateChild($"Card_{classType}", parent);
        RectTransform cardRT = cardGO.GetComponent<RectTransform>();
        
        // Much larger size, preserving native aspect ratio
        cardRT.sizeDelta = new Vector2(525, 669); 

        if (tex != null)
        {
            RawImage img = cardGO.AddComponent<RawImage>();
            img.texture = tex;

            // Add drop shadow for depth
            Shadow shadow = cardGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f);
            shadow.effectDistance = new Vector2(25f, -25f);
        }
        else
        {
            Image img = cardGO.AddComponent<Image>();
            img.color = Color.magenta; // Missing texture indicator
        }

        Button btn = cardGO.AddComponent<Button>();
        btn.transition = Selectable.Transition.None; // We handle visual changes via scale
        
        PlayerClassType captured = classType;
        btn.onClick.AddListener(() => {
            if (manager != null) manager.SelectClass(captured, aiToggle != null ? aiToggle.isOn : false);
        });

        ClassCardHoverHandler hover = cardGO.AddComponent<ClassCardHoverHandler>();
        hover.ui = this;
        hover.classType = classType;
    }

    // ─── Tooltip Logic ────────────────────────────────────────────

    public void ShowTooltip(PlayerClassType classType)
    {
        string title = "";
        string desc = "";
        Color accent = Color.white;

        switch (classType)
        {
            case PlayerClassType.Light:
                title = "LIGHT KNIGHT";
                accent = LIGHT_ACCENT;
                desc = "<b>HP:</b> 80   |   <b>SPD:</b> High   |   <b>DMG:</b> Crit Focus\n\nGrapple to surfaces, dash mid-air to rescue yourself from falls, and land deadly critical strikes.";
                break;
            case PlayerClassType.Healer:
                title = "HEALER MAGE";
                accent = HEALER_ACCENT;
                desc = "<b>HP:</b> 100  |   <b>SPD:</b> Med    |   <b>DMG:</b> Low\n\nLock-on heal beam, burst AOE heal, and an Uber meter that grants team immortality.";
                break;
            case PlayerClassType.Tank:
                title = "HEAVY TANK";
                accent = TANK_ACCENT;
                desc = "<b>HP:</b> 200  |   <b>SPD:</b> Low    |   <b>DMG:</b> Heavy\n\nImmovable shield, heavy ground slam that instantly crushes light enemies.";
                break;
        }

        tooltipTitle.text = title;
        tooltipTitle.color = accent;
        tooltipDesc.text = desc;
        
        if (tooltipCoroutine != null) StopCoroutine(tooltipCoroutine);
        tooltipCoroutine = StartCoroutine(FadeTooltip(1f));
    }

    public void HideTooltip()
    {
        if (tooltipCoroutine != null) StopCoroutine(tooltipCoroutine);
        tooltipCoroutine = StartCoroutine(FadeTooltip(0f));
    }

    private System.Collections.IEnumerator FadeTooltip(float targetAlpha)
    {
        float startAlpha = tooltipGroup.alpha;
        float t = 0;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            tooltipGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / 0.15f);
            yield return null;
        }
        tooltipGroup.alpha = targetAlpha;
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

// ─── Hover Handler ───────────────────────────────────────────────

public class ClassCardHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public ClassSelectionUI ui;
    public PlayerClassType classType;
    private RectTransform rect;
    private Coroutine scaleCoroutine;
    
    private void Awake() 
    { 
        rect = GetComponent<RectTransform>(); 
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleTo(1.2f)); // Scale up by 20%
        ui.ShowTooltip(classType);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleTo(1f));
        ui.HideTooltip();
    }

    private System.Collections.IEnumerator ScaleTo(float target)
    {
        Vector3 startScale = rect.localScale;
        Vector3 endScale = new Vector3(target, target, 1f);
        float t = 0;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            rect.localScale = Vector3.Lerp(startScale, endScale, t / 0.15f);
            yield return null;
        }
        rect.localScale = endScale;
    }
}
