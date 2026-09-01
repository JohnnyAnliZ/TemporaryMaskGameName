using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoManager : Singleton<VideoManager>
{
	[System.Serializable]
	public class StateConfig {
		public VideoClip mainClip;
		public VideoClip outlineClip;
		public bool isIdle;
		public Rect hotspot = new Rect(0.4f, 0.4f, 0.2f, 0.2f);
	}

	public StateConfig[] configs;

	public VideoPlayer mainPlayer;
	public VideoPlayer outlinePlayer;

	public CanvasGroup canvasGroup;
	public CanvasGroup videoFadeGroup;
	public RectTransform canvasRect;
	public RectTransform frameRect;
	public RawImage outlineImage;
	public RectTransform cursorUI;

	public RectTransform blinkTop;
	public RectTransform blinkBottom;
	public float blinkDuration = 0.4f;
	public float blinkJitter = 0.35f;
	public Vector2 blinkPartialOpen = new Vector2(0.25f, 0.5f);
	public Vector2 ambientBlinkInterval = new Vector2(2f, 5f);
	public Vector3 blinkCountWeights = new Vector3(5f, 3f, 1f);

	public Vector2 glitchInterval = new Vector2(1.5f, 6f);
	public Vector2Int glitchFrames = new Vector2Int(2, 8);
	public Vector2Int glitchRepeats = new Vector2Int(2, 4);

	public Sprite cursorNormalSprite;
	public Sprite cursorHoverSprite;
	public float cursorNormalScale = 1f;
	public float cursorHoverScale = 1.5f;
	public Color cursorNormalColor = Color.white;
	public Color cursorHoverColor = new Color(1f, 0.3f, 0.3f, 1f);
	public float cursorTransitionSpeed = 12f;

	public float[] initialBlinkAmplitudes = new float[] { 0.25f, 0.55f, 0.8f, 1f };
	public float[] initialBlinkDurations  = new float[] { 0.2f,  0.3f,  2.5f, 0.5f };

	Image cursorGraphic;
	float blinkAmplitude = 1f;
	Vector2 topOpenPos, botOpenPos;
	int currentIndex = -1;
	StateConfig currentConfig;
	bool bBlinking;
	Coroutine ambientRoutine;
	Coroutine glitchRoutine;

	protected override void Awake() {
		base.Awake();
		if (Instance != this) return;
		topOpenPos = blinkTop.anchoredPosition;
		botOpenPos = blinkBottom.anchoredPosition;
		canvasGroup.alpha = 0f;
		outlineImage.enabled = false;
		cursorGraphic = cursorUI.GetComponent<Image>();
	}

	//AspectRatioLocker letterboxes/pillarboxes outputCam, so its viewport rect is the only part of the window
	//the player actually sees. Scale the frame to that box (rather than the window the Overlay canvas spans)
	//so everything parented under it keeps its authored design-resolution layout at any window aspect.
	void LateUpdate() {
		if (frameRect == null || canvasRect == null) return;
		Camera outputCam = CompositeManager.Instance != null ? CompositeManager.Instance.outputCam : null;
		if (outputCam == null) return;

		Vector2 design = frameRect.sizeDelta;
		if (design.x <= 0f || design.y <= 0f) return;

		//Worked in canvas units rather than pixels, so this holds for any CanvasScaler mode.
		Rect canvasR = canvasRect.rect;
		Rect view = outputCam.rect;
		float availW = canvasR.width * view.width;
		float availH = canvasR.height * view.height;

		float scale = Mathf.Min(availW / design.x, availH / design.y);
		frameRect.localScale = new Vector3(scale, scale, 1f);

		//Recentre on the viewport rect -- AspectRatioLocker always centres it, but don't bake that in.
		frameRect.anchoredPosition = new Vector2(
			(view.x + view.width * 0.5f - 0.5f) * canvasR.width,
			(view.y + view.height * 0.5f - 0.5f) * canvasR.height
		);
	}

	public void FadeIn(float factor = 1f, int startIndex = 0) {
		StartCoroutine(FadeInCoroutine(factor, startIndex));
	}
	IEnumerator FadeInCoroutine(float factor, int startIndex) {
		Cursors.Set(CursorLockMode.Confined);
		yield return null;

		PlayAt(startIndex);

		bBlinking = true;
		canvasGroup.alpha = 1f;
		ApplyBlinkAmplitude(0f);

		int n = initialBlinkAmplitudes != null ? initialBlinkAmplitudes.Length : 0;
		float scale = Mathf.Max(0f, factor);

		float totalDur = 0f;
		for (int i = 0; i < n; i++) {
			float bd = initialBlinkDurations[Mathf.Min(i, initialBlinkDurations.Length - 1)] * scale;
			totalDur += (i < n - 1) ? bd : bd * 0.5f;
		}

		videoFadeGroup.alpha = 0f;
		StartCoroutine(FadeAlphaTo(videoFadeGroup, 1f, totalDur));

		for (int i = 0; i < n; i++) {
			float amp = initialBlinkAmplitudes[i];
			float blinkDur = initialBlinkDurations[Mathf.Min(i, initialBlinkDurations.Length - 1)];
			float half = blinkDur * scale * 0.5f;

			yield return BlinkAmplitudeTo(amp, half);
			if (i < n - 1) yield return BlinkAmplitudeTo(0f, half);
		}

		ApplyBlinkAmplitude(1f);
		videoFadeGroup.alpha = 1f;
		bBlinking = false;
		ambientRoutine = StartCoroutine(AmbientBlink());
		glitchRoutine = StartCoroutine(Glitch());
	}

	IEnumerator Glitch() {
		while (true) {
			yield return new WaitForSeconds(Random.Range(glitchInterval.x, glitchInterval.y));
			if (!mainPlayer.isPlaying) continue;

			int frames = Random.Range(glitchFrames.x, glitchFrames.y + 1);
			int mode = Random.Range(0, 3);
			int delta = mode == 0 ? frames : -frames;
			int repeats = mode == 2 ? Random.Range(glitchRepeats.x, glitchRepeats.y + 1) : 1;

			for (int i = 0; i < repeats; i++) {
				long maxFrame = (long)mainPlayer.frameCount - 1;
				long target = mainPlayer.frame + delta;
				if (target < 0) target = 0;
				else if (target > maxFrame) target = maxFrame;

				mainPlayer.frame = target;
				if (outlinePlayer.clip != null) outlinePlayer.frame = target;
				//Same jump for the audio bed. Sent as the intended delta rather than the clamped one -- the
				//audio sources wrap where the video clamps, so they want the raw movement.
				AudioManager.Instance.GlitchSeek(delta / (float)mainPlayer.clip.frameRate);

				yield return new WaitForSeconds(frames / (float)mainPlayer.clip.frameRate);
			}
		}
	}

	IEnumerator AmbientBlink() {
		while (true) {
			yield return new WaitForSeconds(Random.Range(ambientBlinkInterval.x, ambientBlinkInterval.y));
			yield return BlinkBurst(false);
		}
	}

	IEnumerator BlinkBurst(bool bAdvance) {
		float half = blinkDuration * 0.5f;
		yield return BlinkAmplitudeTo(0f, half * Random.Range(1f - blinkJitter, 1f + blinkJitter));

		if (bAdvance) PlayAt(currentIndex + 1);

		float roll = Random.value * (blinkCountWeights.x + blinkCountWeights.y + blinkCountWeights.z);
		int blinks = roll < blinkCountWeights.x ? 1 : (roll < blinkCountWeights.x + blinkCountWeights.y ? 2 : 3);
		for (int i = 1; i < blinks; i++) {
			yield return BlinkAmplitudeTo(Random.Range(blinkPartialOpen.x, blinkPartialOpen.y), half * Random.Range(1f - blinkJitter, 1f + blinkJitter));
			yield return BlinkAmplitudeTo(0f, half * Random.Range(1f - blinkJitter, 1f + blinkJitter));
		}
		yield return BlinkAmplitudeTo(1f, half * Random.Range(1f - blinkJitter, 1f + blinkJitter));
	}

	IEnumerator FadeAlphaTo(CanvasGroup cg, float target, float duration) {
		float start = cg.alpha;
		float t = 0f;
		while (t < duration) {
			t += Time.deltaTime;
			cg.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
			yield return null;
		}
		cg.alpha = target;
	}
	IEnumerator BlinkAmplitudeTo(float targetAmp, float duration) {
		float startAmp = blinkAmplitude;
		float t = 0f;
		while (t < duration) {
			t += Time.deltaTime;	
			float u = Mathf.SmoothStep(0f, 1f, t / duration);
			ApplyBlinkAmplitude(Mathf.Lerp(startAmp, targetAmp, u));
			yield return null;
		}
		ApplyBlinkAmplitude(targetAmp);
	}
	void ApplyBlinkAmplitude(float amp) {
		Vector2 topClosed = new Vector2(topOpenPos.x, 0f);
		Vector2 botClosed = new Vector2(botOpenPos.x, 0f);
		blinkTop.anchoredPosition = Vector2.Lerp(topClosed, topOpenPos, amp);
		blinkBottom.anchoredPosition = Vector2.Lerp(botClosed, botOpenPos, amp);
		blinkAmplitude = amp;
	}

	void PlayAt(int index) {
		mainPlayer.loopPointReached -= OnMainEnd;
		if (index >= configs.Length) {
			currentIndex = -1;
			currentConfig = null;
			if (ambientRoutine != null) { StopCoroutine(ambientRoutine); ambientRoutine = null; }
			if (glitchRoutine != null) { StopCoroutine(glitchRoutine); glitchRoutine = null; }
			mainPlayer.Stop();
			outlinePlayer.Stop();
			canvasGroup.alpha = 0;
			cursorUI.gameObject.SetActive(false);
			Cursors.Set(CursorLockMode.Locked);
			GameManager.Instance.runner.CompleteSection();
			return;
		}

		currentIndex = index;
		currentConfig = configs[index];

		//Just for the last one
		bool bAnyIdleAhead = false;
		for (int i = index; i < configs.Length; i++) {
			if (configs[i].isIdle) { bAnyIdleAhead = true; break; }
		}
		cursorUI.gameObject.SetActive(bAnyIdleAhead);

		mainPlayer.clip = currentConfig.mainClip;
		mainPlayer.isLooping = currentConfig.isIdle;
		mainPlayer.Play();
		AudioManager.Instance.HandleRLSound(index, (float)currentConfig.mainClip.length);

		if (currentConfig.outlineClip != null) {
			outlinePlayer.clip = currentConfig.outlineClip;
			outlinePlayer.isLooping = currentConfig.isIdle;
			outlinePlayer.Play();
			Cursors.WarpTo(Cursors.pauseStartUV);
		} else {
			outlinePlayer.Stop();
			outlinePlayer.clip = null;
		}

		if (!currentConfig.isIdle && mainPlayer != null) mainPlayer.loopPointReached += OnMainEnd;
	}

	void OnMainEnd(VideoPlayer vp) {
		StartCoroutine(BlinkAndAdvance());
	}

	void Update() {
		if (canvasGroup.alpha < 1f) return;

		Vector2 screenPos = Mouse.current.position.ReadValue();
		RectTransform hitRect = frameRect != null ? frameRect : canvasRect;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(hitRect, screenPos, null, out Vector2 localPos);

		Rect r = hitRect.rect;
		Vector2 uv = new Vector2(
			(localPos.x - r.xMin) / r.width,
			(localPos.y - r.yMin) / r.height
		);
		bool bInHotspot = currentConfig.hotspot.Contains(uv);

		bool bCanAdvance = currentConfig.isIdle && bInHotspot;
		if (outlineImage != null) outlineImage.enabled = !bBlinking && currentConfig.outlineClip != null && bInHotspot;

		cursorUI.anchoredPosition = localPos;
		float targetScale = bCanAdvance ? cursorHoverScale : cursorNormalScale;
		Color targetColor = bCanAdvance ? cursorHoverColor : cursorNormalColor;
		float k = 1f - Mathf.Exp(-cursorTransitionSpeed * Time.deltaTime);
		float s = Mathf.Lerp(cursorUI.localScale.x, targetScale, k);
		cursorUI.localScale = new Vector3(s, s, 1f);
		cursorGraphic.color = Color.Lerp(cursorGraphic.color, targetColor, k);
		cursorGraphic.sprite = bCanAdvance ? cursorHoverSprite : cursorNormalSprite;

		if (bBlinking) return;
		if (bCanAdvance && Mouse.current.leftButton.wasPressedThisFrame) {
			StartCoroutine(BlinkAndAdvance());
		}
	}

	IEnumerator BlinkAndAdvance() {
		bBlinking = true;
		if (ambientRoutine != null) StopCoroutine(ambientRoutine);
		if (outlineImage != null) outlineImage.enabled = false;

		yield return BlinkBurst(true);

		bBlinking = false;
		if (currentConfig != null) ambientRoutine = StartCoroutine(AmbientBlink());
	}

	void OnDrawGizmos() {
		Vector3 bl, right, up;
		Vector3 origin = transform.position;
		bl = origin - new Vector3(2f, 1.125f, 0f);
		right = new Vector3(4f, 0f, 0f);
		up = new Vector3(0f, 2.25f, 0f);
		Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
		Gizmos.DrawLine(bl, bl + right);
		Gizmos.DrawLine(bl + right, bl + right + up);
		Gizmos.DrawLine(bl + right + up, bl + up);
		Gizmos.DrawLine(bl + up, bl);

		for (int i = 0; i < configs.Length; i++) {
			StateConfig c = configs[i];
			if (c == null) continue;
			Rect h = c.hotspot;
			Vector3 p0 = bl + right * h.x + up * h.y;
			Vector3 p1 = bl + right * (h.x + h.width) + up * h.y;
			Vector3 p2 = bl + right * (h.x + h.width) + up * (h.y + h.height);
			Vector3 p3 = bl + right * h.x + up * (h.y + h.height);

			Gizmos.color = Color.yellow;
			Gizmos.DrawLine(p0, p1);
			Gizmos.DrawLine(p1, p2);
			Gizmos.DrawLine(p2, p3);
			Gizmos.DrawLine(p3, p0);
		}
	}
}
