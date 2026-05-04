using UnityEngine;
using TMPro;

public class ScoreCounter : MonoBehaviour
{
    public TextMeshProUGUI scoreText;
    private int score = 0;
    private float currentSpeed = 0f;
    private bool canJump = false;
    private int currentHealth = 3;
    private string localIpAddress = "";
    private string publicIpAddress = "";
    
    // New HUD Elements
    private GameObject frameObj;
    private GameObject bossObj;
    private GameObject scoreHudObj;
    private GameObject[] abilityIcons = new GameObject[3];
    
    // Feature: Boss Defeats & Health Bar
    public static ScoreCounter Instance;
    private GameObject healthBarObj;
    private UnityEngine.UI.RawImage healthBarImg;
    private RectTransform healthBarRt;
    private GameObject[] xIcons = new GameObject[3];
    private int defeatedBosses = 0;

    void Awake()
    {
        if (scoreText == null)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public void SetLocalIp(string ip)
    {
        localIpAddress = ip;
        UpdateHUDText();
    }

    public void SetPublicIp(string ip)
    {
        publicIpAddress = ip;
        UpdateHUDText();
    }

    // FPS Tracking
    private float accum = 0f;
    private int frames = 0;
    private float timeleft = 0.5f;
    private float currentFps = 0f;

    void Start()
    {
        if (scoreText != null)
        {
            Canvas canvas = scoreText.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler == null) scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                // Move directly under Canvas to escape any hidden/masked layout groups
                scoreText.transform.SetParent(canvas.transform, false);

                // --- Instantiate Frame ---
                Texture2D frameTex = Resources.Load<Texture2D>("game_hud");
                if (frameTex != null)
                {
                    frameObj = new GameObject("GameHUDFrame", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                    frameObj.transform.SetParent(canvas.transform, false);
                    RectTransform frameRt = frameObj.GetComponent<RectTransform>();
                    frameRt.anchorMin = Vector2.zero;
                    frameRt.anchorMax = Vector2.one;
                    frameRt.offsetMin = Vector2.zero;
                    frameRt.offsetMax = Vector2.zero;
                    
                    UnityEngine.UI.RawImage frameImg = frameObj.GetComponent<UnityEngine.UI.RawImage>();
                    frameImg.texture = frameTex;
                    frameImg.raycastTarget = false;
                    frameObj.transform.SetAsFirstSibling(); // Render behind other UI
                    frameObj.SetActive(false);
                }

                // --- Instantiate Boss Icons ---
                Texture2D bossIconsTex = Resources.Load<Texture2D>("boss_hud_icons");
                if (bossIconsTex != null)
                {
                    bossObj = new GameObject("BossHUDIcons", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                    bossObj.transform.SetParent(canvas.transform, false);
                    RectTransform bossRt = bossObj.GetComponent<RectTransform>();
                    bossRt.anchorMin = new Vector2(0.5f, 1f);
                    bossRt.anchorMax = new Vector2(0.5f, 1f);
                    bossRt.pivot = new Vector2(0.5f, 1f);
                    bossRt.anchoredPosition = new Vector2(0, -20);
                    
                    // Maintain native size ratio, assuming max height of ~150px
                    float scaleRatio = 150f / bossIconsTex.height;
                    bossRt.sizeDelta = new Vector2(bossIconsTex.width * scaleRatio, bossIconsTex.height * scaleRatio);
                    
                    UnityEngine.UI.RawImage bossImg = bossObj.GetComponent<UnityEngine.UI.RawImage>();
                    bossImg.texture = bossIconsTex;
                    bossImg.raycastTarget = false;
                    bossObj.SetActive(false);

                    // --- Instantiate X Icons inside BossHUDIcons ---
                    Texture2D xTex = Resources.Load<Texture2D>("x_button");
                    if (xTex != null)
                    {
                        float width = bossRt.sizeDelta.x;
                        float spacing = width / 3f;

                        for (int i = 0; i < 3; i++)
                        {
                            GameObject xObj = new GameObject($"BossX_{i}", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                            xObj.transform.SetParent(bossObj.transform, false);
                            RectTransform xRt = xObj.GetComponent<RectTransform>();
                            
                            xRt.anchorMin = new Vector2(0.5f, 0.5f);
                            xRt.anchorMax = new Vector2(0.5f, 0.5f);
                            xRt.pivot = new Vector2(0.5f, 0.5f);
                            xRt.anchoredPosition = new Vector2((i - 1) * spacing, 0); // Left, Center, Right
                            
                            float xScaleRatio = 60f / xTex.height;
                            xRt.sizeDelta = new Vector2(xTex.width * xScaleRatio, xTex.height * xScaleRatio);
                            
                            UnityEngine.UI.RawImage xImg = xObj.GetComponent<UnityEngine.UI.RawImage>();
                            xImg.texture = xTex;
                            xImg.color = new Color(1, 1, 1, 0); // Initially hidden
                            xImg.raycastTarget = false;
                            xIcons[i] = xObj;
                        }
                    }
                }

                // --- Instantiate Score HUD Frame ---
                Texture2D scoreHudTex = Resources.Load<Texture2D>("score_hud");
                if (scoreHudTex != null)
                {
                    scoreHudObj = new GameObject("ScoreHUDFrame", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                    scoreHudObj.transform.SetParent(canvas.transform, false);
                    RectTransform scoreRt = scoreHudObj.GetComponent<RectTransform>();
                    scoreRt.anchorMin = new Vector2(1f, 1f); // Top Right
                    scoreRt.anchorMax = new Vector2(1f, 1f);
                    scoreRt.pivot = new Vector2(1f, 1f);
                    scoreRt.anchoredPosition = new Vector2(-20, -20); // 20px padding from the corner
                    
                    float scaleRatio = 125f / scoreHudTex.height;
                    scoreRt.sizeDelta = new Vector2(scoreHudTex.width * scaleRatio, scoreHudTex.height * scaleRatio);
                    
                    UnityEngine.UI.RawImage scoreImg = scoreHudObj.GetComponent<UnityEngine.UI.RawImage>();
                    scoreImg.texture = scoreHudTex;
                    scoreImg.raycastTarget = false;
                    scoreHudObj.SetActive(false);
                }

                // --- Instantiate Health Bar ---
                Texture2D healthBarTex = Resources.Load<Texture2D>("health_bar");
                if (healthBarTex != null)
                {
                    // Create Container
                    healthBarObj = new GameObject("HealthBarContainer", typeof(RectTransform));
                    healthBarObj.transform.SetParent(canvas.transform, false);
                    RectTransform containerRt = healthBarObj.GetComponent<RectTransform>();
                    
                    // Position at bottom left, inside the wooden frame template
                    // Position at bottom left, explicitly sized to fit the hole
                    containerRt.anchorMin = new Vector2(0f, 0f);
                    containerRt.anchorMax = new Vector2(0f, 0f);
                    containerRt.pivot = new Vector2(0f, 0f);
                    containerRt.anchoredPosition = new Vector2(64, 34); // Shifted slightly up to balance the vertical gap
                    
                    // Explicitly set width/height to fit inside the wooden borders without stretching outside
                    // Reduced based on visual feedback
                    Vector2 barSize = new Vector2(550, 125); 
                    containerRt.sizeDelta = barSize;

                    // Create Background Silhouette
                    GameObject bgObj = new GameObject("HealthBar_BG", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                    bgObj.transform.SetParent(containerRt, false);
                    RectTransform bgRt = bgObj.GetComponent<RectTransform>();
                    bgRt.anchorMin = Vector2.zero;
                    bgRt.anchorMax = Vector2.zero;
                    bgRt.pivot = Vector2.zero;
                    bgRt.anchoredPosition = Vector2.zero;
                    bgRt.sizeDelta = barSize;
                    
                    UnityEngine.UI.RawImage bgImg = bgObj.GetComponent<UnityEngine.UI.RawImage>();
                    bgImg.texture = healthBarTex;
                    bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f); // Dark grey silhouette
                    bgImg.raycastTarget = false;

                    // Create Foreground Fill
                    GameObject fillObj = new GameObject("HealthBar_Fill", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                    fillObj.transform.SetParent(containerRt, false);
                    healthBarRt = fillObj.GetComponent<RectTransform>();
                    healthBarRt.anchorMin = Vector2.zero;
                    healthBarRt.anchorMax = Vector2.zero;
                    healthBarRt.pivot = Vector2.zero; // Pivot bottom-left so scaling shrinks to the left
                    healthBarRt.anchoredPosition = Vector2.zero;
                    healthBarRt.sizeDelta = barSize;
                    
                    healthBarImg = fillObj.GetComponent<UnityEngine.UI.RawImage>();
                    healthBarImg.texture = healthBarTex;
                    healthBarImg.raycastTarget = false;
                    
                    healthBarObj.SetActive(false);
                }

                // --- Instantiate Ability Icons ---
                string[] knightIcons = { "k_grapple", "k_dash", "k_passive" };
                for (int i = 0; i < 3; i++)
                {
                    Texture2D tex = Resources.Load<Texture2D>(knightIcons[i]);
                    if (tex != null)
                    {
                        GameObject iconObj = new GameObject($"AbilityIcon_{i}", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                        iconObj.transform.SetParent(canvas.transform, false);
                        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                        
                        // Anchor to Middle-Left
                        iconRt.anchorMin = new Vector2(0f, 0.5f);
                        iconRt.anchorMax = new Vector2(0f, 0.5f);
                        iconRt.pivot = new Vector2(0f, 0.5f);
                        
                        // Precise Y coordinates to match the HUD rings exactly
                        float[] yPositions = { 340f, 85f, -130f }; // Top, Middle, Bottom
                        iconRt.anchoredPosition = new Vector2(40f, yPositions[i]);
                        iconRt.sizeDelta = new Vector2(120f, 120f);
                        
                        UnityEngine.UI.RawImage img = iconObj.GetComponent<UnityEngine.UI.RawImage>();
                        img.texture = tex;
                        img.raycastTarget = false;
                        
                        abilityIcons[i] = iconObj;
                        iconObj.SetActive(false);
                    }
                }
            }

            // Reposition scoreText to Top-Right nicely inside the new score HUD frame
            RectTransform rt = scoreText.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-20, -20); // Match frame position
                rt.sizeDelta = new Vector2(412.5f, 125f); // Match frame size exactly
                rt.localScale = Vector3.one;          
                rt.localRotation = Quaternion.identity;
            }
            
            // CRITICAL: Disable Auto-Sizing so the text doesn't balloon up
            scoreText.enableAutoSizing = false;
            scoreText.fontSize = 50; // Increased size slightly since it's just a number
            scoreText.alignment = TextAlignmentOptions.Center; // Centered inside the frame
            scoreText.overflowMode = TextOverflowModes.Overflow; 
            scoreText.margin = Vector4.zero; 
            scoreText.raycastTarget = false; 
            
            // Format text: Yellow with Black Stroke
            scoreText.color = Color.yellow;
            var outline = scoreText.gameObject.GetComponent<UnityEngine.UI.Outline>();
            if (outline == null) outline = scoreText.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0, 0, 0, 1f);
            outline.effectDistance = new Vector2(2, -2);
            
            // Apply Custom Font
            Font customFont = Resources.Load<Font>("MedievalSharp-Bold");
            if (customFont != null)
            {
                TMPro.TMP_FontAsset tmpFont = TMPro.TMP_FontAsset.CreateFontAsset(customFont);
                scoreText.font = tmpFont;
            }
            
            // Strictly enforce rendering Z-index (First sibling renders in back, last in front)
            if (healthBarObj != null) healthBarObj.transform.SetAsFirstSibling();
            
            // Render ability icons right after healthbar (behind the main frame!)
            for (int i = 0; i < 3; i++)
            {
                if (abilityIcons[i] != null) abilityIcons[i].transform.SetAsLastSibling();
            }
            
            if (frameObj != null) frameObj.transform.SetAsLastSibling();
            if (bossObj != null) bossObj.transform.SetAsLastSibling();
            if (scoreHudObj != null) scoreHudObj.transform.SetAsLastSibling();
            scoreText.transform.SetAsLastSibling();
            
            // Hide initially until the game actually starts
            scoreText.gameObject.SetActive(false);
        }

        UpdateHUDText();
    }

    public void AddScore(int points)
    {
        score += points;
        UpdateHUDText();
    }

    public void UpdateStats(float speed, bool jumpReady, int health, int maxHealth = 3)
    {
        currentSpeed = speed;
        canJump = jumpReady;
        currentHealth = health;
        
        // Show HUD now that stats are being actively updated
        if (scoreText != null && !scoreText.gameObject.activeSelf)
        {
            scoreText.gameObject.SetActive(true);
            if (frameObj != null) frameObj.SetActive(true);
            if (bossObj != null) bossObj.SetActive(true);
            if (healthBarObj != null) healthBarObj.SetActive(true);
            if (scoreHudObj != null) scoreHudObj.SetActive(true);
            
            for (int i = 0; i < 3; i++)
            {
                if (abilityIcons[i] != null) abilityIcons[i].SetActive(true);
            }
        }

        if (healthBarRt != null && maxHealth > 0)
        {
            float fill = Mathf.Clamp01((float)health / maxHealth);
            healthBarRt.localScale = new Vector3(fill, 1f, 1f);
        }

        UpdateHUDText();
    }

    public void MarkBossDefeated()
    {
        if (defeatedBosses < 3 && xIcons[defeatedBosses] != null)
        {
            StartCoroutine(FadeInX(xIcons[defeatedBosses].GetComponent<UnityEngine.UI.RawImage>()));
            defeatedBosses++;
        }
    }

    private System.Collections.IEnumerator FadeInX(UnityEngine.UI.RawImage img)
    {
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f; // 0.5s fade
            img.color = new Color(1, 1, 1, Mathf.Clamp01(t));
            yield return null;
        }
    }

    void Update()
    {
        timeleft -= Time.unscaledDeltaTime;
        accum += Time.unscaledDeltaTime;
        frames++;

        if (timeleft <= 0.0)
        {
            currentFps = frames / accum;
            timeleft = 0.5f;
            accum = 0.0f;
            frames = 0;
            
            UpdateHUDText();
        }
    }

    private void UpdateHUDText()
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString();
        }
    }
}
