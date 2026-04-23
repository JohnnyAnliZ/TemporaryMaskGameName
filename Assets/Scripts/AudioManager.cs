using UnityEngine;

public class AudioManager : Singleton<AudioManager>
{

    [Header("Music Clips")]
    public AudioClip track2DIntro;
    public AudioClip track2D;
    public AudioClip trackTransTo3D;
    public AudioClip track3DIntro;
    public AudioClip track3D;
    public AudioClip trackTransToRealLife;
    public AudioClip trackRealLife;

    [Header("SFX Clips")]
    public AudioClip crumble;

    [Header("Ambience Clips")]
    public AudioClip ambience;

    [Header("Footstep Settings")]
    public AudioClip[] footstepClips;
    public float footstepVolume = 1.0f;
    public float footstepInterval = 0.5f;

    private AudioSource footstepSource;
    private AudioSource ambienceSource;
    private AudioSource track2DIntroSource;
    private AudioSource track2DSource;
    private AudioSource trackTransTo3DSource;
    private AudioSource track3DIntroSource;
    private AudioSource track3DSource;
    private AudioSource trackTransToRealLifeSource;
    private AudioSource trackRealLifeSource;

    private double startTime = 100000;
    private bool hasTransitioned = false;

    private void Start()
    {
        ambienceSource = CreateChildAudioSource("ambienceSource", 1, ambience, true);

        footstepSource = CreateChildAudioSource("footstepSource", footstepVolume, footstepClips[0], false);

        track2DIntroSource = CreateChildAudioSource("track2DIntroSource", 1, track2DIntro, true);
        track2DSource = CreateChildAudioSource("track2DSource", 0, track2D, true);
        trackTransTo3DSource = CreateChildAudioSource("trackTransTo3DSource", 0, trackTransTo3D, true);
        track3DIntroSource = CreateChildAudioSource("track3DIntroSource", 0, track3DIntro, true);
        track3DSource = CreateChildAudioSource("track3DSource", 0, track3D, true);
        trackTransToRealLifeSource = CreateChildAudioSource("trackTransToRealLifeSource", 0, trackTransToRealLife, true);
        trackRealLifeSource = CreateChildAudioSource("trackRealLifeSource", 0, trackRealLife, true);

        ambienceSource.Play();
    }

    public void StartMusic()
    {
        track2DIntroSource.time = 3.75f; 
        track2DIntroSource.Play();
        track2DSource.time = 3.75f; 
        track2DSource.Play();

        startTime = AudioSettings.dspTime;
    }

    public void PlayFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0 || footstepSource == null) return;
        AudioClip randomClip = footstepClips[Random.Range(0, footstepClips.Length)];
        footstepSource.PlayOneShot(randomClip, footstepVolume);
    }

    private void Update()
    {
        double elapsedTime = AudioSettings.dspTime - startTime;

        if (elapsedTime >= 36.25f && !hasTransitioned)
        {
            hasTransitioned = true;
            FadeOutMusic(track2DIntroSource, 8);
            StartCoroutine(FadeInCoroutine(track2DSource, 8));
        }
    }

    private AudioSource CreateChildAudioSource(string childName, float volume = 1.0f, AudioClip clip = null, bool loop = false)
    {
        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(transform);
        AudioSource audioSource = childObject.AddComponent<AudioSource>();
        audioSource.volume = volume;
        audioSource.loop = loop;
        if (clip != null)
        {
            audioSource.clip = clip;
        }
        return audioSource;
    }


    public void FadeOutMusic(AudioSource source, float duration = 2f)
    {
        StartCoroutine(FadeOutCoroutine(source, duration));
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
}
