using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;

public class Wash2DOverlay : MonoBehaviour
{
	public static Wash2DOverlay Instance { get; private set; }

	public Transform mirrorHole;
	public float startOrthoSize = 5f;
	public float endOrthoSize = 0.25f;

	[Header("Wash Animation")]
	public Animator washAnimator;
	public AnimationClip animationClip;

	[Header("Hotspot Cursor")]
	public Sprite cursorNormalSprite;
	public Sprite cursorHoverSprite;
	public float cursorNormalScale = 1f;
	public float cursorHoverScale = 1.5f;
	public Color cursorNormalColor = Color.white;
	public Color cursorHoverColor = new(1f, 0.3f, 0.3f, 1f);
	public float cursorTransitionSpeed = 12f;
	public GameObject hoverObject;

	bool bWaitingForHotspot;
	bool bCursorHovering;
	float cursorScale;
	Color cursorColor;

	Camera washCam;
	PlayableGraph graph;
	AnimationClipPlayable clipPlayable;

	void Awake() {
		Instance = this;
	}
	void OnDestroy() {
		if (Instance == this) Instance = null;
		if (graph.IsValid()) graph.Destroy();
	}

	//Overlay-------------------------------------------------------------------------------
	public void Show() {
		if (washCam == null && !Build()) return;

		Vector3 focus = mirrorHole != null ? mirrorHole.position : transform.position;
		washCam.transform.position = new Vector3(focus.x, focus.y, focus.z - 10f);
		washCam.orthographicSize = startOrthoSize;
		washCam.aspect = CompositeManager.Instance.aspectLocker.targetAspect;
		washCam.enabled = true;
	}
	public void Hide() {
		washCam.enabled = false;
	}

	public void SetZoom(float u) {
		washCam.orthographicSize = Mathf.Lerp(startOrthoSize, endOrthoSize, Mathf.Clamp01(u));
	}

	bool Build() {
		CompositeManager composite = CompositeManager.Instance;

		GameObject go = new GameObject("Wash2DCamera");
		washCam = go.AddComponent<Camera>();
		washCam.orthographic = true;
		washCam.clearFlags = CameraClearFlags.Nothing;
		washCam.cullingMask = 1 << gameObject.layer;
		washCam.allowHDR = false;
		washCam.allowMSAA = false;
		washCam.useOcclusionCulling = false;
		washCam.enabled = false;

		UniversalAdditionalCameraData data = washCam.GetUniversalAdditionalCameraData();
		data.renderType = CameraRenderType.Overlay; //composited after the base camera
		data.SetRenderer(0);
		data.requiresDepthTexture = false;

		UniversalAdditionalCameraData outputData = composite.outputCam.GetUniversalAdditionalCameraData();
		if (!outputData.cameraStack.Contains(washCam)) outputData.cameraStack.Add(washCam);

		int washMask = 1 << gameObject.layer;
		composite.cameraA.cullingMask &= ~washMask;
		composite.cameraB.cullingMask &= ~washMask;

		return true;
	}

	public IEnumerator WashSequence(float speed, float pauseDelay, Rect resumeHotspot) {
		yield return PlayClip(speed);
		yield return new WaitForSeconds(pauseDelay);
		yield return WaitForHotspotClick(resumeHotspot);
	}

	IEnumerator PlayClip(float speed) {
		if (graph.IsValid()) graph.Destroy();
		clipPlayable = AnimationPlayableUtilities.PlayClip(washAnimator, animationClip, out graph);
		clipPlayable.SetTime(0d);
		clipPlayable.SetSpeed(Mathf.Max(speed, 0.01f));

		while (clipPlayable.GetTime() < animationClip.length) yield return null;

		graph.Stop();
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

		while (true) {
			bool bHover = false;
			Vector2 uv = Viewport.ToUV(mouse.position.ReadValue());
			bHover = resumeHotspot.Contains(uv);

			bCursorHovering = bHover;
			hoverObject.SetActive(bHover);
			float k = 1f - Mathf.Exp(-cursorTransitionSpeed * Time.deltaTime);
			cursorScale = Mathf.Lerp(cursorScale, bHover ? cursorHoverScale : cursorNormalScale, k);
			cursorColor = Color.Lerp(cursorColor, bHover ? cursorHoverColor : cursorNormalColor, k);

			if (bHover && mouse.leftButton.wasPressedThisFrame) break;
			yield return null;
		}

		hoverObject.SetActive(false);
		bWaitingForHotspot = false;
		Cursor.lockState = CursorLockMode.Locked;
	}

	void OnGUI() {
		if (!bWaitingForHotspot) return;

		Sprite sprite = (bCursorHovering && cursorHoverSprite != null) ? cursorHoverSprite : cursorNormalSprite;
		Vector2 mousePos = Mouse.current.position.ReadValue();
		float guiY = Screen.height - mousePos.y;

		Rect texRect = sprite.textureRect;
		Texture2D tex = sprite.texture;
		float uiScale = CompositeManager.Instance.outputCam.rect.height * Screen.height / 1080;
		float w = texRect.width * cursorScale * uiScale;
		float h = texRect.height * cursorScale * uiScale;

		Rect drawRect = new(mousePos.x - w * 0.5f, guiY - h * 0.5f, w, h);
		Rect uvRect = new(texRect.x / tex.width, texRect.y / tex.height, texRect.width / tex.width, texRect.height / tex.height);

		Color prevColor = GUI.color;
		GUI.color = cursorColor;
		GUI.DrawTextureWithTexCoords(drawRect, tex, uvRect);
		GUI.color = prevColor;
	}
}
