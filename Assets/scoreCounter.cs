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
        UpdateHUDText();
    }

    public void AddScore(int points)
    {
        score += points;
        UpdateHUDText();
    }

    public void UpdateStats(float speed, bool jumpReady, int health)
    {
        currentSpeed = speed;
        canJump = jumpReady;
        currentHealth = health;
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
