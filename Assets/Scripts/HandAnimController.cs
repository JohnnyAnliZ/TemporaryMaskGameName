using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.Serialization;

[Serializable]
public struct Trans3DKeyframe {
	public float frame; //relative to startFrame
	public Vector3 handLocalPos;
	public Vector3 cameraPos;
	public Vector3 cameraEuler;
	public AnimationCurve easeIn;
}

[Serializable]
public class Trans3DPart {
	public int startFrame = 0;
	public int endFrame = 189;
	public float speed = 1f;
	public List<Trans3DKeyframe> keyframes = new();
}

[RequireComponent(typeof(Animator))]
public class HandAnimController : MonoBehaviour
{
	public AnimationClip animationClip;

	public Sprite cursorNormalSprite;
	public Sprite cursorHoverSprite;
	public float cursorNormalScale = 1f;
	public float cursorHoverScale = 1.5f;
	public Color cursorNormalColor = Color.white;
	public Color cursorHoverColor = new(1f, 0.3f, 0.3f, 1f);
	public float cursorTransitionSpeed = 12f;

	public Renderer mirrorRenderer;
	[FormerlySerializedAs("mirrorNormalMaterialSlot1")] public Material mirrorNormalMaterialSlot0;
	[FormerlySerializedAs("mirrorHoverMaterialSlot1")] public Material mirrorHoverMaterialSlot0;
	public Material mirrorNormalMaterialSlot2;
	public Material mirrorHoverMaterialSlot2;

	Animator animator;
	PlayableGraph graph;
	AnimationClipPlayable clipPlayable;
	Coroutine sequence;

	bool bWaitingForHotspot;
	bool cursorHovering;
	float cursorScale;
	Color cursorColor;

	Animator anim => animator != null ? animator : (animator = GetComponent<Animator>());

	public void PlayRubFaceSequence(Trans3DPart part1, Trans3DPart part2, Rect resumeHotspot, Action onComplete, bool debugSkipToPause = false) {
		if (sequence != null) StopCoroutine(sequence);
		sequence = StartCoroutine(RunSequence(part1, part2, resumeHotspot, onComplete, debugSkipToPause));
	}

	IEnumerator RunSequence(Trans3DPart part1, Trans3DPart part2, Rect resumeHotspot, Action onComplete, bool debugSkipToPause) {
		Transform player = GameManager.Instance.player3D.transform;
		CharacterController controller = player.GetComponent<CharacterController>();
		FirstPersonLook look = FindAnyObjectByType<FirstPersonLook>();
		float eyeOffset = Globals.Instance.eyeOffset;

		controller.enabled = false;

		if (debugSkipToPause) SnapToEnd(part1, player, look, eyeOffset);
		else yield return RunPart(part1, player, look, eyeOffset);

		yield return WaitForHotspotClick(resumeHotspot);
		yield return RunPart(part2, player, look, eyeOffset);

		controller.enabled = true;
		sequence = null;
		onComplete?.Invoke();
	}

	void SnapToEnd(Trans3DPart part, Transform player, FirstPersonLook look, float eyeOffset) {
		if (graph.IsValid()) graph.Destroy();
		clipPlayable = AnimationPlayableUtilities.PlayClip(anim, animationClip, out graph);
		clipPlayable.SetTime(part.endFrame / animationClip.frameRate);
		graph.Stop();

		TickKeyframes(part.keyframes, part.endFrame - part.startFrame, player, look, eyeOffset);
	}

