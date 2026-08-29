using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System;

public enum Section {
	Intro		= 0,
	Gameplay	= 1,
	LiveAction	= 2,
	Trans3D		= 3,
	Trans2D		= 4,
}

[Serializable]
public abstract class Subsection {
	public string name;
	public virtual void OnStart() {}
	public virtual void OnEnd() {}
	public virtual void FastForwardToEnd() { OnStart(); OnEnd(); }
}

//Intro---------------------------------------------
[Serializable]
public struct CutsceneKeyframe {
	public float time;
	public Vector2 cameraPos;
	public float orthoSize;
	public AnimationCurve easeIn;
}
[Serializable]
public class CutsceneSubsection : Subsection {
	public List<CutsceneKeyframe> keyframes = new();
	public bool waitForInputAtEnd = false;
	public bool endAtGameplayPose = false;

	public virtual void OnKeyframeReached(int index) { }
	public virtual void OnTick(float t) { }
}

[Serializable]
public class IntroIdle : CutsceneSubsection {
	public override void OnStart() {
		CompositeManager.Instance.maskDrawer.ResetMask();
		GameManager.Instance.player2D.SetActive(false);
		SectionStart introMarker = SectionStart.Find(Section.Intro);
		GameManager.Instance.player3D.GetComponent<Player3DController>().Teleport(introMarker.transform.position);

		FirstPersonLook look = UnityEngine.Object.FindAnyObjectByType<FirstPersonLook>();
		look.SetLook(introMarker.transform.rotation);
		GameObject.Find("SinkAnim").GetComponent<SpriteRenderer>().enabled = true;

		//Reset state
		foreach (Platform p in UnityEngine.Object.FindObjectsByType<Platform>(FindObjectsSortMode.None)) {
			p.bIsBroken = false;
			p.bHasShrunk = false;
		}
		UnityEngine.Object.FindAnyObjectByType<ShrinkTrigger>().bEntered = false;
		AudioManager.Instance.reset();
	}
}
[Serializable]
public class IntroCutscene1Subsection : CutsceneSubsection {
	public override void OnStart() {
		GameObject.Find("SinkAnim").GetComponent<Animator>().Play("Sink", 0, 0f);
		AudioManager.Instance.PlayIntroSink();
	}
	public override void OnEnd() {
		GameObject.Find("SinkAnim").GetComponent<Animator>().Play("Idle", 0, 0f);
	}
}
[Serializable]
public class IntroPanSubsection : CutsceneSubsection {
	public float length = 1f;
	public AnimationCurve speedToStrengthCurve = AnimationCurve.Constant(0f, 1f, 0f);
	public AnimationCurve streakLengthCurve = AnimationCurve.Constant(0f, 1f, 0f);

	StreakBlurDriver driver;

	public override void OnStart() {
		driver = UnityEngine.Object.FindAnyObjectByType<StreakBlurDriver>();
		driver.enabled = true;
	}
	public override void OnEnd() {
		driver.enabled = false;
		GameManager.Instance.player2D.SetActive(true);
	}
	public override void OnTick(float t) {
		float u = length > 0f ? Mathf.Clamp01(t / length) : 0f;
		driver.speedToStrength = speedToStrengthCurve.Evaluate(u);
		driver.streakLength = streakLengthCurve.Evaluate(u);
	}
}
[Serializable]
public class IntroFlowerSubsection : CutsceneSubsection {
	public override void OnStart() {
		GameObject.Find("SinkAnim").GetComponent<SpriteRenderer>().enabled = false;
		AudioManager.Instance.HandleSubsection("IntroFlowerSubsection");
	}
}

//Gameplay-----------------------------------------------------------------------
public enum GameplayStart {
	TwoD,
	TwoDBreak,
	ThreeD,
	ThreeDBreak,
}
[Serializable]
public class GameplaySubsection : Subsection {
	public GameplayStart start;

	public override void OnStart() {
		GameManager.Instance.player2D.SetActive(true);
		if (start == GameplayStart.ThreeD || start == GameplayStart.ThreeDBreak) {
			CompositeManager.Instance.maskDrawer.ResetMask3D();
		} else CompositeManager.Instance.maskDrawer.ResetMask();
		AudioManager.Instance.HandleSubsection($"{start}");
	}
}

