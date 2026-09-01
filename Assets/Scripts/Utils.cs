using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class Rays {
	public static bool Cast(Vector3 origin, Vector3 direction, out RaycastHit hit,
		float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers,
		QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal,
		bool visualize = false, float visualizeDuration = 0f) {
		bool didHit = Physics.Raycast(origin, direction, out hit, maxDistance, layerMask, triggerInteraction);
		#if UNITY_EDITOR
		if (visualize) {
			Vector3 dir = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
			float drawLen = float.IsInfinity(maxDistance) ? 1000f : maxDistance;
			float duration = visualizeDuration <= 0f ? 1e9f : visualizeDuration;
			if (didHit) {
				Debug.DrawLine(origin, hit.point, Color.green, duration, false);
			}
			else {
				Debug.DrawRay(origin, dir * drawLen, Color.red, duration, false);
			}
		}
		#endif
		return didHit;
	}
}

public static class Viewport {
	//AspectRatioLocker letterboxes/pillarboxes the output camera whenever the window aspect doesn't match its
	//target, so outputCam.rect covers only a subregion of the window rather than (0,0,1,1). Raw
	//screenPos/Screen.width would misalign a hotspot against what the player actually sees at any other aspect
	//(e.g. a resized build window); remapping through that rect keeps it aligned at any window size.
	public static Vector2 ToUV(Vector2 screenPos) {
		Rect rect = CompositeManager.Instance.outputCam.rect;
		Vector2 norm = new(screenPos.x / Screen.width, screenPos.y / Screen.height);
		if (rect.width <= 0f || rect.height <= 0f) return norm;
		return new Vector2((norm.x - rect.x) / rect.width, (norm.y - rect.y) / rect.height);
	}
	public static Vector2 FromUV(Vector2 uv) {
		Rect rect = CompositeManager.Instance.outputCam.rect;
		return new Vector2(
			(rect.x + uv.x * rect.width) * Screen.width,
			(rect.y + uv.y * rect.height) * Screen.height
		);
	}
}

public static class Cursors {
	public static readonly Vector2 pauseStartUV = new(0.5f, 0.1f);

	//What the section running right now wants the cursor to be. Escape releases to None so the window can be
	//left, and regaining focus or clicking back in restores this rather than assuming Locked -- the live action
	//and Trans sections run a Confined cursor, so a hardcoded relock jammed them to the screen centre.
	public static CursorLockMode desired = CursorLockMode.Locked;

	public static void Set(CursorLockMode mode) {
		desired = mode;
		Apply();
	}

	//Every mode except None draws its own cursor sprite, so the OS one only appears once actually released.
	public static void Apply() {
		Cursor.lockState = desired;
		Cursor.visible = desired == CursorLockMode.None;
	}

	//Warping moves the OS cursor but raises no input event, so Mouse.position keeps handing back its old value
	//InputState.Change writes the control state directly so the next ReadValue() agrees with reality
	public static void WarpTo(Vector2 uv) {
		Mouse mouse = Mouse.current;
		Vector2 screenPos = Viewport.FromUV(uv);
		mouse.WarpCursorPosition(screenPos);
		InputState.Change(mouse.position, screenPos);
	}
}