	IEnumerator RunPart(Trans3DPart part, Transform player, FirstPersonLook look, float eyeOffset) {
		if (graph.IsValid()) graph.Destroy();
		clipPlayable = AnimationPlayableUtilities.PlayClip(anim, animationClip, out graph);
		clipPlayable.SetTime(part.startFrame / animationClip.frameRate);
		clipPlayable.SetSpeed(part.speed);

		while (true) {
			float currentFrame = (float)clipPlayable.GetTime() * animationClip.frameRate;
			TickKeyframes(part.keyframes, currentFrame - part.startFrame, player, look, eyeOffset);

			if (currentFrame >= part.endFrame) break;
			yield return null;
		}

		graph.Stop();
	}
	void TickKeyframes(List<Trans3DKeyframe> keyframes, float partFrame, Transform player, FirstPersonLook look, float eyeOffset) {
		if (keyframes == null || keyframes.Count == 0) return;

		Trans3DKeyframe a, b;
		float u;
		if (partFrame <= keyframes[0].frame) {
			a = b = keyframes[0];
			u = 0f;
		} else if (partFrame >= keyframes[^1].frame) {
			a = b = keyframes[^1];
			u = 0f;
		} else {
			int idx = 0;
			for (int i = keyframes.Count - 2; i >= 0; i--) {
				if (partFrame >= keyframes[i].frame) { idx = i; break; }
			}
			a = keyframes[idx];
			b = keyframes[idx + 1];
			float span = Mathf.Max(b.frame - a.frame, 0.0001f);
			u = Mathf.Clamp01((partFrame - a.frame) / span);
			if (b.easeIn != null && b.easeIn.length > 0) u = b.easeIn.Evaluate(u);
		}

		transform.localPosition = Vector3.Lerp(a.handLocalPos, b.handLocalPos, u);
		Vector3 cameraPos = Vector3.Lerp(a.cameraPos, b.cameraPos, u);
		Quaternion cameraRot = Quaternion.Slerp(Quaternion.Euler(a.cameraEuler), Quaternion.Euler(b.cameraEuler), u);

		player.position = cameraPos - Vector3.up * eyeOffset;
		look.SetLook(cameraRot);
	}

	IEnumerator WaitForHotspotClick(Rect resumeHotspot) {
		Mouse mouse = Mouse.current;

		Cursor.lockState = CursorLockMode.Confined;
		yield return null; //need to wait for mouse cursor stuff...
		Cursors.WarpTo(Cursors.pauseStartUV);
		yield return null;

		bWaitingForHotspot = true;
		cursorScale = cursorNormalScale;
		cursorColor = cursorNormalColor;
		SetMirrorMaterial(false);

		while (true) {
			bool bHover = false;
			if (mouse != null) {
				Vector2 uv = Viewport.ToUV(mouse.position.ReadValue());
				bHover = resumeHotspot.Contains(uv);
			}

			cursorHovering = bHover;
			float k = 1f - Mathf.Exp(-cursorTransitionSpeed * Time.deltaTime);
			cursorScale = Mathf.Lerp(cursorScale, bHover ? cursorHoverScale : cursorNormalScale, k);
			cursorColor = Color.Lerp(cursorColor, bHover ? cursorHoverColor : cursorNormalColor, k);
			SetMirrorMaterial(bHover);

			if (bHover && mouse != null && mouse.leftButton.wasPressedThisFrame) break;
			yield return null;
		}

		bWaitingForHotspot = false;
		Cursor.lockState = CursorLockMode.Locked;
	}

	void SetMirrorMaterial(bool bHover) {
		Material[] mats = mirrorRenderer.sharedMaterials;
		bool changed = false;
		changed |= TrySetSlot(mats, 0, bHover ? mirrorHoverMaterialSlot0 : mirrorNormalMaterialSlot0);
		changed |= TrySetSlot(mats, 2, bHover ? mirrorHoverMaterialSlot2 : mirrorNormalMaterialSlot2);
		if (changed) mirrorRenderer.sharedMaterials = mats;
	}
	static bool TrySetSlot(Material[] mats, int slot, Material target) {
		if (target == null || slot >= mats.Length) return false;
		mats[slot] = target;
		return true;
	}

	//Cursor
	void OnGUI() {
		if (!bWaitingForHotspot) return;
		Sprite sprite = (cursorHovering && cursorHoverSprite != null) ? cursorHoverSprite : cursorNormalSprite;
		if (sprite == null) return;

		Mouse mouse = Mouse.current;
		if (mouse == null) return;
		Vector2 mousePos = mouse.position.ReadValue();
		float guiY = Screen.height - mousePos.y;

		Rect texRect = sprite.textureRect;
		Texture2D tex = sprite.texture;
		float w = texRect.width * cursorScale;
		float h = texRect.height * cursorScale;

		Rect drawRect = new(mousePos.x - w * 0.5f, guiY - h * 0.5f, w, h);
		Rect uvRect = new(texRect.x / tex.width, texRect.y / tex.height, texRect.width / tex.width, texRect.height / tex.height);

		Color prevColor = GUI.color;
		GUI.color = cursorColor;
		GUI.DrawTextureWithTexCoords(drawRect, tex, uvRect);
		GUI.color = prevColor;
	}

	void OnDestroy() {
		if (sequence != null) StopCoroutine(sequence);
		if (graph.IsValid()) graph.Destroy();
	}
}