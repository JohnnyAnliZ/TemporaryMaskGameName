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
	public RawImage outlineImage;
	public RectTransform cursorUI;

	public RectTransform blinkTop;
	public RectTransform blinkBottom;
	public Image blinkBack;
	public float blinkDuration = 0.8f;

	public float cursorNormalScale = 0.1f;
	public float cursorHoverScale = 0.2f;
	public Color cursorNormalColor = Color.white;
	public Color cursorHoverColor = new Color(0.5f, 0.3f, 0.3f, 1f);
	public float cursorTransitionSpeed = 12f;

	public float[] initialBlinkAmplitudes = new float[] { 0.25f, 0.55f, 0.8f, 1f };
	public float[] initialBlinkDurations  = new float[] { 0.2f,  0.3f,  2.5f, 0.5f };

	Graphic cursorGraphic;
	Vector2 topOpenPos, botOpenPos;
	int currentIndex = -1;
	StateConfig currentConfig;
	bool bBlinking;

	protected override void Awake() {
		base.Awake();
		if (Instance != this) return;
		topOpenPos = blinkTop.anchoredPosition;
		botOpenPos = blinkBottom.anchoredPosition;
		canvasGroup.alpha = 0f;
		outlineImage.enabled = false;
		cursorGraphic = cursorUI.GetComponent<Graphic>();
		Color c = blinkBack.color;
		c.a = 0f;
		blinkBack.color = c;
	}

	public void FadeIn(float factor = 1f, int startIndex = 0) {
		StartCoroutine(FadeInCoroutine(factor, startIndex));
	}
	IEnumerator FadeInCoroutine(float factor, int startIndex) {
		Cursor.lockState = CursorLockMode.Confined;
		Cursor.visible = false;

		PlayAt(startIndex);

		canvasGroup.alpha = 1f;
		ApplyBlinkAmplitude(0f);

		int n = initialBlinkAmplitudes != null ? initialBlinkAmplitudes.Length : 0;
		float scale = Mathf.Max(0f, factor);

		//Total blink runtime so the parallel video fade matches
		float totalDur = 0f;
		for (int i = 0; i < n; i++) {
			float bd = initialBlinkDurations[Mathf.Min(i, initialBlinkDurations.Length - 1)] * scale;
			totalDur += (i < n - 1) ? bd : bd * 0.5f;
		}

		if (videoFadeGroup != null) {
			videoFadeGroup.alpha = 0f;
			StartCoroutine(FadeAlphaTo(videoFadeGroup, 1f, totalDur));
		}

		for (int i = 0; i < n; i++) {
			float amp = initialBlinkAmplitudes[i];
			float blinkDur = initialBlinkDurations[Mathf.Min(i, initialBlinkDurations.Length - 1)];
			float half = blinkDur * scale * 0.5f;

			yield return BlinkAmplitudeTo(amp, half);
			if (i < n - 1) yield return BlinkAmplitudeTo(0f, half);
		}

		ApplyBlinkAmplitude(1f);
		if (videoFadeGroup != null) videoFadeGroup.alpha = 1f;
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
		float startAmp = blinkBack != null ? 1f - blinkBack.color.a : 0f;
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
		Color c = blinkBack.color;
		c.a = 1f - amp;
		blinkBack.color = c;
	}

	void PlayAt(int index) {
		mainPlayer.loopPointReached -= OnMainEnd;
		if (index >= configs.Length) {
			currentIndex = -1;
			currentConfig = null;
			//TODO: move player back to sink, set the animation of hand at the point where it covers the screen
			GameObject player3D = GameManager.Instance.player3D;
			GameManager.Instance.bInputEnabled = true;
            CompositeManager.Instance.maskDrawer.ResetMask3D();
            player3D.GetComponent<Player3DController>().Reset();
			GameObject.Find("hand").GetComponent<AnimationController>().play = true;
            //hide canvas
            canvasGroup.alpha = 0;
            return;
		}

		currentIndex = index;
		currentConfig = configs[index];

		mainPlayer.clip = currentConfig.mainClip;
		mainPlayer.isLooping = currentConfig.isIdle;
		mainPlayer.Play();

		if (currentConfig.outlineClip != null) {
			outlinePlayer.clip = currentConfig.outlineClip;
			outlinePlayer.isLooping = currentConfig.isIdle;
			outlinePlayer.Play();
		} else {
			outlinePlayer.Stop();
			outlinePlayer.clip = null;
		}

		if (!currentConfig.isIdle && mainPlayer != null) mainPlayer.loopPointReached += OnMainEnd;
	}

	void OnMainEnd(VideoPlayer vp) {
		PlayAt(currentIndex + 1);
	}

	void Update() {
		if (canvasGroup == null || canvasGroup.alpha < 1f) return;
		if (bBlinking) return;
		if (currentConfig == null) return;

		Mouse mouse = Mouse.current;
		if (mouse == null) return;

		Vector2 screenPos = mouse.position.ReadValue();

		bool bHasOutline = currentConfig.outlineClip != null;
		if (cursorUI != null) cursorUI.gameObject.SetActive(bHasOutline);
		if (!bHasOutline && outlineImage != null) outlineImage.enabled = false;

		if (canvasRect != null) {
			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out Vector2 localPos);
			if (cursorUI != null && bHasOutline) cursorUI.anchoredPosition = localPos;

			Rect r = canvasRect.rect;
			Vector2 uv = new Vector2(
				(localPos.x - r.xMin) / r.width,
				(localPos.y - r.yMin) / r.height
			);
			bool bHover = bHasOutline && currentConfig.hotspot.Contains(uv);
			if (outlineImage != null && bHasOutline) outlineImage.enabled = bHover;

			//Cursor hover
			if (bHasOutline) {
				float targetScale = bHover ? cursorHoverScale : cursorNormalScale;
				Color targetColor = bHover ? cursorHoverColor : cursorNormalColor;
				float k = 1f - Mathf.Exp(-cursorTransitionSpeed * Time.deltaTime);
				float s = Mathf.Lerp(cursorUI.localScale.x, targetScale, k);
				cursorUI.localScale = new Vector3(s, s, 1f);
				if (cursorGraphic != null) cursorGraphic.color = Color.Lerp(cursorGraphic.color, targetColor, k);
			}

			if (mouse.leftButton.wasPressedThisFrame) {
				Debug.Log($"[VM] click: isIdle={currentConfig.isIdle} bHover={bHover} currentIndex={currentIndex}");
			}
			if (currentConfig.isIdle && bHover && mouse.leftButton.wasPressedThisFrame) {
				StartCoroutine(BlinkAndAdvance());
			}
		}
	}

	IEnumerator BlinkAndAdvance() {
		Debug.Log($"[VM] BlinkAndAdvance start: currentIndex={currentIndex} blinkDuration={blinkDuration} topOpen={topOpenPos} botOpen={botOpenPos}");
		bBlinking = true;
		if (outlineImage != null) outlineImage.enabled = false;

		float half = blinkDuration * 0.5f;
		Vector2 topClosed = new Vector2(topOpenPos.x, 0f);
		Vector2 botClosed = new Vector2(botOpenPos.x, 0f);

		//Close
		float t = 0f;
		while (t < half) {
			t += Time.deltaTime;
			float u = Mathf.SmoothStep(0f, 1f, t / half);
			blinkTop.anchoredPosition = Vector2.Lerp(topOpenPos, topClosed, u);
			blinkBottom.anchoredPosition = Vector2.Lerp(botOpenPos, botClosed, u);
			Color color = blinkBack.color;
			color.a = u;
			blinkBack.color = color;
			yield return null;
		}

		PlayAt(currentIndex + 1);

		//Open
		t = 0f;
		while (t < half) {
			t += Time.deltaTime;
			float u = Mathf.SmoothStep(0f, 1f, t / half);
			blinkTop.anchoredPosition = Vector2.Lerp(topClosed, topOpenPos, u);
			blinkBottom.anchoredPosition = Vector2.Lerp(botClosed, botOpenPos, u);
			Color col = blinkBack.color;
			col.a = 1f - u;
			blinkBack.color = col;
			yield return null;
		}
		blinkTop.anchoredPosition = topOpenPos;
		blinkBottom.anchoredPosition = botOpenPos;
		Color c = blinkBack.color;
		c.a = 0f;
		blinkBack.color = c;

		bBlinking = false;
	}

	//Runtime (Game view) visualization — edit hotspots in the inspector while playing and watch them update live.
	//Stop play and re-author the final values back into the prefab.
	void OnGUI() {
		#if !UNITY_EDITOR
		return;
		#endif
		if (configs == null || canvasRect == null) return;

		Vector3[] corners = new Vector3[4];
		canvasRect.GetWorldCorners(corners);
		Canvas canvas = canvasRect.GetComponent<Canvas>();
		Camera worldCam = (canvas != null) ? canvas.worldCamera : null;

		//Canvas BL + local right/up axes in SCREEN space (handles canvas rotation correctly)
		Vector2 blS = WorldToGuiScreen(corners[0], canvas, worldCam);
		Vector2 brS = WorldToGuiScreen(corners[3], canvas, worldCam);
		Vector2 tlS = WorldToGuiScreen(corners[1], canvas, worldCam);
		Vector2 rightS = brS - blS;
		Vector2 upS    = tlS - blS;

		for (int i = 0; i < configs.Length; i++) {
			StateConfig c = configs[i];
			if (c == null) continue;
			Rect h = c.hotspot;
			//Four hotspot corners in screen-GUI coords
			Vector2 p0 = blS + rightS * h.x + upS * h.y;
			Vector2 p1 = blS + rightS * (h.x + h.width) + upS * h.y;
			Vector2 p2 = blS + rightS * (h.x + h.width) + upS * (h.y + h.height);
			Vector2 p3 = blS + rightS * h.x + upS * (h.y + h.height);

			Color col = (c == currentConfig) ? Color.yellow : new Color(1f, 1f, 1f, 0.5f);
			DrawGuiLine(p0, p1, col, 2f);
			DrawGuiLine(p1, p2, col, 2f);
			DrawGuiLine(p2, p3, col, 2f);
			DrawGuiLine(p3, p0, col, 2f);

			GUI.color = col;
			GUI.Label(new Rect(p0.x + 4, p0.y - 18, 80, 20), i.ToString());
			GUI.color = Color.white;
		}

		//Cursor UV readout so you can match hotspot values to where the mouse actually lands
		Mouse mouse = Mouse.current;
		if (mouse != null) {
			Vector2 screenPos = mouse.position.ReadValue();
			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out Vector2 localPos);
			Rect rect = canvasRect.rect;
			float uvx = (localPos.x - rect.xMin) / rect.width;
			float uvy = (localPos.y - rect.yMin) / rect.height;
			GUI.color = Color.yellow;
			GUI.Label(new Rect(10, 10, 400, 20), $"cursor UV: ({uvx:F3}, {uvy:F3})");
			GUI.color = Color.white;
		}
	}

	//World-space RectTransform corner → screen-GUI coord (top-left origin, y flipped)
	static Vector2 WorldToGuiScreen(Vector3 world, Canvas canvas, Camera cam) {
		Vector2 sp;
		if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) sp = world;
		else if (cam != null) sp = cam.WorldToScreenPoint(world);
		else sp = Vector2.zero;
		return new Vector2(sp.x, Screen.height - sp.y);
	}

	static void DrawGuiLine(Vector2 a, Vector2 b, Color c, float thickness) {
		Vector2 d = b - a;
		float len = d.magnitude;
		if (len < 0.5f) return;
		float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
		Matrix4x4 prevMatrix = GUI.matrix;
		Color prevColor = GUI.color;
		GUI.color = c;
		GUIUtility.RotateAroundPivot(angle, a);
		GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), Texture2D.whiteTexture);
		GUI.matrix = prevMatrix;
		GUI.color = prevColor;
	}

	void OnDrawGizmos() {
		if (configs == null) return;

		//Prefer canvasRect (so the visualization matches the actual hit-test area).
		//Fallback to a world-space rect when the canvas is Overlay (its world corners are screen-pixel coords, not useful here).
		Vector3 bl, right, up;
		Canvas canvas = canvasRect != null ? canvasRect.GetComponent<Canvas>() : null;
		bool bUseCanvas = canvasRect != null && (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay);

		if (bUseCanvas) {
			Vector3[] corners = new Vector3[4];
			canvasRect.GetWorldCorners(corners);
			bl = corners[0];
			right = corners[3] - corners[0];
			up = corners[1] - corners[0];
		} else {
			Vector3 origin = transform.position;
			bl = origin - new Vector3(2f, 1.125f, 0f);
			right = new Vector3(4f, 0f, 0f);
			up = new Vector3(0f, 2.25f, 0f);
			Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
			Gizmos.DrawLine(bl, bl + right);
			Gizmos.DrawLine(bl + right, bl + right + up);
			Gizmos.DrawLine(bl + right + up, bl + up);
			Gizmos.DrawLine(bl + up, bl);
		}

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
