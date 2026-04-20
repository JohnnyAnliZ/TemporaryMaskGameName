using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] AudioSource musicSource1;
    [SerializeField] AudioSource musicSource2;
    [SerializeField] AudioSource sfxSource;
    [SerializeField] AudioSource ambienceSource;

    [Header("Music Clips")]
    public AudioClip track_2D;
    public AudioClip track_2D_Intro;
    public AudioClip track_Trans_To_3D;
    public AudioClip track_3D_Intro;
    public AudioClip track_3D;
    public AudioClip track_Trans_To_Real_Life;
    public AudioClip track_Real_Life;

    [Header("SFX Clips")]
    public AudioClip crumble;

    [Header("Ambience Clips")]
    public AudioClip ambience;

    [Header("Music Transition Settings")]
    [SerializeField] private int fadeOutMeasure = 9;
    [SerializeField] private int transitionMeasure = 9;

    private ExampleClass metronome;

    private void Start()
    {
        musicSource1.clip = track_2D_Intro;
        musicSource1.Play();

        musicSource2.clip = track_2D;

        // Subscribe to metronome events
        metronome = ExampleClass.Instance;
        if (metronome != null)
        {
            metronome.OnMeasureChanged += HandleMeasureChange;
            metronome.OnBeatChanged += HandleBeatChange;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (metronome != null)
        {
            metronome.OnMeasureChanged -= HandleMeasureChange;
            metronome.OnBeatChanged -= HandleBeatChange;
        }
    }

    private void HandleMeasureChange(int measure)
    {
        Debug.Log($"Measure changed to: {measure}");
        
        // Trigger music changes at specific measures
        if (measure == fadeOutMeasure)
        {
            FadeOutMusic(musicSource1);
        }
        
        if (measure == transitionMeasure)
        {
            PlayMusicWithFadeIn(musicSource2, track_2D);
        }
    }

    private void HandleBeatChange(int measure, int beat)
    {
        // Called every beat (use for more granular control if needed)
        // Example: trigger SFX on specific beats
    }

    public void PlayMusicAtMeasure(AudioSource source, AudioClip clip, int targetMeasure)
    {
        if (metronome == null)
            metronome = ExampleClass.Instance;
            
        int currentMeasure = metronome.GetCurrentMeasure();
        
        if (currentMeasure == targetMeasure)
        {
            source.clip = clip;
            source.Play();
            Debug.Log($"Playing {clip.name} at measure {targetMeasure}");
        }
    }

    public void StopMusicAtMeasure(AudioSource source, int targetMeasure)
    {
        if (metronome == null)
            metronome = ExampleClass.Instance;
            
        int currentMeasure = metronome.GetCurrentMeasure();
        
        if (currentMeasure == targetMeasure)
        {
            source.Stop();
            Debug.Log($"Stopping music at measure {targetMeasure}");
        }
    }

    public void FadeOutMusic(AudioSource source, float duration = 2f)
    {
        StartCoroutine(FadeOutCoroutine(source, duration));
    }

    public void PlayMusicWithFadeIn(AudioSource source, AudioClip clip, float duration = 1f)
    {
        source.clip = clip;
        source.volume = 0f;
        source.Play();
        StartCoroutine(FadeInCoroutine(source, duration));
    }

    private System.Collections.IEnumerator FadeOutCoroutine(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        source.volume = 0f;
        source.Stop();
    }

    private System.Collections.IEnumerator FadeInCoroutine(AudioSource source, float duration)
    {
        float targetVolume = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
            yield return null;
        }

        source.volume = targetVolume;
    }

    private void Update()
    {
        if (musicSource1.isPlaying && musicSource1.time >= 39)
        {
            musicSource1.volume -= 0.1f * Time.deltaTime;

            if (musicSource1.isPlaying && musicSource1.time == 39) {
                musicSource2.Play();
                musicSource2.volume = 0.0f;
            }
            musicSource2.volume += 0.1f * Time.deltaTime;
        }
    }
}
