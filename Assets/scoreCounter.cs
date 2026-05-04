using UnityEngine;
using TMPro;

public class ScoreCounter : MonoBehaviour
{
    public static ScoreCounter Instance { get; private set; }

    public TextMeshProUGUI scoreText;
    private int score = 0;
    private float currentSpeed = 0f;
    private bool canJump = false;
    private int currentHealth = 3;
    private string localIpAddress = "";
    private string publicIpAddress = "";

    public int GetScore() => score;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
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
            }

            // Reposition scoreText to Top-Left nicely
            RectTransform rt = scoreText.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                // Move it even further down so FPS isn't cut off at the top
                rt.anchoredPosition = new Vector2(250, -150);
                rt.sizeDelta = new Vector2(800, 600); 
                rt.localScale = Vector3.one;          
                rt.localRotation = Quaternion.identity;
            }
            
            // CRITICAL: Disable Auto-Sizing so the text doesn't balloon up to fill the 800x600 box
            scoreText.enableAutoSizing = false;
            scoreText.fontSize = 24;
            scoreText.alignment = TextAlignmentOptions.TopLeft;
            scoreText.overflowMode = TextOverflowModes.Overflow; 
            scoreText.margin = Vector4.zero; 
            scoreText.raycastTarget = false; 
            
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

    public void DeductScore(int points)
    {
        score -= points;
        UpdateHUDText();
    }

    public void UpdateStats(float speed, bool jumpReady, int health)
    {
        currentSpeed = speed;
        canJump = jumpReady;
        currentHealth = health;
        
        // Show HUD now that stats are being actively updated
        if (scoreText != null && !scoreText.gameObject.activeSelf)
        {
            scoreText.gameObject.SetActive(true);
        }

        UpdateHUDText();
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
            string jumpColor = canJump ? "green" : "red";
            string jumpStatus = canJump ? "READY" : "WAIT";
            
            string hearts = "";
            for (int i = 0; i < currentHealth; i++) hearts += "♥";
            if (currentHealth <= 0) hearts = "DEAD";
            
            // Format FPS Color
            string fpsColor = "green";
            if (currentFps < 30) fpsColor = "red";
            else if (currentFps < 60) fpsColor = "yellow";

            scoreText.text = $"FPS: <color={fpsColor}>{Mathf.RoundToInt(currentFps)}</color>\n" +
                             $"Score: {score}\n" +
                             $"Health: <color=red>{hearts}</color>\n" +
                             $"Speed: {currentSpeed:F2}\n" +
                             $"Jump: <color={jumpColor}>{jumpStatus}</color>" +
                             (string.IsNullOrEmpty(localIpAddress) ? "" : $"\nLocal IP: <color=yellow>{localIpAddress}</color>") +
                             (string.IsNullOrEmpty(publicIpAddress) ? "" : $"\nPublic IP: <color=cyan>{publicIpAddress}</color>");
        }
    }
}
