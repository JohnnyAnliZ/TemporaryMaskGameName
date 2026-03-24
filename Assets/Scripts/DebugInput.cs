using UnityEngine;
using UnityEngine.InputSystem;

public class DebugInput : MonoBehaviour
{
	public float portalRadius = 100f;
	MaskDrawer maskDrawer;

	#if UNITY_EDITOR
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	static void AutoCreate() {
		var go = new GameObject("DebugInput");
		go.AddComponent<DebugInput>();
	}
	#endif

	void Start() {
		maskDrawer = FindAnyObjectByType<MaskDrawer>();
	}

	void Update() {
		if (maskDrawer == null) return;

		var mouse = Mouse.current;
		var keyboard = Keyboard.current;
		if (mouse == null || keyboard == null) return;

		if (mouse.leftButton.wasPressedThisFrame) {
			Vector2 pos = mouse.position.ReadValue();
			maskDrawer.AddPortal(pos, portalRadius);
		}

		if (keyboard.cKey.wasPressedThisFrame) {
			maskDrawer.ClearPortals();
		}

		if (keyboard.mKey.wasPressedThisFrame) {
			showMask = !showMask;
		}
	}

	bool showMask;

	void OnGUI() {
		if (!showMask) return;
		var rt = CompositeManager.Instance?.maskRT;
		if (rt == null) return;
		float size = 256;
		GUI.DrawTexture(new Rect(10, 10, size, size), rt, ScaleMode.ScaleToFit);
	}
}
