using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class CharacterSelectionTool : Editor
{
    private const string CharacterScenePath = "Assets/MainMenu_Scenes/CharacterScene.unity";
    private const string ResourcesPath = "Assets/Resources";

    [MenuItem("BallsOfBabel/Character/Auto-Setup Selection Screen")]
    public static void SetupCharacterScreen()
    {
        // 1. Open Scene
        if (SceneManagerHelper.GetCurrentScenePath() != CharacterScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(CharacterScenePath);
        }

        // 2. Find Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas_Character", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // 3. Create Info Panel (The tooltip)
        GameObject infoPanel = GameObject.Find("CharacterInfoPanel");
        if (infoPanel == null)
        {
            infoPanel = new GameObject("CharacterInfoPanel", typeof(RectTransform), typeof(Image));
            infoPanel.transform.SetParent(canvas.transform, false);
            
            RectTransform rt = infoPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.1f);
            rt.anchorMax = new Vector2(0.5f, 0.1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(600, 150);
            
            infoPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f);

            GameObject textGO = new GameObject("DescriptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(infoPanel.transform, false);
            TextMeshProUGUI text = textGO.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 24;
            text.text = "Hover over a character to see details.";
            
            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
        }

        // 4. Create Card Container
        GameObject containerGO = GameObject.Find("ClassCardContainer");
        if (containerGO == null)
        {
            containerGO = new GameObject("ClassCardContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            containerGO.transform.SetParent(canvas.transform, false);
            
            RectTransform rt = containerGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1200, 500);
            
            var hlg = containerGO.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 50;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlHeight = true;
            hlg.childControlWidth = true;
        }

        // 5. Create Cards in Order: Knight, Healer, Heavy
        string[] cardNames = { "knight_class_card", "healer_class_card", "heavy_class_card" };
        
        foreach (string cardName in cardNames)
        {
            string cardGOKey = "Card_" + cardName;
            GameObject cardGO = GameObject.Find(cardGOKey);
            if (cardGO == null)
            {
                cardGO = new GameObject(cardGOKey, typeof(RectTransform), typeof(Image), typeof(CharacterHoverInfo));
                cardGO.transform.SetParent(containerGO.transform, false);
                
                // Set Sprite
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(ResourcesPath, cardName + ".png"));
                if (sprite != null)
                {
                    cardGO.GetComponent<Image>().sprite = sprite;
                }
                else
                {
                    Debug.LogWarning($"[CharacterTool] Could not find sprite at Assets/Resources/{cardName}.png");
                }

                // Configure Hover Script
                var hover = cardGO.GetComponent<CharacterHoverInfo>();
                hover.infoPanel = infoPanel;
                hover.infoText = infoPanel.GetComponentInChildren<TextMeshProUGUI>();
                hover.description = $"This is the {cardName.Split('_')[0]} class. Edit this text in the Inspector!";
            }
        }

        Debug.Log("[CharacterTool] Character selection screen setup complete!");
    }
}
