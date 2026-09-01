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
    public AudioClip trackTrans;

    [Header("SFX Clips")]
    public AudioClip introSink;
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

    [Header("2D -> 3D Handoff")]
    public float handoffWait = 6.0f;
    public float handoffCrossfade = 3.0f;
    public float track3DIntroVolume = 0.6f;

    [Header("Trans Sections")]
    public float trans3DGlitchy = 0.5f;
    public float trans2DGlitchy = 0.8f;
    public float transFadeIn = 4.0f;
    public float introWaterVolume = 1.0f;
    public float introWaterFadeOut = 3.0f;

    public float shrinkShatterDelay = 0.25f;
    public float shrinkShatterVolume = 0.5f;

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
    private AudioSource trackTransSource;

    // SFX sources
    private AudioSource sfxSource;
    private AudioSource sinkIdleSource;
    private AudioSource mirrorIdleSource;
    private AudioSource ambienceSource;
    private AudioSource glitchyAmbienceSource;
    private AudioSource footstepSource;
    private AudioSource impactSource;
    
    // Vars
    bool musicStarted = false;

	float footstepTimer = 0;

    private int shattersPlayed = 0;
    private float shatterVol = 0.1f;

    private int cracksPlayed = 0;
    private float crackVol = 0.4f;

    private AudioClip[] footstepClips;
    private AudioClip[] impactClips;

    private double startTime = 100000;
    private double elapsedTime = 0;
    private float transTime = 36.25f;
    private bool hasTransitioned = false;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        reset();
    }

    public void reset()
    {
        StopAllCoroutines();
        for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

        musicStarted = false;
        footstepTimer = 0f;
        shattersPlayed = 0;
        shatterVol = 0.1f;
        cracksPlayed = 0;
        crackVol = 0.4f;
        startTime = 100000;
        elapsedTime = 0;
        transTime = 36.25f;
        hasTransitioned = false;

        // Audio sources
        // SFX
        sfxSource = CreateChildAudioSource("sfxSource", 0.5f, null, false);
        sinkIdleSource = CreateChildAudioSource("sinkIdleSource", 1.0f, sinkIdle, true);
        mirrorIdleSource = CreateChildAudioSource("mirrorIdleSource", 1.0f, mirrorIdle, true);

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

        track3DIntroSource = CreateChildAudioSource("track3DIntroSource", 0, track3DIntro, false);

        track3DSource = CreateChildAudioSource("track3DSource", 0, track3D, true);
        track3DFilter = track3DSource.gameObject.AddComponent<AudioLowPassFilter>();
        track3DFilter.cutoffFrequency = 22000f;

        trackTransToRealLifeSource = CreateChildAudioSource("trackTransToRealLifeSource", 0, trackTransToRealLife, true);
        trackTransToRealLifeFilter = trackTransToRealLifeSource.gameObject.AddComponent<AudioLowPassFilter>();
        trackTransToRealLifeFilter.cutoffFrequency = 0;

        trackRealLifeSource = CreateChildAudioSource("trackRealLifeSource", 0, trackRealLife, true);
        trackRealLifeSource.panStereo = 0.5f;

        trackTransSource = CreateChildAudioSource("trackTransSource", 0, trackTrans, true);

        // Start ambience
        ambienceSource.Play();
        glitchyAmbienceSource.Play();
    }

    void SilenceAllMusic()
    {
        track2DIntroSource.volume = 0f;
        track2DSource.volume = 0f;
        trackTransTo3DSource.volume = 0f;
        track3DIntroSource.volume = 0f;
        track3DSource.volume = 0f;
        trackTransToRealLifeSource.volume = 0f;
        trackRealLifeSource.volume = 0f;
        trackTransSource.volume = 0f;
        track2DFilter.cutoffFrequency = 22000f;
        track3DFilter.cutoffFrequency = 22000f;
        trackTransToRealLifeFilter.cutoffFrequency = 0f;
    }

    public void HandleSubsection(string subsection) {
        SilenceAllMusic();

        switch (subsection) {
            case "IntroFlowerSubsection":
                sfxSource.Stop();
                track2DIntroSource.volume = 1.0f;
                ambienceSource.volume = 1.0f;
                glitchyAmbienceSource.volume = 0.03f;
                StartMusic(3.75f);
                startTime = AudioSettings.dspTime;
                break; 
            case "TwoD":
                track2DIntroSource.volume = 1.0f;
                ambienceSource.volume = 1.0f;
                glitchyAmbienceSource.volume = 0.03f;
                if (!musicStarted) {
                    startTime = AudioSettings.dspTime;
                    transTime -= 8.0f;
                }
                StartMusic(3.75f + 8.0f);
                break; 
            case "TwoDBreak":
                hasTransitioned = true;
                track2DSource.volume = 1.0f;
                ambienceSource.volume = 1.0f;
                glitchyAmbienceSource.volume = 0.03f;
                StartMusic();
                break; 
            case "ThreeD":
            case "ThreeDBreak":
                hasTransitioned = true;
                track2DFilter.cutoffFrequency = 0f;
                track3DSource.volume = 1.0f;
                ambienceSource.volume = 0.0f;
                glitchyAmbienceSource.volume = 0.4f;
                footstepClips = footstepClips3D;
                footstepSource.volume = footstepVolume + 0.1f;
                impactClips = impactClips3D;
                StartMusic();
                break; 
            case "LiveActionSubsection":
                hasTransitioned = true;
                trackTransToRealLifeFilter.cutoffFrequency = 5000f;
                trackRealLifeSource.volume = 1.0f;
                ambienceSource.volume = 0.0f;
                glitchyAmbienceSource.volume = 0.0f;
                StartMusic();
                break;
            case "Trans3DSubsection":
                hasTransitioned = true;
                trackRealLifeSource.volume = 1.0f;
                trackTransSource.volume = 1.0f;
                if (!trackTransSource.isPlaying) trackTransSource.Play();
                ambienceSource.volume = 0.0f;
                glitchyAmbienceSource.volume = trans3DGlitchy;
                StartMusic();
                break;
            case "Trans2DSubsection":
                hasTransitioned = true;
                trackRealLifeSource.volume = 1.0f;
                trackTransSource.volume = 1.0f;
                if (!trackTransSource.isPlaying) trackTransSource.Play();
                ambienceSource.volume = 0.0f;
                glitchyAmbienceSource.volume = trans2DGlitchy;
                StartMusic();
                break;
            default:
                Log.Warn($"HandleSubsection: unknown subsection {subsection}");
                break;
        }
    }

    private void StartMusic(float startTime = 0.0f) {
        if (!musicStarted) {
            track2DIntroSource.time = startTime;
            track2DIntroSource.Play();
            track2DSource.time = startTime;
            track2DSource.Play();
            trackTransTo3DSource.time = startTime;
            trackTransTo3DSource.Play();
            track3DSource.time = startTime;
            track3DSource.Play();
            trackTransToRealLifeSource.time = startTime;
            trackTransToRealLifeSource.Play();
            trackRealLifeSource.time = startTime;
            trackRealLifeSource.Play();

            musicStarted = true;
        }
    }

    public void PlayIntroSink() {
        sfxSource.PlayOneShot(introSink, 1f);
        trackTransSource.volume = introWaterVolume;
        trackTransSource.Play();
        StartCoroutine(FadeOutIntroWater());
    }
    private System.Collections.IEnumerator FadeOutIntroWater() {
        yield return new WaitForSeconds(Mathf.Max(0f, introSink.length - introWaterFadeOut));
        FadeToVolume(trackTransSource, 0f, introWaterFadeOut);
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

    public void PlayFootstep() {
        AudioClip randomClip = footstepClips[Random.Range(0, footstepClips.Length)];
        footstepSource.PlayOneShot(randomClip, footstepVolume);
    }

    public void HandleImpact(float verticalVelocity) {
        AudioClip randomClip;

        float volume;
        if (verticalVelocity > -12f) {
			volume = 0.4f;
            randomClip = impactClips[impactClips.Length-1];
		} else if (verticalVelocity <= -12f && verticalVelocity >= -20f) {
			volume = 0.2f + ((-verticalVelocity - 12f) * 0.01f); // scale by velocity
            randomClip = impactClips[Random.Range(0, impactClips.Length-1)];
		} else {
			volume = 1f;
            randomClip = impactClips[Random.Range(0, impactClips.Length-1)];
		}
        impactSource.PlayOneShot(randomClip, volume);
    }

    public void HandleShatter() {
        if (shatterClips == null || shatterClips.Length == 0 || sfxSource == null || shattersPlayed >= Globals.Instance.numBreaks) return;

        AudioClip currClip = shatterClips[Mathf.Min(shattersPlayed, shatterClips.Length - 1)];
        sfxSource.PlayOneShot(currClip, shatterVol);
        shattersPlayed += 1;
        shatterVol += 0.1f;

        switch (shattersPlayed) {
            case 1:
                if (!hasTransitioned) {
                    hasTransitioned = true;
                    FadeToVolume(track2DIntroSource, 0f, 8);
                }
                FadeToVolume(track2DSource, 0.85f, 1);
                FadeLowPassFilterCutoff(track2DFilter, 9000, 1);
                FadeToVolume(trackTransTo3DSource, 0.2f, 1);
                ambienceSource.volume = 0.85f;
                glitchyAmbienceSource.volume = 0.1f;
                break;
            case 2:
                FadeToVolume(track2DSource, 0.65f, 1);
                FadeLowPassFilterCutoff(track2DFilter, 5000, 1);
                FadeToVolume(trackTransTo3DSource, 0.45f, 1);
                ambienceSource.volume = 0.6f;
                glitchyAmbienceSource.volume = 0.15f;
                break;
            case 3:
                FadeToVolume(track2DSource, 0.5f, 1);
                FadeLowPassFilterCutoff(track2DFilter, 2000, 1);
                FadeToVolume(trackTransTo3DSource, 0.7f, 1);
                ambienceSource.volume = 0.4f;
                glitchyAmbienceSource.volume = 0.22f;
                footstepClips = footstepClipsTrans;
                footstepSource.volume = footstepVolume + 0.05f;
                impactClips = impactClipsTrans;
                break;
            case 4:
                FadeToVolume(track2DSource, 0.3f, 1);
                FadeLowPassFilterCutoff(track2DFilter, 1000f, 1);
                FadeToVolume(trackTransTo3DSource, 1, 1);
                ambienceSource.volume = 0.2f;
                glitchyAmbienceSource.volume = 0.26f;
                break;
            case 5:
                FadeLowPassFilterCutoff(track2DFilter, 600f, 1);
                ambienceSource.volume = 0.1f;
                glitchyAmbienceSource.volume = 0.3f;
                break;
        }

        if (shattersPlayed >= Globals.Instance.numBreaks) {
            FadeToVolume(track2DSource, 0f, handoffCrossfade);
            FadeLowPassFilterCutoff(track2DFilter, 0, 4);
            ambienceSource.volume = 0.0f;
            glitchyAmbienceSource.volume = 0.4f;
            footstepClips = footstepClips3D;
            footstepSource.volume = footstepVolume + 0.1f;
            impactClips = impactClips3D;
            StartCoroutine(HandoffTo3D());
            return;
        }

    }
    private System.Collections.IEnumerator HandoffTo3D() {
        FadeToVolume(trackTransTo3DSource, 0f, handoffCrossfade);
        track3DIntroSource.Play();
        FadeToVolume(track3DIntroSource, track3DIntroVolume, handoffCrossfade);
        yield return new WaitForSeconds(handoffWait);

        FadeToVolume(track3DIntroSource, 0f, handoffCrossfade);
        FadeToVolume(track3DSource, 1f, handoffCrossfade);
    }

    public void HandleShrink(bool isFinal) {
        do {
        if (crackingClips == null || crackingClips.Length == 0 || sfxSource == null || cracksPlayed >= Globals.Instance.num3DBreaks) return;

        AudioClip currClip = crackingClips[Mathf.Min(cracksPlayed, crackingClips.Length - 1)];
        sfxSource.PlayOneShot(currClip, crackVol);
        StartCoroutine(PlayShrinkShatter());
        cracksPlayed += 1;
        crackVol += 0.15f;

        if (cracksPlayed >= Globals.Instance.num3DBreaks) {
            FadeLowPassFilterCutoff(trackTransToRealLifeFilter, 5000f, 1);
            FadeToVolume(trackTransToRealLifeSource, 0, 6);
            FadeToVolume(track3DSource, 0, 1);
            FadeToVolume(trackRealLifeSource, 1f, 4);
            glitchyAmbienceSource.volume = 0;
            return;
        }

        switch (cracksPlayed) {
            case 1:
                FadeToVolume(trackTransToRealLifeSource, 0.3f, 1);
                FadeLowPassFilterCutoff(trackTransToRealLifeFilter, 500f, 1);
                FadeToVolume(track3DSource, 0.9f, 1);
                FadeLowPassFilterCutoff(track3DFilter, 10000f, 1);
                glitchyAmbienceSource.volume = 0.3f;
                break; 
            case 2:
                FadeToVolume(trackTransToRealLifeSource, 0.55f, 1);
                FadeLowPassFilterCutoff(trackTransToRealLifeFilter, 1000f, 1);
                FadeToVolume(track3DSource, 0.75f, 1);
                FadeLowPassFilterCutoff(track3DFilter, 5000f, 1);
                glitchyAmbienceSource.volume = 0.2f;
                break;
            case 3:
                FadeToVolume(trackTransToRealLifeSource, 0.8f, 1);
                FadeLowPassFilterCutoff(trackTransToRealLifeFilter, 1500f, 1);
                FadeToVolume(track3DSource, 0.4f, 1);
                FadeLowPassFilterCutoff(track3DFilter, 1000f, 1);
                glitchyAmbienceSource.volume = 0.1f;
                break;
            case 4:
                FadeToVolume(trackTransToRealLifeSource, 1, 1);
                FadeLowPassFilterCutoff(trackTransToRealLifeFilter, 2500f, 1);
                FadeToVolume(track3DSource, 0.2f, 1);
                FadeLowPassFilterCutoff(track3DFilter, 500f, 1);
                break;
        }
        } while (isFinal);
    }
    private System.Collections.IEnumerator PreLapTransBed(float clipLength) {
        yield return new WaitForSeconds(Mathf.Max(0f, clipLength - transFadeIn));
        trackTransSource.Play();
        FadeToVolume(trackTransSource, 1.0f, transFadeIn);
    }

    private System.Collections.IEnumerator PlayShrinkShatter() {
        yield return new WaitForSeconds(shrinkShatterDelay);
        sfxSource.PlayOneShot(shatterClips[6], shrinkShatterVolume);
    }

    //The live action picture jumps, stutters and skips its way through the section. The audio under it has to
    //take the same jumps or the two visibly come apart -- the video players carry no audio of their own, so
    //nothing follows those seeks unless we do it here. Wrapped rather than clamped, so a backward jump near
    //the head of a loop lands at the tail instead of piling up on zero.
    public void GlitchSeek(float deltaSeconds)
    {
        void Seek(AudioSource s) {
            if (!s.isPlaying) return;
            float len = s.clip.length;
            float t = s.time + deltaSeconds;
            if (t < 0f) t += len;
            else if (t >= len) t -= len;
            s.time = Mathf.Clamp(t, 0f, len - 0.05f);
        }

        Seek(trackRealLifeSource);
        Seek(mirrorIdleSource);
        Seek(sinkIdleSource);
    }

    public void HandleRLSound(int index, float clipLength) {
        switch (index) {
            case 0:
                mirrorIdleSource.Play();
                break;
            case 1:
                FadeToVolume(mirrorIdleSource, 0.0f, 1.0f);
                sfxSource.PlayOneShot(mirrorCheck, 1.0f);
                break;
            case 2:
                mirrorIdleSource.Play();
                FadeToVolume(mirrorIdleSource, 1.0f, 1.0f);
                break;
            case 3:
                FadeToVolume(mirrorIdleSource, 0.0f, 1.0f);
                sfxSource.PlayOneShot(mirrorLookDown, 1.0f);
                break;
            case 4:
                sinkIdleSource.Play();
                break;
            case 5:
                FadeToVolume(sinkIdleSource, 0.0f, 1.0f);
                sfxSource.PlayOneShot(pills, 1.0f);
                break;
            case 6:
                FadeToVolume(sinkIdleSource, 1.0f, 1.0f);
                sinkIdleSource.Play();
                break;
            default:
                FadeToVolume(sinkIdleSource, 0.0f, 1.0f);
                sfxSource.PlayOneShot(washFace, 1.0f);
                StartCoroutine(PreLapTransBed(clipLength));
                break;
        }
    }

    public void FadeOutForIntro(float duration) {
        FadeToVolume(trackRealLifeSource, 0f, duration);
        FadeToVolume(trackTransSource, 0f, duration);
        FadeToVolume(glitchyAmbienceSource, 0f, duration);
        FadeToVolume(ambienceSource, 0f, duration);
    }

    private void Update()
    {
        elapsedTime = AudioSettings.dspTime - startTime;

        if (elapsedTime >= transTime && !hasTransitioned)
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
