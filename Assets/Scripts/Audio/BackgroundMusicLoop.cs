using System.Collections;
using UnityEngine;

/// <summary>
/// Plays one music clip on scene start, fades it out near the end, then restarts it.
/// </summary>
[DisallowMultipleComponent]
public sealed class BackgroundMusicLoop : MonoBehaviour
{
    [SerializeField] private AudioClip musicClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.75f;
    [SerializeField, Min(0.05f)] private float fadeOutDuration = 3f;
    [SerializeField] private bool playOnStart = true;

    private AudioSource audioSource;
    private Coroutine loopRoutine;
    private bool playbackPaused;
    private bool resumeAfterPause;
    private int pausedTimeSamples;
    private float pausedVolume;

    public void Configure(AudioClip clip, float targetVolume, float fadeSeconds, bool shouldPlayOnStart = true)
    {
        musicClip = clip;
        volume = Mathf.Clamp01(targetVolume);
        fadeOutDuration = Mathf.Max(0.05f, fadeSeconds);
        playOnStart = shouldPlayOnStart;
        CacheAudioSource(createIfMissing: true);
        ConfigureAudioSource();
    }

    private void Awake()
    {
        CacheAudioSource(createIfMissing: true);
        ConfigureAudioSource();
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        CacheAudioSource(createIfMissing: true);
        ConfigureAudioSource();

        if (musicClip == null)
        {
            return;
        }

        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
        }

        loopRoutine = StartCoroutine(PlayLoop());
    }

    public void Stop()
    {
        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        playbackPaused = false;
        resumeAfterPause = false;
        pausedTimeSamples = 0;
        pausedVolume = volume;

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.volume = volume;
        }
    }

    public void Pause()
    {
        CacheAudioSource();
        if (audioSource == null || musicClip == null || playbackPaused)
        {
            return;
        }

        resumeAfterPause = audioSource.isPlaying;
        if (!resumeAfterPause)
        {
            return;
        }

        pausedTimeSamples = Mathf.Clamp(audioSource.timeSamples, 0, Mathf.Max(0, musicClip.samples - 1));
        pausedVolume = audioSource.volume;
        playbackPaused = true;
        audioSource.Pause();
    }

    public void Resume()
    {
        CacheAudioSource();
        if (!playbackPaused)
        {
            return;
        }

        playbackPaused = false;
        if (!resumeAfterPause || audioSource == null || musicClip == null)
        {
            resumeAfterPause = false;
            return;
        }

        ConfigureAudioSource(resetVolume: false);
        audioSource.clip = musicClip;
        audioSource.timeSamples = Mathf.Clamp(pausedTimeSamples, 0, Mathf.Max(0, musicClip.samples - 1));
        audioSource.volume = pausedVolume;
        audioSource.UnPause();
        resumeAfterPause = false;
    }

    private IEnumerator PlayLoop()
    {
        while (musicClip != null)
        {
            ConfigureAudioSource();
            audioSource.clip = musicClip;
            audioSource.volume = volume;
            audioSource.time = 0f;
            audioSource.Play();

            float clipLength = Mathf.Max(0.05f, musicClip.length);
            float fadeStartTime = Mathf.Max(0f, clipLength - fadeOutDuration);
            float fadeLength = Mathf.Max(0.05f, clipLength - fadeStartTime);

            while (audioSource.isPlaying || playbackPaused)
            {
                if (playbackPaused)
                {
                    yield return null;
                    continue;
                }

                if (audioSource.time >= fadeStartTime)
                {
                    float fadeProgress = Mathf.Clamp01((audioSource.time - fadeStartTime) / fadeLength);
                    float easedProgress = fadeProgress * fadeProgress * (3f - 2f * fadeProgress);
                    audioSource.volume = Mathf.Lerp(volume, 0f, easedProgress);
                }

                yield return null;
            }

            audioSource.Stop();
            audioSource.volume = volume;
            yield return null;
        }

        loopRoutine = null;
    }

    private void CacheAudioSource(bool createIfMissing = false)
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null && createIfMissing)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void ConfigureAudioSource(bool resetVolume = true)
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        if (resetVolume)
        {
            audioSource.volume = volume;
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Pause();
        }
        else
        {
            Resume();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    private void OnValidate()
    {
        volume = Mathf.Clamp01(volume);
        fadeOutDuration = Mathf.Max(0.05f, fadeOutDuration);
        CacheAudioSource();
        ConfigureAudioSource();
    }
}

public static class BackgroundMusicBootstrap
{
    private const string EditorMusicClipPath = "Assets/music/first_castle.mp3";
    private const string ResourcesMusicClipPath = "Music/first_castle";
    private const float DefaultVolume = 0.75f;
    private const float DefaultFadeOutDuration = 3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureBackgroundMusic()
    {
        if (Object.FindFirstObjectByType<BackgroundMusicLoop>() != null)
        {
            return;
        }

        AudioClip musicClip = LoadMusicClip();
        if (musicClip == null)
        {
            Debug.LogWarning(
                $"Background music clip was not found. Expected {EditorMusicClipPath} in the editor.");
            return;
        }

        GameObject musicObject = new GameObject("Background Music");
        BackgroundMusicLoop musicLoop = musicObject.AddComponent<BackgroundMusicLoop>();
        musicLoop.Configure(musicClip, DefaultVolume, DefaultFadeOutDuration, shouldPlayOnStart: false);
        musicLoop.Play();
    }

    private static AudioClip LoadMusicClip()
    {
#if UNITY_EDITOR
        AudioClip editorClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(EditorMusicClipPath);
        if (editorClip != null)
        {
            return editorClip;
        }
#endif

        return Resources.Load<AudioClip>(ResourcesMusicClipPath);
    }
}
