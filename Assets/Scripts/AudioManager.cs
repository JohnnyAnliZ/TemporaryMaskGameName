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
    public AudioClip[] shatterClips;
    public AudioClip[] crackingClips;
    public AudioClip sinkIdle;
    public AudioClip sinkIdleHands;
    public AudioClip washFace;
    public AudioClip pills;
    public AudioClip mirrorLookDown;
    public AudioClip mirrorIdle;
    public AudioClip mirrorCheck;

    [Header("Ambience Clips")]
    public AudioClip ambience;
    public AudioClip glitchyAmbience;

    [Header("Footstep Settings")]
    public AudioClip[] footstepClips2D;
    public AudioClip[] footstepClipsTrans;
    public AudioClip[] footstepClips3D;
    public float footstepVolume = 1.0f;
    public float footstepInterval = 0.5f;

    [Header("Impact Settings")]
    public AudioClip[] impactClips2D;
    public AudioClip[] impactClipsTrans;
    public AudioClip[] impactClips3D;
    public float impactVolume = 1.0f;

    // Music sources
    private AudioSource track2DIntroSource;
    private AudioSource track2DSource;
    private AudioLowPassFilter track2DFilter;
    private AudioSource trackTransTo3DSource;
    private AudioSource track3DIntroSource;
    private AudioSource track3DSource;
    private AudioLowPassFilter track3DFilter;
    private AudioSource trackTransToRealLifeSource;
    private AudioLowPassFilter trackTransToRealLifeFilter;
    private AudioSource trackRealLifeSource;

    // SFX sources
    private AudioSource sfxSource;
    private AudioSource sinkIdleSource;
    private AudioSource sinkIdleHandsSource;
    private AudioSource mirrorIdleSource;
    private AudioSource ambienceSource;
    private AudioSource glitchyAmbienceSource;
    private AudioSource footstepSource;
    private AudioSource impactSource;
    
    // Vars
	float footstepTimer = 0;

    private int shattersPlayed = 0;
    private float shatterVol = 0.1f;

    private int cracksPlayed = 0;
    private float crackVol = 0.4f;

    private AudioClip[] footstepClips;
    private AudioClip[] impactClips;

    private double startTime = 100000;
    private bool hasTransitioned = false;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        // Audio sources
        // SFX
        sfxSource = CreateChildAudioSource("sfxSource", 0.5f, null, false);
        sinkIdleSource = CreateChildAudioSource("sinkIdleSource", 0.5f, sinkIdle, true);
        sinkIdleHandsSource = CreateChildAudioSource("sinkIdleHandsSource", 0.5f, sinkIdleHands, true);
        mirrorIdleSource = CreateChildAudioSource("mirrorIdleSource", 0.5f, mirrorIdle, true);

        ambienceSource = CreateChildAudioSource("ambienceSource", 1, ambience, true);
        glitchyAmbienceSource = CreateChildAudioSource("glitchyAmbienceSource", 0.03f, glitchyAmbience, true);

        footstepClips = footstepClips2D;
        footstepSource = CreateChildAudioSource("footstepSource", footstepVolume, footstepClips[0], false);

        impactClips = impactClips2D;
        impactSource = CreateChildAudioSource("impactSource", impactVolume, impactClips[0], false);

        // Music
        track2DIntroSource = CreateChildAudioSource("track2DIntroSource", 0, track2DIntro, true);

        track2DSource = CreateChildAudioSource("track2DSource", 0, track2D, true);
        track2DFilter = track2DSource.gameObject.AddComponent<AudioLowPassFilter>();
        track2DFilter.cutoffFrequency = 22000f;

        trackTransTo3DSource = CreateChildAudioSource("trackTransTo3DSource", 0, trackTransTo3D, true);

        track3DIntroSource = CreateChildAudioSource("track3DIntroSource", 0.6f, track3DIntro, false);

        track3DSource = CreateChildAudioSource("track3DSource", 0, track3D, true);
        track3DFilter = track3DSource.gameObject.AddComponent<AudioLowPassFilter>();
        track3DFilter.cutoffFrequency = 22000f;
        
        trackTransToRealLifeSource = CreateChildAudioSource("trackTransToRealLifeSource", 0, trackTransToRealLife, true);
        trackTransToRealLifeFilter = trackTransToRealLifeSource.gameObject.AddComponent<AudioLowPassFilter>();
        trackTransToRealLifeFilter.cutoffFrequency = 0;

        trackRealLifeSource = CreateChildAudioSource("trackRealLifeSource", 0, trackRealLife, true);

        // Start ambience
        ambienceSource.Play();
        glitchyAmbienceSource.Play();
    }

    public void StartMusic()
    {
        track2DIntroSource.volume = 1.0f;
        track2DIntroSource.time = 3.75f;
        track2DIntroSource.Play();
        track2DSource.time = 3.75f;
        track2DSource.Play();
        trackTransTo3DSource.time = 3.75f;
        trackTransTo3DSource.Play();
        track3DSource.time = 3.75f;
        track3DSource.Play();
        trackTransToRealLifeSource.time = 3.75f;
        trackTransToRealLifeSource.Play();
        trackRealLifeSource.time = 3.75f;
        trackRealLifeSource.Play();

        startTime = AudioSettings.dspTime;
    }

    public void HandleFootsteps(Vector3 movementDirection, bool isGrounded) {
		// Only play footsteps if grounded and moving
		bool isWalking = movementDirection != Vector3.zero && isGrounded;

		if (isWalking) {
			footstepTimer -= Time.deltaTime;

			if (footstepTimer <= 0f) {
				PlayFootstep();
				footstepTimer = footstepInterval;
			}
		} else {
			footstepTimer = 0f;
		}
	}

    public void PlayFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0 || footstepSource == null) return;
        AudioClip randomClip = footstepClips[Random.Range(0, footstepClips.Length)];
        footstepSource.PlayOneShot(randomClip, footstepVolume);
    }

    public void HandleImpact(float verticalVelocity)
    {
        AudioClip randomClip;
        if (verticalVelocity > -12f) {
			impactVolume = 0.4f;
            randomClip = impactClips[impactClips.Length-1];
		} else if (verticalVelocity <= -12f && verticalVelocity >= -20f) {
			impactVolume = 0.2f + ((-verticalVelocity - 12f) * 0.01f); // scale by velocity
            randomClip = impactClips[Random.Range(0, impactClips.Length-1)];
		} else {
			impactVolume = 1f;
            randomClip = impactClips[Random.Range(0, impactClips.Length-1)];
		}
        if (impactClips == null || impactClips.Length == 0 || impactSource == null) return;
        impactSource.PlayOneShot(randomClip, impactVolume);
    }

    public void HandleShatter()
    {
        // Play shatter sound effect
        if (shatterClips == null || shatterClips.Length == 0 || sfxSource == null || shattersPlayed >= Globals.Instance.numBreaks) return;
        AudioClip currClip = shatterClips[shattersPlayed];
        sfxSource.PlayOneShot(currClip, shatterVol);
        shattersPlayed += 1;
        shatterVol += 0.1f;

        // Transition music
        switch (shattersPlayed)
        {
            case 1:
                FadeToVolume(track2DIntroSource, 0f, 8);
                FadeToVolume(track2DSource, 1f, 8);
                FadeLowPassFilterCutoff(track2DFilter, 18000f, 0.5f);
                FadeToVolume(trackTransTo3DSource, 0.05f, 1);
                glitchyAmbienceSource.volume = 0.06f;
                break; 
            case 2:
                FadeLowPassFilterCutoff(track2DFilter, 14000f, 0.5f);
                FadeToVolume(trackTransTo3DSource, 0.1f, 1);
                glitchyAmbienceSource.volume = 0.09f;
                break;
            case 3:
                FadeLowPassFilterCutoff(track2DFilter, 10000f, 0.5f);
                FadeToVolume(trackTransTo3DSource, 0.15f, 1);
                glitchyAmbienceSource.volume = 0.12f;
                break;
            case 4:
                FadeLowPassFilterCutoff(track2DFilter, 6000f, 0.5f);
                FadeToVolume(trackTransTo3DSource, 0.25f, 1);
                glitchyAmbienceSource.volume = 0.15f;
                footstepClips = footstepClipsTrans;
                footstepSource.volume += 0.05f;
                impactClips = impactClipsTrans;
                break;
            case 5:
                FadeLowPassFilterCutoff(track2DFilter, 3000f, 0.5f);
                FadeToVolume(trackTransTo3DSource, 0.45f, 1);
                glitchyAmbienceSource.volume = 0.18f;
                break;
            case 6:
                FadeLowPassFilterCutoff(track2DFilter, 1000f, 0.5f);
                FadeToVolume(trackTransTo3DSource, 0.7f, 1);
                glitchyAmbienceSource.volume = 0.21f;
                break;
            default:
                FadeLowPassFilterCutoff(track2DFilter, 0, 4);
                FadeToVolume(track2DSource, 0f, 8);
                FadeToVolume(trackTransTo3DSource, 1f, 1);
                glitchyAmbienceSource.volume = 0.24f;
                footstepClips = footstepClips3D;
                footstepSource.volume += 0.05f;
                impactClips = impactClips3D;
                FadeToVolume(trackTransTo3DSource, 0f, 8);
                FadeToVolume(track3DSource, 1f, 8);
                track3DIntroSource.Play();
                break;
        }
    }

    public void HandleShrink()
    {
        // Play cracking sound effect
        if (crackingClips == null || crackingClips.Length == 0 || sfxSource == null || cracksPlayed >= Globals.Instance.num3DBreaks) return;
        AudioClip currClip = crackingClips[cracksPlayed];
        sfxSource.PlayOneShot(currClip, crackVol);
        cracksPlayed += 1;
        crackVol += 0.15f;

        // Transition music
        switch (cracksPlayed)
        {
            case 1:
                FadeToVolume(track3DIntroSource, 0f, 8);
                trackTransToRealLifeFilter.cutoffFrequency = 300f;
                FadeToVolume(trackTransToRealLifeSource, 0.4f, 1);
                FadeLowPassFilterCutoff(track3DFilter, 1000f, 0.2f);
                ambienceSource.volume = 0.75f;
                glitchyAmbienceSource.volume = 0.18f;
                break; 
            case 2:
                trackTransToRealLifeFilter.cutoffFrequency = 400f;
                FadeToVolume(trackTransToRealLifeSource, 0.6f, 1);
                FadeLowPassFilterCutoff(track3DFilter, 600f, 0.2f);
                ambienceSource.volume = 0.50f;
                glitchyAmbienceSource.volume = 0.12f;
                break;
            case 3:
                trackTransToRealLifeFilter.cutoffFrequency = 500f;
                FadeToVolume(trackTransToRealLifeSource, 0.8f, 1);
                FadeLowPassFilterCutoff(track3DFilter, 400f, 0.2f);
                ambienceSource.volume = 0.25f;
                glitchyAmbienceSource.volume = 0.06f;
                break;
            default:
                trackTransToRealLifeFilter.cutoffFrequency = 5000f;
                FadeToVolume(trackTransToRealLifeSource, 1f, 1);
                track3DFilter.cutoffFrequency = 300f;
                FadeLowPassFilterCutoff(track3DFilter, 0f, 4);
                FadeToVolume(track3DSource, 0, 8);
                ambienceSource.volume = 0;
                glitchyAmbienceSource.volume = 0;
                FadeToVolume(trackTransToRealLifeSource, 0f, 4);
                FadeToVolume(trackRealLifeSource, 1f, 4);
                break;
        }
    }

    private void Update()
    {
        double elapsedTime = AudioSettings.dspTime - startTime;

        if (elapsedTime >= 36.25f && !hasTransitioned)
        {
            hasTransitioned = true;
            FadeToVolume(track2DIntroSource, 0f, 8);
            FadeToVolume(track2DSource, 1f, 8);
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


    public void FadeToVolume(AudioSource source, float targetVolume, float duration = 2f)
    {
        StartCoroutine(FadeToVolumeCoroutine(source, targetVolume, duration));
    }

    private System.Collections.IEnumerator FadeToVolumeCoroutine(AudioSource source, float targetVolume, float duration)
    {
        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            yield return null;
        }

        source.volume = targetVolume;
        
        if (targetVolume == 0f)
        {
            source.Stop();
        }
    }

    public void FadeLowPassFilterCutoff(AudioLowPassFilter filter, float targetCutoff, float duration = 2f)
    {
        StartCoroutine(FadeLowPassFilterCutoffCoroutine(filter, targetCutoff, duration));
    }

    private System.Collections.IEnumerator FadeLowPassFilterCutoffCoroutine(AudioLowPassFilter filter, float targetCutoff, float duration)
    {
        float startCutoff = filter.cutoffFrequency;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            filter.cutoffFrequency = Mathf.Lerp(startCutoff, targetCutoff, elapsed / duration);
            yield return null;
        }

        filter.cutoffFrequency = targetCutoff;
    }
}
