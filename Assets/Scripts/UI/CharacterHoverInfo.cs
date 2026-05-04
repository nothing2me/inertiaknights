using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class CharacterHoverInfo : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Configuration")]
    [TextArea(3, 10)]
    public string description = "Enter character description here...";
    
    [Header("UI References")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;

    [Header("Panel Styling")]
    public Vector2 padding = new Vector2(20, 20);
    public float maxWidth = 400f;

    private RectTransform panelRect;

    private void Awake()
    {
        // Auto-assign if missing
        if (infoPanel == null)
        {
            Transform panelTransform = transform.Find("CharacterInfoPanel");
            if (panelTransform == null) panelTransform = transform.Find("CharacterInfoPanel (1)");
            if (panelTransform == null) panelTransform = transform.Find("CharacterInfoPanel (2)");
            
            if (panelTransform != null) infoPanel = panelTransform.gameObject;
        }

        if (infoPanel != null)
        {
            panelRect = infoPanel.GetComponent<RectTransform>();
            if (infoText == null) infoText = infoPanel.GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    private void Start()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
    }

    private void Update()
    {
        // Keep panel sized to text if it's active
        if (infoPanel != null && infoPanel.activeSelf && infoText != null && panelRect != null)
        {
            ResizePanel();
        }
    }

    private void ResizePanel()
    {
        if (infoText == null || panelRect == null) return;

        // Force text to wrap at maxWidth - padding
        infoText.rectTransform.sizeDelta = new Vector2(maxWidth - padding.x, infoText.rectTransform.sizeDelta.y);
        
        Vector2 preferredSize = infoText.GetPreferredValues(maxWidth - padding.x, 0);
        float finalWidth = Mathf.Min(preferredSize.x + padding.x, maxWidth);
        panelRect.sizeDelta = new Vector2(finalWidth, preferredSize.y + padding.y);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (infoPanel != null && infoText != null)
        {
            infoText.text = description;
            infoPanel.SetActive(true);
            
            // Force immediate resize
            Canvas.ForceUpdateCanvases();
            ResizePanel();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }
    }
}
