#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public static class AddItemGridToScene
{
    [MenuItem("BallsOfBabel/Add Item Grid to Open Scene", priority = 17)]
    public static void AddItemGrid()
    {
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error", "No Canvas found in the active scene!", "OK");
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        string buttonText = "Item";

        if (sceneName.Contains("Store"))
        {
            buttonText = "Cost: 100";
        }
        else if (sceneName.Contains("Character"))
        {
            buttonText = "Equip";
        }
        else
        {
            if (!EditorUtility.DisplayDialog("Warning", 
                $"You are not in the StoreScene or CharacterScene.\nCurrent scene: {sceneName}\n\nDo you still want to generate the grid?", "Yes", "No"))
            {
                return;
            }
        }

        // 1. SCROLL VIEW (Main wrapper)
        var scrollViewGO = new GameObject("ItemGridScrollView");
        scrollViewGO.transform.SetParent(canvas.transform, false);
        var scrollViewRT = scrollViewGO.AddComponent<RectTransform>();
        scrollViewRT.anchorMin = new Vector2(0.2f, 0.25f);
        scrollViewRT.anchorMax = new Vector2(0.8f, 0.75f);
        scrollViewRT.offsetMin = Vector2.zero;
        scrollViewRT.offsetMax = Vector2.zero;
        
        var scrollRect = scrollViewGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false; // Vertical scroll only
        scrollRect.scrollSensitivity = 35f;

        // 2. VIEWPORT
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollViewGO.transform, false);
        var viewportRT = viewportGO.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;
        viewportGO.AddComponent<RectMask2D>(); 

        // 3. CONTENT
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        var glg = contentGO.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(160, 200);
        glg.spacing = new Vector2(25, 25);
        glg.padding = new RectOffset(20, 20, 20, 20);
        glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment = TextAnchor.UpperCenter;

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.MinSize;

        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;

        // 4. VISUAL SCROLLBAR
        var scrollbarGO = new GameObject("Scrollbar");
        scrollbarGO.transform.SetParent(scrollViewGO.transform, false);
        var scrollbarRT = scrollbarGO.AddComponent<RectTransform>();
        scrollbarRT.anchorMin = new Vector2(1f, 0f);
        scrollbarRT.anchorMax = new Vector2(1f, 1f);
        scrollbarRT.pivot = new Vector2(1f, 0.5f);
        scrollbarRT.sizeDelta = new Vector2(20f, 0f);
        scrollbarRT.anchoredPosition = new Vector2(-10f, 0f);

        var scrollbarBg = scrollbarGO.AddComponent<Image>();
        scrollbarBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        var slidingAreaGO = new GameObject("SlidingArea");
        slidingAreaGO.transform.SetParent(scrollbarGO.transform, false);
        var slidingAreaRT = slidingAreaGO.AddComponent<RectTransform>();
        slidingAreaRT.anchorMin = Vector2.zero;
        slidingAreaRT.anchorMax = Vector2.one;
        slidingAreaRT.offsetMin = new Vector2(4, 4);
        slidingAreaRT.offsetMax = new Vector2(-4, -4);

        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(slidingAreaGO.transform, false);
        var handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.offsetMin = Vector2.zero;
        handleRT.offsetMax = Vector2.zero;

        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = new Color(0.5f, 0.5f, 0.5f, 1f); 

        var scrollbar = scrollbarGO.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImg;
        scrollbar.handleRect = handleRT;

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = -3;

        // 5. GENERATE ITEMS
        for (int i = 0; i < 20; i++)
        {
            var itemGO = new GameObject($"Slot_{i}");
            itemGO.transform.SetParent(contentGO.transform, false);
            
            // Icon Background
            var iconGO = new GameObject("Icon_Background");
            iconGO.transform.SetParent(itemGO.transform, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0, 0.2f); // top 80%
            iconRT.anchorMax = new Vector2(1, 1);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Text Label
            var textGO = new GameObject("Label");
            textGO.transform.SetParent(itemGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0, 0); // bottom 20%
            textRT.anchorMax = new Vector2(1, 0.2f);
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = buttonText;
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
        }

        Selection.activeGameObject = scrollViewGO;
        Debug.Log($"[BallsOfBabel] Added Item Grid for {sceneName}");
    }
}
#endif