//Live Action---------------------------------------------------------------------------
[Serializable]
public class LiveActionSubsection : Subsection {
	public float fadeInFactor = 1f;
	public int startIndex = 0;

	public override void OnStart() {
		CompositeManager.Instance.maskDrawer.SetCamerasSuspended(true);
		VideoManager.Instance.FadeIn(fadeInFactor, startIndex);
		AudioManager.Instance.HandleSubsection("LiveActionSubsection");
	}

	public override void OnEnd() {
		CompositeManager.Instance.maskDrawer.SetCamerasSuspended(false);
	}
}

//Transitions---------------------------------------------------------------------------
[Serializable]
public class Trans3DSubsection : Subsection {
	static readonly int DitherSuppressId = Shader.PropertyToID("_DitherSuppress");

	public Trans3DPart part1 = new();
	public Trans3DPart part2 = new();
	public Rect resumeHotspot = new(0.4f, 0.4f, 0.2f, 0.2f);
	public bool debugSkipToPause = false;
	[Range(0f, 1f)] public float stripWidth = 0.45f;

	public float wobbleMin = 0.2f;
	public float wobbleMax = 1f;
	public float wobbleSpeed = 1f;
	public float caMultiplier = 5f;

	float prevWobbleMin, prevWobbleMax, prevWobbleSpeed, prevCAMultiplier;

	public override void OnStart() {
		CompositeManager.Instance.maskDrawer.ResetMask3D();
		CompositeManager.Instance.maskDrawer.SetStripWidth(stripWidth);

		prevWobbleMin = CompositeManager.Instance.wobbleMin;
		prevWobbleMax = CompositeManager.Instance.wobbleMax;
		prevWobbleSpeed = CompositeManager.Instance.wobbleSpeed;
		prevCAMultiplier = CompositeManager.Instance.caMultiplier;
		CompositeManager.Instance.wobbleMin = wobbleMin;
		CompositeManager.Instance.wobbleMax = wobbleMax;
		CompositeManager.Instance.wobbleSpeed = wobbleSpeed;
		CompositeManager.Instance.caMultiplier = caMultiplier;

		Shader.SetGlobalFloat(DitherSuppressId, 1f);

		AudioManager.Instance.HandleTransBackTo3D();

		GameObject.Find("hand").GetComponent<HandAnimController>()
			.PlayRubFaceSequence(part1, part2, resumeHotspot, () => GameManager.Instance.runner.CompleteSection(), debugSkipToPause);
	}

	public override void OnEnd() {
		CompositeManager.Instance.wobbleMin = prevWobbleMin;
		CompositeManager.Instance.wobbleMax = prevWobbleMax;
		CompositeManager.Instance.wobbleSpeed = prevWobbleSpeed;
		CompositeManager.Instance.caMultiplier = prevCAMultiplier;
		Shader.SetGlobalFloat(DitherSuppressId, 0f);
	}
}
[Serializable]
public class Trans2DSubsection : Subsection {
	public float speed = 1f;
	public float pauseDelay = 0.5f;
	public Rect resumeHotspot = new(0.4f, 0.4f, 0.2f, 0.2f);
	[Range(0f, 1f)] public float stripWidth = 0.6f;

	public float zoomDuration = 3f;
	public AnimationCurve zoomEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	public Vector2 camPosFrom = new(-14.2f, 6.2f);
	public float camOrthoFrom = 1.5f;

	public override void OnStart() {
		MaskDrawer mask = CompositeManager.Instance.maskDrawer;
		mask.ResetMask();
		mask.SetStripWidth(stripWidth);

		GameManager.Instance.player2D.SetActive(false);
		GameObject.Find("SinkAnim").GetComponent<SpriteRenderer>().enabled = true;

		Wash2DOverlay.Instance.Show();
	}
}

public class SectionRunner : MonoBehaviour {
	CutscenePlayer cutscenePlayer;
	SectionAsset currentAsset;
	Subsection currentSubsection;
	int subsectionIndex = -1;

	public void Init(CutscenePlayer cutscenePlayer) {
		this.cutscenePlayer = cutscenePlayer;
	}

