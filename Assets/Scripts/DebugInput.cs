using UnityEngine;
using UnityEngine.InputSystem;

public class DebugInput : MonoBehaviour
{
	public float portalRadius = 100f;
	MaskDrawer maskDrawer;

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
		if (maskDrawer == null) return;

		Mouse mouse = Mouse.current;
		Keyboard keyboard = Keyboard.current;
		if (mouse == null || keyboard == null) return;

		if (mouse.leftButton.wasPressedThisFrame) {
			Vector2 pos = mouse.position.ReadValue();
			maskDrawer.AddPortal(pos, portalRadius);
            maskDrawer.Do_Shatter();

        }

        if (keyboard.cKey.wasPressedThisFrame) {
			maskDrawer.ClearPortals();
		}

		if (keyboard.mKey.wasPressedThisFrame) {
			bShowMask = !bShowMask;
		}
	}

	bool bShowMask, bShowDebug = true;
	GUIStyle btnStyle, lblStyle;

	void InitStyles() {
		if (btnStyle != null) return;
		btnStyle = new GUIStyle(GUI.skin.button);
		btnStyle.normal.background = Texture2D.grayTexture;
		btnStyle.hover.background = Texture2D.whiteTexture;
		btnStyle.active.background = Texture2D.whiteTexture;
		btnStyle.normal.textColor = Color.white;
		btnStyle.hover.textColor = Color.black;
		btnStyle.fontSize = 10;
		btnStyle.fixedHeight = 18;
		btnStyle.margin = new RectOffset(0, 0, 0, 0);

		lblStyle = new GUIStyle(GUI.skin.label);
		lblStyle.fontSize = 8;
		lblStyle.normal.textColor = new Color(1, 1, 1, 1);
	}

	void OnGUI() {
		InitStyles();

		if (bShowDebug && maskDrawer != null) {
			GUILayout.BeginArea(new Rect(10, 10, 80, 100));
			if (GUILayout.Button("Portals", btnStyle)) maskDrawer.mode = MaskMode.Portals;
			if (GUILayout.Button("2D", btnStyle)) maskDrawer.mode = MaskMode.TwoD;
			if (GUILayout.Button("3D", btnStyle)) maskDrawer.mode = MaskMode.ThreeD;
			if (GUILayout.Button("20/20", btnStyle)) maskDrawer.mode = MaskMode.Split;
			GUILayout.EndArea();
		}

		if (bShowMask) {
			RenderTexture rt = CompositeManager.Instance?.maskRT;
			if (rt != null) {
				float size = 256;
				float y = bShowDebug ? 120 : 10;
				GUI.DrawTexture(new Rect(10, y, size, size), rt, ScaleMode.ScaleToFit);
			}
		}
	}
}
