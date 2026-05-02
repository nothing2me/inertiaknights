using UnityEditor;
using UnityEngine;
using TMPro;

public static class AddControlsTextToScene
{
    [MenuItem("BallsOfBabel/Add Controls Text Box To Scene")]
    public static void AddControls()
    {
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error", "No Canvas found in the active scene! Please make sure a Canvas exists.", "OK");
            return;
        }

        // 1. SCROLL VIEW (Main wrapper)
        var scrollViewGO = new GameObject("ControlsScrollView");
        scrollViewGO.transform.SetParent(canvas.transform, false);
        var scrollViewRT = scrollViewGO.AddComponent<RectTransform>();
        scrollViewRT.anchorMin = new Vector2(0.28f, 0.15f); // Shifted right
        scrollViewRT.anchorMax = new Vector2(0.85f, 0.75f); // Shifted right
        scrollViewRT.offsetMin = Vector2.zero;
        scrollViewRT.offsetMax = Vector2.zero;
        
        var scrollRect = scrollViewGO.AddComponent<UnityEngine.UI.ScrollRect>();
        scrollRect.horizontal = false; // Vertical scroll only
        scrollRect.scrollSensitivity = 35f;

        // 2. VIEWPORT (Masks the content so it doesn't overlap)
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollViewGO.transform, false);
        var viewportRT = viewportGO.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;
        viewportGO.AddComponent<UnityEngine.UI.RectMask2D>(); // Clips overflowing children

        // 3. CONTENT (Scrollable area)
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0, 1000f); // Fixed height for scrolling
        contentRT.anchoredPosition = Vector2.zero;

        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;

        // 4. THE TABLE (Child of Content)
        var tableGO = new GameObject("ControlsTable");
        tableGO.transform.SetParent(contentGO.transform, false);
        var tableRT = tableGO.AddComponent<RectTransform>();
        tableRT.anchorMin = Vector2.zero;
        tableRT.anchorMax = Vector2.one;
        tableRT.offsetMin = Vector2.zero;
        tableRT.offsetMax = Vector2.zero;

        // Colors
        string spacing = "\n\n";

        // LEFT COLUMN (Actions)
        var leftGO = new GameObject("LeftColumn");
        leftGO.transform.SetParent(tableGO.transform, false);
        var leftRT = leftGO.AddComponent<RectTransform>();
        leftRT.anchorMin = new Vector2(0.0f, 0.0f);
        leftRT.anchorMax = new Vector2(0.45f, 1.0f);
        leftRT.offsetMin = leftRT.offsetMax = Vector2.zero;

        var leftTMP = leftGO.AddComponent<TextMeshProUGUI>();
        leftTMP.fontSize = 40;
        leftTMP.alignment = TextAlignmentOptions.TopLeft;
        leftTMP.color = Color.white;
        leftTMP.richText = true;
        leftTMP.text = 
            $"<b><color=#A0C0FF>Action / Command</color></b>{spacing}" +
            $"<b>Move</b>{spacing}" +
            $"<b>Look</b>{spacing}" +
            $"<b>Jump</b>{spacing}" +
            $"<b>Sprint</b>{spacing}" +
            $"<b>Attack</b>{spacing}" +
            $"<b>Interact</b>{spacing}" +
            $"<b>Crouch</b>{spacing}" +
            $"<b>Previous Item</b>{spacing}" +
            $"<b>Next Item</b>";

        // RIGHT COLUMN (Bindings)
        var rightGO = new GameObject("RightColumn");
        rightGO.transform.SetParent(tableGO.transform, false);
        var rightRT = rightGO.AddComponent<RectTransform>();
        rightRT.anchorMin = new Vector2(0.5f, 0.0f);
        rightRT.anchorMax = new Vector2(1.0f, 1.0f);
        rightRT.offsetMin = rightRT.offsetMax = Vector2.zero;

        var rightTMP = rightGO.AddComponent<TextMeshProUGUI>();
        rightTMP.fontSize = 40;
        rightTMP.alignment = TextAlignmentOptions.TopLeft;
        rightTMP.color = Color.white;
        rightTMP.richText = true;
        rightTMP.text = 
            $"<b><color=#A0C0FF>Key Binding(s)</color></b>{spacing}" +
            $"W, A, S, D / Arrow Keys{spacing}" +
            $"Mouse{spacing}" +
            $"Space{spacing}" +
            $"Left Shift{spacing}" +
            $"Left Mouse Button / Enter{spacing}" +
            $"E{spacing}" +
            $"C{spacing}" +
            $"1{spacing}" +
            $"2";

        // 5. VISUAL SCROLLBAR
        var scrollbarGO = new GameObject("Scrollbar");
        scrollbarGO.transform.SetParent(scrollViewGO.transform, false);
        var scrollbarRT = scrollbarGO.AddComponent<RectTransform>();
        scrollbarRT.anchorMin = new Vector2(1f, 0f);
        scrollbarRT.anchorMax = new Vector2(1f, 1f);
        scrollbarRT.pivot = new Vector2(1f, 0.5f);
        scrollbarRT.sizeDelta = new Vector2(20f, 0f);
        scrollbarRT.anchoredPosition = new Vector2(-10f, 0f);

        // Scrollbar Background
        var scrollbarBg = scrollbarGO.AddComponent<UnityEngine.UI.Image>();
        scrollbarBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        // Sliding Area
        var slidingAreaGO = new GameObject("SlidingArea");
        slidingAreaGO.transform.SetParent(scrollbarGO.transform, false);
        var slidingAreaRT = slidingAreaGO.AddComponent<RectTransform>();
        slidingAreaRT.anchorMin = Vector2.zero;
        slidingAreaRT.anchorMax = Vector2.one;
        slidingAreaRT.offsetMin = new Vector2(4, 4);
        slidingAreaRT.offsetMax = new Vector2(-4, -4);

        // Handle
        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(slidingAreaGO.transform, false);
        var handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.offsetMin = Vector2.zero;
        handleRT.offsetMax = Vector2.zero;

        var handleImg = handleGO.AddComponent<UnityEngine.UI.Image>();
        handleImg.color = new Color(0.5f, 0.5f, 0.5f, 1f); // Grey handle

        var scrollbar = scrollbarGO.AddComponent<UnityEngine.UI.Scrollbar>();
        scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImg;
        scrollbar.handleRect = handleRT;

        // Wire it to ScrollRect
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = -3;

        // Auto-select the scroll view wrapper
        Selection.activeGameObject = scrollViewGO;
        
        Debug.Log("[BallsOfBabel] Scrollable Controls Table successfully added to the Canvas!");
    }
}
