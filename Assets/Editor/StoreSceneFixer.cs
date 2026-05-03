#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public static class StoreSceneFixer
{
    private const string BannerPath = "Assets/Artsystack - Fantasy RPG GUI/Resources/Sprites/components/placeholder.png";
    private const string FramePath  = "Assets/Artsystack - Fantasy RPG GUI/Resources/Sprites/components/placeholder_slot.png";

    [MenuItem("BallsOfBabel/Fix Store Scene Sprites", priority = 20)]
    public static void FixStoreSprites()
    {
        GameObject root = GameObject.Find("Store_Item_Placeholders");
        if (root == null) return;

        Sprite bannerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BannerPath);
        Sprite frameSprite  = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);

        foreach (Transform frame in root.transform)
        {
            Transform placeholder = frame.Cast<Transform>().FirstOrDefault(t => t.name.ToLower().Contains("placeholder"));
            if (placeholder == null) continue;

            Transform icon = FindRecursive(frame, "store_item_") ?? CreateNewIcon(placeholder);
            GameObject iconGO = icon.gameObject;
            Undo.RecordObject(iconGO, "Title Fix");
            Undo.RecordObject(placeholder.gameObject, "Title Fix");

            // 1. Frame Setup
            var pImg = placeholder.GetComponent<Image>();
            if (pImg != null)
            {
                if (frameSprite != null) pImg.sprite = frameSprite;
                pImg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            }

            // 2. Icon Setup
            var img = iconGO.GetComponent<Image>() ?? iconGO.AddComponent<Image>();
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (icon.parent != placeholder) icon.SetParent(placeholder, false);
            
            RectTransform rt = icon.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(10, 30); rt.offsetMax = new Vector2(-10, -10);

            // 3. TITLE (Above the box)
            Transform existingTitle = frame.Find("Item_Title");
            GameObject titleGO = existingTitle != null ? existingTitle.gameObject : new GameObject("Item_Title");
            titleGO.transform.SetParent(frame, false);
            titleGO.transform.SetAsLastSibling();

            var titleTMP = titleGO.GetComponent<TextMeshProUGUI>() ?? titleGO.AddComponent<TextMeshProUGUI>();
            string cleanName = iconGO.name.Replace("store_item_", "").Replace("_", " ").ToUpper();
            titleTMP.text = string.IsNullOrEmpty(cleanName) || cleanName.Contains("PLACEHOLDER") ? "NEW ITEM" : cleanName;
            titleTMP.fontSize = 14;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.color = new Color(1f, 0.8f, 0.2f); // Gold
            titleTMP.alignment = TextAlignmentOptions.Center;

            RectTransform titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1.05f); // Positioned above the box
            titleRT.anchorMax = new Vector2(1, 1.25f);
            titleRT.offsetMin = titleRT.offsetMax = Vector2.zero;

            // 4. Status Banner (Bottom)
            Transform labelRoot = placeholder.Find("Label_Container");
            GameObject labelGO = labelRoot != null ? labelRoot.gameObject : new GameObject("Label_Container");
            labelGO.transform.SetParent(placeholder, false);
            labelGO.transform.SetAsLastSibling();

            var labelBg = labelGO.GetComponent<Image>() ?? labelGO.AddComponent<Image>();
            if (bannerSprite != null) labelBg.sprite = bannerSprite;
            labelBg.color = Color.white;
            labelBg.preserveAspect = true;

            RectTransform lRT = labelGO.GetComponent<RectTransform>();
            lRT.anchorMin = new Vector2(0.05f, -0.1f); lRT.anchorMax = new Vector2(0.95f, 0.2f);
            lRT.offsetMin = lRT.offsetMax = Vector2.zero;

            Transform textObj = labelGO.transform.Find("Text");
            GameObject txtGO = textObj != null ? textObj.gameObject : new GameObject("Text");
            txtGO.transform.SetParent(labelGO.transform, false);
            var tmp = txtGO.GetComponent<TextMeshProUGUI>() ?? txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "ACCESSIBLE IN GAME";
            tmp.fontSize = 10;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.93f, 0.81f, 0.62f);
            tmp.alignment = TextAlignmentOptions.Center;

            RectTransform tRT = txtGO.GetComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
            tRT.offsetMin = tRT.offsetMax = Vector2.zero;

            if (iconGO.GetComponent<UIHoverEffect>() == null) iconGO.AddComponent<UIHoverEffect>();
        }
        Debug.Log("[StoreFixer] Item titles added above slots.");
    }

    private static Transform CreateNewIcon(Transform parent)
    {
        GameObject go = new GameObject("store_item_placeholder");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Transform FindRecursive(Transform parent, string nameContains)
    {
        foreach (Transform child in parent)
        {
            if (child.name.ToLower().Contains(nameContains.ToLower())) return child;
            Transform found = FindRecursive(child, nameContains);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
