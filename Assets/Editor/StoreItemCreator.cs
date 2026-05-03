#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class StoreItemCreator
{
    private const string BannerPath = "Assets/Artsystack - Fantasy RPG GUI/Resources/Sprites/components/placeholder.png";
    private const string FramePath  = "Assets/Artsystack - Fantasy RPG GUI/Resources/Sprites/components/placeholder_slot.png";

    [MenuItem("BallsOfBabel/Create New Store Item", priority = 30)]
    public static void CreateItem()
    {
        GameObject parent = GameObject.Find("Store_Item_Placeholders") ?? GameObject.Find("Canvas_StoreScene") ?? Selection.activeGameObject;
        Sprite bannerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BannerPath);
        Sprite frameSprite  = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);

        GameObject frame = new GameObject("Store_Item_Frame_New");
        frame.transform.SetParent(parent?.transform, false);
        var frameRT = frame.AddComponent<RectTransform>();
        frameRT.sizeDelta = new Vector2(132, 132);

        // 1. Title (Above)
        GameObject titleGO = new GameObject("Item_Title");
        titleGO.transform.SetParent(frame.transform, false);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "NEW ITEM";
        titleTMP.fontSize = 14;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(1f, 0.8f, 0.2f);
        titleTMP.alignment = TextAlignmentOptions.Center;
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 1.05f);
        titleRT.anchorMax = new Vector2(1, 1.25f);
        titleRT.offsetMin = titleRT.offsetMax = Vector2.zero;

        // 2. Background
        GameObject bg = new GameObject("Item_Background");
        bg.transform.SetParent(frame.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        if (frameSprite != null) bgImg.sprite = frameSprite;
        bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        // 3. Icon
        GameObject icon = new GameObject("store_item_icon");
        icon.transform.SetParent(bg.transform, false);
        var iRT = icon.AddComponent<RectTransform>();
        iRT.anchorMin = Vector2.zero; iRT.anchorMax = Vector2.one;
        iRT.offsetMin = new Vector2(10, 30); iRT.offsetMax = new Vector2(-10, -10);
        var iImg = icon.AddComponent<Image>();
        iImg.color = Color.white;
        iImg.preserveAspect = true;
        iImg.raycastTarget = false;

        // 4. Status Banner
        GameObject label = new GameObject("Label_Container");
        label.transform.SetParent(bg.transform, false);
        var lBg = label.AddComponent<Image>();
        if (bannerSprite != null) lBg.sprite = bannerSprite;
        lBg.preserveAspect = true;
        RectTransform lRT = label.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0.05f, -0.1f); lRT.anchorMax = new Vector2(0.95f, 0.2f);
        lRT.offsetMin = lRT.offsetMax = Vector2.zero;

        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(label.transform, false);
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "ACCESSIBLE IN GAME";
        tmp.fontSize = 10;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.93f, 0.81f, 0.62f);
        tmp.alignment = TextAlignmentOptions.Center;
        RectTransform tRT = txtGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        icon.AddComponent<UIHoverEffect>();
        Selection.activeGameObject = frame;
        Undo.RegisterCreatedObjectUndo(frame, "Create Store Item");
    }
}
#endif