	public void PlaySection(Section section, int startSubsection = 0) {
		currentAsset = Resources.Load<SectionAsset>($"Sections/Section_{section}");

		//Catch up scene state from any subsections we're skipping
		for (int i = 0; i < startSubsection && i < currentAsset.subsections.Count; i++) {
			currentAsset.subsections[i].FastForwardToEnd();
		}

		//An empty section (e.g. a not-yet-authored Trans section) rolls straight through Advance to the next.
		subsectionIndex = startSubsection - 1;
		Advance();
	}

	public void Advance() {
		//End current subsection
		if (currentSubsection != null) {
			currentSubsection.OnEnd();
			currentSubsection = null;
		}

		subsectionIndex++;
		if (subsectionIndex >= currentAsset.subsections.Count) {
			Section[] all = (Section[])System.Enum.GetValues(typeof(Section));
			Section next = all[(System.Array.IndexOf(all, currentAsset.section) + 1) % all.Length];
			currentAsset = null;
			subsectionIndex = -1;
			PlaySection(next);
			return;
		}

		switch (currentAsset.subsections[subsectionIndex]) {
			case CutsceneSubsection c: StartCutscene(c); break;
			case GameplaySubsection g: StartGameplay(g); break;
			case LiveActionSubsection la: StartLiveAction(la); break;
			case Trans3DSubsection t: StartTrans3D(t); break;
			case Trans2DSubsection t2: StartTrans2D(t2); break;
		}
	}

	public void CompleteSection() {
		if (currentAsset == null) return;
		subsectionIndex = currentAsset.subsections.Count - 1; //for the if block in Advance()
		Advance();
	}

	public void StartLiveAction(LiveActionSubsection la) {
		currentSubsection = la;
		GameManager.Instance.bInputEnabled = false;
		la.OnStart();
	}

	void StartTrans3D(Trans3DSubsection t) {
		currentSubsection = t;
		GameManager.Instance.bInputEnabled = false;
		t.OnStart();
	}

	void StartTrans2D(Trans2DSubsection t) {
		currentSubsection = t;
		GameManager.Instance.bInputEnabled = false;

		cutscenePlayer.Park(t.camPosFrom, t.camOrthoFrom);
		t.OnStart();
		StartCoroutine(RunTrans2D(t));
	}

	IEnumerator RunTrans2D(Trans2DSubsection t) {
		Wash2DOverlay wash = Wash2DOverlay.Instance;
		MaskDrawer mask = CompositeManager.Instance.maskDrawer;
		Camera cam2D = CompositeManager.Instance.cameraA;

		yield return wash.WashSequence(t.speed, t.pauseDelay, t.resumeHotspot);

		Vector2 startPos = cam2D.transform.position;
		float startOrtho = cam2D.orthographicSize;
		float startWidth = mask.StripWidth;
		float zOffset = Globals.Instance.cameraZOffset;
		SectionAsset intro = Resources.Load<SectionAsset>($"Sections/Section_{Section.Intro}");
		CutsceneKeyframe end = ((CutsceneSubsection)intro.subsections[0]).keyframes[0];

		float time = 0f;
		while (time < t.zoomDuration) {
			time += Time.deltaTime;
			float u = Mathf.Clamp01(time / t.zoomDuration);
			if (t.zoomEase != null && t.zoomEase.length > 0) u = t.zoomEase.Evaluate(u);
			wash.SetZoom(u);
			Vector2 pos = Vector2.Lerp(startPos, end.cameraPos, u);
			cam2D.transform.position = new Vector3(pos.x, pos.y, zOffset);
			cam2D.orthographicSize = Mathf.Lerp(startOrtho, end.orthoSize, u);
			mask.SetStripWidth(Mathf.Lerp(startWidth, 1f, u));
			yield return null;
		}
		wash.SetZoom(1f);
		cam2D.transform.position = new Vector3(end.cameraPos.x, end.cameraPos.y, zOffset);
		cam2D.orthographicSize = end.orthoSize;
		mask.SetStripWidth(1f);

		wash.Hide();
		CompleteSection();
	}

	void StartCutscene(CutsceneSubsection c) {
		if (c.keyframes.Count == 0) { Advance(); return; }
		currentSubsection = c;
		cutscenePlayer.Play(c, Advance);
	}

	void StartGameplay(GameplaySubsection g) {
		Vector3 pos = SectionStart.Find(Section.Gameplay, g.start).transform.position;
		Player3DController player = GameManager.Instance.player3D.GetComponent<Player3DController>();
		player.Teleport(pos);
		player.SetSpawnPoint(pos);

		currentSubsection = g;
		g.OnStart();
		GameManager.Instance.bInputEnabled = true;
	}
}

