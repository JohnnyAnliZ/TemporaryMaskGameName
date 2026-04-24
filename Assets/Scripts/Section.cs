using UnityEngine;
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

	public virtual void OnKeyframeReached(int index) { }
	public virtual void OnTick(float t) { }
}

[Serializable]
public class IntroIdle : CutsceneSubsection {
	public override void OnStart() {
		GameObject.Find("SinkAnim").GetComponent<SpriteRenderer>().enabled = true;
	}
}
[Serializable]
public class IntroCutscene1Subsection : CutsceneSubsection {
	public override void OnStart() {
		GameObject.Find("SinkAnim").GetComponent<Animator>().Play("Sink", 0, 0f);
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
		GameObject.Find("SinkAnim").SetActive(false);

		AudioManager.Instance.StartMusic();
	}
}
//Gameplay---------------------------------------------------------------
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
	}
}
[Serializable]
public class Gameplay3DBreakSubsection : GameplaySubsection {
	public override void OnStart() {
		base.OnStart();
		CompositeManager.Instance.maskDrawer.Do_ShatterAll();
	}
}

public class SectionRunner : MonoBehaviour {
	Camera cam;
	CameraFollow2D follow;
	SectionAsset currentAsset;
	int subsectionIndex = -1;
	Action onSectionComplete;

	Subsection currentSubsection;

	//Cutscene playback state
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

		//Catch up scene state from any subsections we're skipping
		for (int i = 0; i < startSubsection && i < asset.subsections.Count; i++) {
			asset.subsections[i].FastForwardToEnd();
		}

		subsectionIndex = startSubsection - 1;
		Advance();
	}

	//Call to end the current subsection and move to the next (e.g., from a gameplay end trigger)
	public void Advance() {
		//End current subsection
		if (bPlayingCutscene) {
			bPlayingCutscene = false;
			if (follow != null) follow.enabled = true;
			GameManager.Instance.bInputEnabled = true;
		}
		if (currentSubsection != null) {
			currentSubsection.OnEnd();
			currentSubsection = null;
		}
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
		currentSubsection = c;
		t = 0f;
		nextEventIndex = 0;
		bPlayingCutscene = true;
		GameManager.Instance.bInputEnabled = false;
		if (follow != null) follow.enabled = false;
		c.OnStart();
	}

	void StartGameplay(GameplaySubsection g) {
		SectionStart marker = null;
		foreach (SectionStart s in FindObjectsByType<SectionStart>(FindObjectsSortMode.None)) {
			if (s.section == Section.Gameplay && s.gameplayStart == g.start) {
				marker = s;
				break;
			}
		}
		GameManager.Instance.TeleportPlayer(marker.transform.position);
		currentSubsection = g;
		g.OnStart();
		GameManager.Instance.bInputEnabled = true;
	}

	void Update() {
		if (!bWaitingForInput) return;
		bool keyPressed = Keyboard.current.anyKey.wasPressedThisFrame;
		bool mousePressed = Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame;
		if (keyPressed || mousePressed) Advance();
	}

	void LateUpdate() {
		if (!bPlayingCutscene || bWaitingForInput) return;
		CutsceneSubsection cutscene = (CutsceneSubsection)currentSubsection;
		var kfs = cutscene.keyframes;
		if (kfs.Count == 0) return;

		t += Time.deltaTime;
		cutscene.OnTick(t);

		while (nextEventIndex < kfs.Count && t >= kfs[nextEventIndex].time) {
			cutscene.OnKeyframeReached(nextEventIndex);
			nextEventIndex++;
		}

		//Past the last keyframe's time: apply final state and advance (or hold)
		if (t >= kfs[^1].time) {
			var last = kfs[^1];
			Apply(last.cameraPos, last.orthoSize);
			if (cutscene.waitForInputAtEnd) bWaitingForInput = true;
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
