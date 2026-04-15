using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class SceneViewSync
{
	const string KEY = "ViewportSync";

	static bool bIsEnabled;
	static bool bIsSyncing;
	static float lastSyncedX;

	static SceneViewSync() {
		bIsEnabled = EditorPrefs.GetBool(KEY, true);
		SceneView.duringSceneGui += OnSceneGUI;
	}

	[MenuItem("Window/Viewport Sync")]
	static void Toggle() {
		bIsEnabled = !bIsEnabled;
		EditorPrefs.SetBool(KEY, bIsEnabled); //persist across recompiles
		if (bIsEnabled) {
			SceneView sceneView = EditorWindow.focusedWindow as SceneView;
			if (sceneView != null) lastSyncedX = sceneView.pivot.x;
		}
	}

	[MenuItem("Window/Viewport Sync", true)]
	static bool ToggleValidate() {
		Menu.SetChecked("Window/Viewport Sync", bIsEnabled);
		return true;
	}

	static void OnSceneGUI(SceneView view) {
		if (!bIsEnabled || bIsSyncing) return;

		Globals g = Globals.Instance;
		foreach (SceneView sceneView in SceneView.sceneViews) {
			if (sceneView.in2DMode) {
				var settings = sceneView.cameraSettings;
				settings.dynamicClip = false;
				settings.nearClip = g.camera2DNearClip;
				settings.farClip = g.camera2DFarClip;
				sceneView.cameraSettings = settings;

				Vector3 pivot = sceneView.pivot;
				pivot.z = 0;
				sceneView.pivot = pivot;
			}
		}

		if (!view.hasFocus) return;

		float viewX = view.pivot.x;
		if (Mathf.Approximately(viewX, lastSyncedX)) return; //if repaint called every mouse move it will jitter
		lastSyncedX = viewX;

		bIsSyncing = true;
		foreach (SceneView other in SceneView.sceneViews) {
			if (other == view) continue;
			Vector3 pivot = other.pivot;
			pivot.x = viewX;
			other.pivot = pivot;
			other.Repaint();
		}
		bIsSyncing = false;
	}
}