public class CutscenePlayer : MonoBehaviour {
	static readonly int FlickerSuppressId = Shader.PropertyToID("_FlickerSuppress");

	Camera cam;
	CameraFollow2D follow;

	CutsceneSubsection cutscene;
	Action onComplete;
	float t;
	int nextEventIndex;
	bool bPlaying;
	bool bWaitingForInput;

	public void Init(Camera cam, CameraFollow2D follow) {
		this.cam = cam;
		this.follow = follow;
	}

	//Begin playing a cutscene; onComplete fires once it finishes (or the player presses a key at a wait-for-input end).
	public void Play(CutsceneSubsection c, Action onComplete) {
		cutscene = c;
		this.onComplete = onComplete;
		t = 0f;
		nextEventIndex = 0;
		bWaitingForInput = false;
		bPlaying = true;
		GameManager.Instance.bInputEnabled = false;
		follow.enabled = false;
		Shader.SetGlobalFloat(FlickerSuppressId, 1f);
		c.OnStart();
	}

	//Cutscene finished: restore the follow camera + input, then tell the runner to advance.
	void Finish() {
		bPlaying = false;
		bWaitingForInput = false;
		cutscene = null;
		follow.enabled = true;
		GameManager.Instance.bInputEnabled = true;
		Shader.SetGlobalFloat(FlickerSuppressId, 0f);
		Action cb = onComplete;
		onComplete = null;
		cb?.Invoke();
	}

	void Update() {
		if (!bWaitingForInput) return;
		bool keyPressed = Keyboard.current.anyKey.wasPressedThisFrame;
		bool mousePressed = Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame;
		if (keyPressed || mousePressed) Finish();
	}

	void LateUpdate() {
		if (!bPlaying || bWaitingForInput) return;
		var kfs = cutscene.keyframes;
		if (kfs.Count == 0) return;

		t += Time.deltaTime;
		cutscene.OnTick(t);

		while (nextEventIndex < kfs.Count && t >= kfs[nextEventIndex].time) {
			cutscene.OnKeyframeReached(nextEventIndex);
			nextEventIndex++;
		}

		Vector2 endPos = kfs[^1].cameraPos;
		float endOrtho = kfs[^1].orthoSize;
		if (cutscene.endAtGameplayPose) {
			follow.GetSettlePose(out Vector3 settlePos, out float settleOrtho);
			endPos = new Vector2(settlePos.x, settlePos.y);
			endOrtho = settleOrtho;
		}

		//Past the last keyframe's time: apply final state and finish (or hold for input)
		if (t >= kfs[^1].time) {
			Apply(endPos, endOrtho);
			if (cutscene.waitForInputAtEnd) bWaitingForInput = true;
			else Finish();
			return;
		}

		//Before the first keyframe: hold at kf[0]
		if (t < kfs[0].time) {
			Apply(kfs[0].cameraPos, kfs[0].orthoSize);
			return;
		}

		//Lerp within current segment
		int idx = 0;
		for (int i = kfs.Count - 2; i >= 0; i--) {
			if (t >= kfs[i].time) { idx = i; break; }
		}
		var a = kfs[idx];
		var b = kfs[idx + 1];
		float span = Mathf.Max(b.time - a.time, 0.0001f);
		float u = Mathf.Clamp01((t - a.time) / span);
		if (b.easeIn != null && b.easeIn.length > 0) u = b.easeIn.Evaluate(u);

		bool bIsLastSegment = idx + 1 == kfs.Count - 1;
		Vector2 pos = Vector2.Lerp(a.cameraPos, bIsLastSegment ? endPos : b.cameraPos, u);
		float ortho = Mathf.Lerp(a.orthoSize, bIsLastSegment ? endOrtho : b.orthoSize, u);
		Apply(pos, ortho);
	}

	public void Park(Vector2 pos, float ortho) {
		follow.enabled = false;
		Apply(pos, ortho);
	}

	void Apply(Vector2 pos, float ortho) {
		var g = Globals.Instance;
		cam.transform.position = new Vector3(pos.x, pos.y, g.cameraZOffset);
		cam.orthographicSize = ortho;
	}
}
