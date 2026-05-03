using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Simple UI script that scales an element up when hovered.
/// Provides that "pop-up" feel for store items.
/// </summary>
public class UIHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Settings")]
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float animationSpeed = 10f;

    private Vector3 originalScale;
    private Vector3 targetScale;

    private void Awake()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    private void Update()
    {
        // Smoothly lerp towards the target scale
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * animationSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    private void OnDisable()
    {
        // Reset scale if disabled while hovering
        transform.localScale = originalScale;
        targetScale = originalScale;
    }
}
