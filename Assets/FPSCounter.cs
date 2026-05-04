using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class FPSCounter : MonoBehaviour
{
    [Header("Settings")]
    public float updateInterval = 0.5f; // How often to update the text so it doesn't flicker too fast

    private TextMeshProUGUI fpsText;
    private float accum = 0f; // FPS accumulated over the interval
    private int frames = 0; // Frames drawn over the interval
    private float timeleft; // Time left for current interval

    void Start()
    {
        fpsText = GetComponent<TextMeshProUGUI>();
        timeleft = updateInterval;
    }

    void Update()
    {
        timeleft -= Time.unscaledDeltaTime; // Use unscaled so it still calculates accurately if game is paused
        accum += Time.unscaledDeltaTime;
        frames++;

        // Interval ended - update GUI text and start new interval
        if (timeleft <= 0.0)
        {
            // Display two fractional digits (f2 format)
            float currentFps = frames / accum;
            if (fpsText != null)
            {
                fpsText.text = $"FPS: {Mathf.RoundToInt(currentFps)}";
            }

            // Optional: You could change the text color based on FPS
            if (currentFps < 30)
                fpsText.color = Color.red;
            else if (currentFps < 60)
                fpsText.color = Color.yellow;
            else
                fpsText.color = Color.green;

            timeleft = updateInterval;
            accum = 0.0f;
            frames = 0;
        }
    }
}
