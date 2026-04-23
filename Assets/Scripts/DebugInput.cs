using UnityEngine;
using UnityEngine.InputSystem;

public class DebugInput : MonoBehaviour
{
	MaskDrawer maskDrawer;
	bool bShowMask;

	#if UNITY_EDITOR
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	static void AutoCreate() {
		GameObject go = new GameObject("DebugInput");
		go.AddComponent<DebugInput>();
	}
	#endif

	void Start() {
		maskDrawer = FindAnyObjectByType<MaskDrawer>();
	}

	void Update() {
		Mouse mouse = Mouse.current;
		Keyboard keyboard = Keyboard.current;

		if (mouse.leftButton.wasPressedThisFrame) {
			maskDrawer.Do_Shatter();
		}
		if (keyboard.yKey.wasPressedThisFrame) {
			maskDrawer.Do_ShrinkToBlack();
		}

		if (keyboard.cKey.wasPressedThisFrame) {
			maskDrawer.ResetMask();
		}

		if (keyboard.mKey.wasPressedThisFrame) {
			bShowMask = !bShowMask;
		}
	}
}
