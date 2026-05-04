using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Persistent singleton that manages background music across scenes.
/// Loads audio clips from Resources/Music/ by name.
/// Supports looping playback, crossfading between tracks, and volume control.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Volume")]
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    [Header("Crossfade")]
    [Tooltip("Duration of the crossfade between tracks in seconds.")]
    public float crossfadeDuration = 1.5f;

    // Track names (loaded from Resources/Music/)
    private const string MENU_TRACK  = "Music/MenuMusic";
    private const string GAME_TRACK  = "Music/GameMusic";

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;
    private AudioSource sfxSource;
    private Coroutine fadeCoroutine;

    // Cache the hover sound so we don't load it every time we hover
    private AudioClip hoverClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Create two AudioSources for crossfading
        sourceA = gameObject.AddComponent<AudioSource>();
        sourceB = gameObject.AddComponent<AudioSource>();

        // Create a separate AudioSource for one-shot SFX
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f; // 2D UI sounds

        ConfigureSource(sourceA);
        ConfigureSource(sourceB);

        activeSource = sourceA;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Play music for whatever scene we're starting in
        PlayMusicForScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusicForScene(scene.name);
    }

    private void PlayMusicForScene(string sceneName)
    {
        switch (sceneName)
        {
            case "MainMenu":
                PlayTrack(MENU_TRACK);
                break;

            case "MainScene":
                PlayTrack(GAME_TRACK);
                break;

            // Other scenes (Store, Character, Settings) keep the current track
        }
    }

    /// <summary>
    /// Crossfade to a new track by resource path. If the track is already playing, does nothing.
    /// </summary>
    public void PlayTrack(string resourcePath)
    {
        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"[MusicManager] Could not load audio clip at 'Resources/{resourcePath}'. Skipping.");
            return;
        }

        // Don't restart if this track is already playing
        if (activeSource.clip == clip && activeSource.isPlaying)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(CrossfadeTo(clip));
    }

    /// <summary>
    /// Stop all music with a fade out.
    /// </summary>
    public void StopMusic()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeOut(activeSource, crossfadeDuration));
    }

    /// <summary>
    /// Play the UI hover click sound.
    /// </summary>
    public void PlayUIHover()
    {
        if (hoverClip == null)
        {
            hoverClip = Resources.Load<AudioClip>("SFX/ButtonHover");
        }
        
        if (hoverClip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(hoverClip, 0.4f); // Slightly quieter so it isn't obnoxious
        }
    }

    /// <summary>
    /// Set the volume (0–1) and apply it to the active source.
    /// </summary>
    public void SetVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (activeSource != null && activeSource.isPlaying)
            activeSource.volume = musicVolume;
    }

    private IEnumerator CrossfadeTo(AudioClip newClip)
    {
        AudioSource oldSource = activeSource;
        AudioSource newSource = (activeSource == sourceA) ? sourceB : sourceA;
        activeSource = newSource;

        // Start the new track at zero volume
        newSource.clip = newClip;
        newSource.volume = 0f;
        newSource.Play();

        float elapsed = 0f;

        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / crossfadeDuration;

            newSource.volume = Mathf.Lerp(0f, musicVolume, t);
            oldSource.volume = Mathf.Lerp(musicVolume, 0f, t);

            yield return null;
        }

        newSource.volume = musicVolume;
        oldSource.volume = 0f;
        oldSource.Stop();
        oldSource.clip = null;

        fadeCoroutine = null;
    }

    private IEnumerator FadeOut(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        source.volume = 0f;
        source.Stop();
        source.clip = null;

        fadeCoroutine = null;
    }

    private void ConfigureSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0f;
        source.spatialBlend = 0f; // 2D audio (not positional)
        source.priority = 0;     // Highest priority — music should never be culled
    }
}
