#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class ShopAutomationTool : Editor
{
    private const string StoreScenePath = "Assets/MainMenu_Scenes/StoreScene.unity";
    private const string NPCStoreScenePath = "Assets/MainMenu_Scenes/StoreScene_NPC.unity";
    private const string InteractionDataDir = "Assets/Interaction/Data";
    private const string ShopDataName = "ShopInteractionData.asset";

    [MenuItem("BallsOfBabel/Shop/Full Automation: Setup NPC and Store", priority = 0)]
    public static void FullAutomation()
    {
        EnsureInteractPromptHUDExists();
        SetupSelectedAsShopNPC();
        CreateDedicatedNPCStoreScene();
        SetupStoreSceneOverlay(NPCStoreScenePath);
        AddStoreSceneToBuildSettings(NPCStoreScenePath);
        Debug.Log("[ShopAutomation] Full automation complete!");
    }

    [MenuItem("BallsOfBabel/Shop/Special: Create Dedicated NPC Store Scene", priority = 20)]
    public static void CreateDedicatedNPCStoreScene()
    {
        if (!File.Exists(StoreScenePath))
        {
            Debug.LogError($"[ShopAutomation] Base StoreScene not found at {StoreScenePath}");
            return;
        }

        if (!File.Exists(NPCStoreScenePath))
        {
            AssetDatabase.CopyAsset(StoreScenePath, NPCStoreScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[ShopAutomation] Created dedicated NPC store scene at {NPCStoreScenePath}");
        }

        // Update InteractionData to point to this scene
        string assetPath = Path.Combine(InteractionDataDir, ShopDataName);
        InteractionData data = AssetDatabase.LoadAssetAtPath<InteractionData>(assetPath);
        if (data != null)
        {
            Undo.RecordObject(data, "Update Store Scene Name");
            data.storeSceneName = "StoreScene_NPC";
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
    }

    private static void EnsureInteractPromptHUDExists()
    {
        // Find even inactive ones
        var existing = Resources.FindObjectsOfTypeAll<InteractPromptHUD>().FirstOrDefault();
        if (existing != null)
        {
            if (!existing.gameObject.activeSelf)
            {
                Undo.RecordObject(existing.gameObject, "Activate InteractPromptHUD");
                existing.gameObject.SetActive(true);
            }
            return;
        }

        Debug.Log("[ShopAutomation] InteractPromptHUD not found. Creating a basic one...");

        // 1. Create Canvas
        GameObject canvasGO = new GameObject("InteractionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        // 2. Create Prompt Panel
        GameObject panelGO = new GameObject("InteractPromptPanel", typeof(RectTransform));
        panelGO.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.2f);
        panelRT.anchorMax = new Vector2(0.5f, 0.2f);
        panelRT.sizeDelta = new Vector2(400, 50);

        // 3. Create Label
        GameObject labelGO = new GameObject("PromptLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(panelGO.transform, false);
        TextMeshProUGUI label = labelGO.GetComponent<TextMeshProUGUI>();
        label.text = "Press E to Interact";
        label.fontSize = 24;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.sizeDelta = Vector2.zero;

        // 4. Add InteractPromptHUD script
        InteractPromptHUD hud = canvasGO.AddComponent<InteractPromptHUD>();
        
        // Use SerializedObject to assign private fields
        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("panel").objectReferenceValue = panelGO;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedProperties();

        Debug.Log("[ShopAutomation] Created basic InteractPromptHUD.");
    }

    [MenuItem("BallsOfBabel/Shop/Step 1: Setup Shop NPC", priority = 10)]
    public static void SetupSelectedAsShopNPC()
    {
        EnsureInteractPromptHUDExists();
        GameObject selected = Selection.activeGameObject;
        
        // If nothing is selected, try to find the "Shop_NPC" object specifically
        if (selected == null)
        {
            selected = GameObject.Find("Shop_NPC");
        }

        if (selected == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a GameObject or ensure 'Shop_NPC' exists in the scene.", "OK");
            return;
        }

        // Ensure the name is consistent if we're dedicating this to Shop_NPC
        if (selected.name != "Shop_NPC")
        {
            if (EditorUtility.DisplayDialog("Confirm NPC", $"Do you want to configure '{selected.name}' as the Shop NPC?", "Yes", "No"))
            {
                // Proceed with selected
            }
            else return;
        }

        // 1. Ensure InteractableController
        var interactable = selected.GetComponent<InteractableController>();
        if (interactable == null)
        {
            interactable = selected.AddComponent<InteractableController>();
        }

        // 2. Ensure InteractionData asset
        if (!Directory.Exists(InteractionDataDir))
        {
            Directory.CreateDirectory(InteractionDataDir);
            AssetDatabase.Refresh();
        }

        string assetPath = Path.Combine(InteractionDataDir, ShopDataName);
        InteractionData data = AssetDatabase.LoadAssetAtPath<InteractionData>(assetPath);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<InteractionData>();
            AssetDatabase.CreateAsset(data, assetPath);
            AssetDatabase.SaveAssets();
        }

        // 3. Configure InteractionData
        Undo.RecordObject(data, "Configure Shop Interaction Data");
        data.type = InteractionType.Shop;
        data.mode = InteractionMode.PressToInteract;
        data.promptText = "Press E to browse the shop";
        data.interactRadius = 3f;
        EditorUtility.SetDirty(data);

        // 4. Assign Data to Controller
        Undo.RecordObject(interactable, "Assign Shop Data");
        interactable.data = data;
        EditorUtility.SetDirty(interactable);

        Debug.Log($"[ShopAutomation] Successfully configured {selected.name} as a Shop NPC.");
    }

    [MenuItem("BallsOfBabel/Shop/Step 2: Configure StoreScene Overlay", priority = 11)]
    public static void SetupStoreSceneOverlayMenu()
    {
        SetupStoreSceneOverlay(StoreScenePath);
    }

    public static void SetupStoreSceneOverlay(string scenePath)
    {
        // 1. Open Scene
        if (SceneManagerHelper.GetCurrentScenePath() != scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(scenePath);
        }

        // 2. Find or Create ShopOverlayManager
        GameObject managerGO = GameObject.Find("ShopOverlayManager");
        if (managerGO == null)
        {
            managerGO = new GameObject("ShopOverlayManager");
            Undo.RegisterCreatedObjectUndo(managerGO, "Create ShopOverlayManager");
        }

        var manager = managerGO.GetComponent<StoreOverlayManager>();
        if (manager == null)
        {
            manager = managerGO.AddComponent<StoreOverlayManager>();
        }

        // 3. Find UI elements to auto-assign (optional but helpful)
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            // Look for a Background image
            Image bg = canvas.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(img => img.gameObject.name.ToLower().Contains("background") || img.gameObject.name.ToLower().Contains("panel"));
            
            // Look for a Close button
            Button closeBtn = canvas.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(btn => btn.gameObject.name.ToLower().Contains("close") || btn.gameObject.name.ToLower().Contains("back"));

            if (bg != null || closeBtn != null)
            {
                Undo.RecordObject(manager, "Auto-assign UI elements");
                SerializedObject so = new SerializedObject(manager);
                if (bg != null) so.FindProperty("backgroundPanel").objectReferenceValue = bg;
                if (closeBtn != null) so.FindProperty("closeButton").objectReferenceValue = closeBtn;
                so.ApplyModifiedProperties();
            }
        }

        Debug.Log($"[ShopAutomation] Scene at {scenePath} configured.");
    }

    [MenuItem("BallsOfBabel/Shop/Step 3: Add StoreScene to Build Settings", priority = 12)]
    public static void AddStoreSceneToBuildSettingsMenu()
    {
        AddStoreSceneToBuildSettings(StoreScenePath);
    }

    public static void AddStoreSceneToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath))
        {
            Debug.Log($"[ShopAutomation] Scene {scenePath} is already in build settings.");
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[ShopAutomation] Added {scenePath} to Build Settings.");
    }
}

public static class SceneManagerHelper
{
    public static string GetCurrentScenePath()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
    }
}
#endif
