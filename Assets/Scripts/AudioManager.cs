using UnityEngine;

public class AudioManager : MonoBehaviour
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

    private double startTime;

    private AudioSource ambienceSource;
    private AudioSource track2DIntroSource;
    private AudioSource track2DSource;
    private AudioSource trackTransTo3DSource;
    private AudioSource track3DIntroSource;
    private AudioSource track3DSource;
    private AudioSource trackTransToRealLifeSource;
    private AudioSource trackRealLifeSource;

    private bool hasTransitioned = false;

    private void Start()
    {
        ambienceSource = CreateChildAudioSource("ambienceSource", 1, ambience);

        track2DIntroSource = CreateChildAudioSource("track2DIntroSource", 1, track2DIntro);
        track2DSource = CreateChildAudioSource("track2DSource", 0, track2D);
        trackTransTo3DSource = CreateChildAudioSource("trackTransTo3DSource", 0, trackTransTo3D);
        track3DIntroSource = CreateChildAudioSource("track3DIntroSource", 0, track3DIntro);
        track3DSource = CreateChildAudioSource("track3DSource", 0, track3D);
        trackTransToRealLifeSource = CreateChildAudioSource("trackTransToRealLifeSource", 0, trackTransToRealLife);
        trackRealLifeSource = CreateChildAudioSource("trackRealLifeSource", 0, trackRealLife);

        
        ambienceSource.Play();
        track2DIntroSource.Play();
        track2DSource.Play();
        
        startTime = AudioSettings.dspTime;
        
        Debug.Log("startTime: " + startTime);
    }

    private void Update()
    {
        double elapsedTime = AudioSettings.dspTime - startTime;
        
        Debug.Log($"Elapsed: {elapsedTime}");
        
        if (elapsedTime >= 40 && !hasTransitioned) 
        {
            hasTransitioned = true;
            FadeOutMusic(track2DIntroSource, 8);
            StartCoroutine(FadeInCoroutine(track2DSource, 8));
        }
    }

    private AudioSource CreateChildAudioSource(string childName, float volume = 1.0f, AudioClip clip = null)
    {
        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(transform);
        AudioSource audioSource = childObject.AddComponent<AudioSource>();
        audioSource.volume = volume;
        audioSource.loop = true;
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
