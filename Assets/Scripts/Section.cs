using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
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
public struct CutsceneKeyframe {
	public float time;
	public Vector2 cameraPos;
	public float orthoSize;
	public AnimationCurve easeIn;
	//public UnityEvent onReached;
}

[Serializable]
public abstract class Subsection {
	public string name;
}

[Serializable]
public class CutsceneSubsection : Subsection {
	public List<CutsceneKeyframe> keyframes = new();
	public bool waitForInputAtEnd = false;

	public virtual void OnStart() {}
	public virtual void OnKeyframeReached(int index) {}
	public virtual void OnTick(float t) {}
	public virtual void OnEnd() {}
}

[Serializable]
public class GameplaySubsection : Subsection {
}

[Serializable]
public class IntroCutscene1Subsection : CutsceneSubsection {
	public string animationTrigger = "play";

	public override void OnStart() {
		GameObject.Find("SinkAnim").TryGetComponent<Animator>(out Animator animator);
		animator.SetTrigger(animationTrigger);
	}
}

[Serializable]
public class IntroPanSubsection : CutsceneSubsection {
	public float length = 1f;
	public AnimationCurve speedToStrengthCurve = AnimationCurve.Constant(0f, 1f, 0.15f);
	public AnimationCurve streakLengthCurve = AnimationCurve.Constant(0f, 1f, 0.25f);

	StreakBlurDriver driver;

	public override void OnStart() {
		driver = UnityEngine.Object.FindAnyObjectByType<StreakBlurDriver>();
		if (driver != null) driver.enabled = true;
	}

	public override void OnTick(float t) {
		if (driver == null) return;
		float u = length > 0f ? Mathf.Clamp01(t / length) : 0f;
		driver.speedToStrength = speedToStrengthCurve.Evaluate(u);
		driver.streakLength = streakLengthCurve.Evaluate(u);
	}

	public override void OnEnd() {
		if (driver != null) driver.enabled = false;
	}
}

[Serializable]
public class IntroFlowerSubsection : CutsceneSubsection
{
	public string animationTrigger = "play";

	public override void OnStart() {
		GameObject.Find("SinkAnim").TryGetComponent<Animator>(out Animator animator);
		animator.SetTrigger(animationTrigger);
	}

	public override void OnKeyframeReached(int index) {
		if (index == 10) {
			//play sound
		}
	}
}

public class SectionRunner : MonoBehaviour {
	Camera cam;
	CameraFollow2D follow;
	SectionAsset currentAsset;
	int subsectionIndex = -1;
	Action onSectionComplete;

	//Cutscene playback state
	CutsceneSubsection currentCutscene;
	float t;
	int nextEventIndex;
	bool bPlayingCutscene;

	//Wait-for-input state
	bool bWaitingForInput;

	public void Init(Camera cam, CameraFollow2D follow) {
		this.cam = cam;
		this.follow = follow;
	}

	public void PlaySection(SectionAsset asset, int startSubsection = 0, Action onComplete = null) {
		currentAsset = asset;
		onSectionComplete = onComplete;
		subsectionIndex = startSubsection - 1;
		Advance();
	}

	//Call to end the current subsection and move to the next (e.g., from a gameplay end trigger)
	public void Advance() {
		if (bPlayingCutscene) StopCutsceneInternal();
		bWaitingForInput = false;

		subsectionIndex++;
		if (currentAsset == null || subsectionIndex >= currentAsset.subsections.Count) {
			var cb = onSectionComplete;
			onSectionComplete = null;
			currentAsset = null;
			subsectionIndex = -1;
			cb?.Invoke();
			return;
		}

		switch (currentAsset.subsections[subsectionIndex]) {
			case CutsceneSubsection c: StartCutscene(c); break;
			case GameplaySubsection g: StartGameplay(g); break;
		}
	}

	void StartCutscene(CutsceneSubsection c) {
		if (c.keyframes.Count == 0) { Advance(); return; }
		currentCutscene = c;
		t = 0f;
		nextEventIndex = 0;
		bPlayingCutscene = true;
		GameManager.Instance.bInputEnabled = false;
		if (follow != null) follow.enabled = false;
		c.OnStart();
	}

	void StopCutsceneInternal() {
		bPlayingCutscene = false;
		if (follow != null) follow.enabled = true;
		GameManager.Instance.bInputEnabled = true;
		currentCutscene?.OnEnd();
		currentCutscene = null;
	}

	void StartGameplay(GameplaySubsection g) {
		GameManager.Instance.bInputEnabled = true;
		//Gameplay advances via external SectionRunner.Advance() call
	}

	void Update() {
		if (bWaitingForInput && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) {
			Advance();
		}
	}

	void LateUpdate() {
		if (!bPlayingCutscene || bWaitingForInput) return;
		var kfs = currentCutscene.keyframes;
		if (kfs.Count == 0) return;

		t += Time.deltaTime;
		currentCutscene.OnTick(t);

		while (nextEventIndex < kfs.Count && t >= kfs[nextEventIndex].time) {
			currentCutscene.OnKeyframeReached(nextEventIndex);
			nextEventIndex++;
		}

		//Past the last keyframe's time: apply final state and advance (or hold)
		if (t >= kfs[^1].time) {
			var last = kfs[^1];
			Apply(last.cameraPos, last.orthoSize);
			if (currentCutscene.waitForInputAtEnd) bWaitingForInput = true;
			else Advance();
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

		Vector2 pos = Vector2.Lerp(a.cameraPos, b.cameraPos, u);
		float ortho = Mathf.Lerp(a.orthoSize, b.orthoSize, u);
		Apply(pos, ortho);
	}

	void Apply(Vector2 pos, float ortho) {
		var g = Globals.Instance;
		cam.transform.position = new Vector3(pos.x, pos.y, g.cameraZOffset);
		cam.orthographicSize = ortho;
	}
}
