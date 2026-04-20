using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class ViewportSync
{
	const string SYNC_KEY = "ViewportSync";
	const string GIZMO_KEY = "SpriteGizmos";

	static bool bIsEnabled;
	static bool bIsSyncing;
	static float lastSyncedX;
	static bool bShowSpriteGizmos;

	static ViewportSync() {
		bIsEnabled = EditorPrefs.GetBool(SYNC_KEY, true);
		bShowSpriteGizmos = EditorPrefs.GetBool(GIZMO_KEY, true);
		SceneView.duringSceneGui += OnSceneGUI;
	}

	[MenuItem("Window/Viewport Sync")]
	static void Toggle() {
		bIsEnabled = !bIsEnabled;
		EditorPrefs.SetBool(SYNC_KEY, bIsEnabled); //persist across recompiles
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

	[MenuItem("Window/Sprite Gizmos")]
	static void ToggleGizmos() {
		bShowSpriteGizmos = !bShowSpriteGizmos;
		EditorPrefs.SetBool(GIZMO_KEY, bShowSpriteGizmos);
		SceneView.RepaintAll();
	}
	[MenuItem("Window/Sprite Gizmos", true)]
	static bool ToggleGizmosValidate() {
		Menu.SetChecked("Window/Sprite Gizmos", bShowSpriteGizmos);
		return true;
	}

	static void OnSceneGUI(SceneView view) {
		Globals g = Globals.Instance;

		if (bShowSpriteGizmos) {
			GameObject parent2D = GameObject.Find("2DScene");
			if (parent2D != null) {
				SpriteRenderer[] sprites = parent2D.GetComponentsInChildren<SpriteRenderer>();
				foreach (SpriteRenderer sprite in sprites) {
					if (sprite.sprite == null) continue;
					if (sprite.CompareTag("NoGizmo")) continue;

					float zOffset = (sprite.transform.position.z - g.world2DZ) * g.platformDistance;
					Vector3 center = new Vector3(sprite.bounds.center.x, sprite.bounds.center.y, g.world3DZ + zOffset + g.zOffset);
					Vector3 size = new Vector3(sprite.bounds.size.x, sprite.bounds.size.y, g.projectionSize);

					//Shaded Cube
					Vector3 halfSize = size * 0.5f;
					Color faceColor = new Color(0, 1, 0, 0.05f);

					//Front face (Z+)
					Vector3[] front = {
						center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
						center + new Vector3(halfSize.x, -halfSize.y, halfSize.z),
						center + new Vector3(halfSize.x, halfSize.y, halfSize.z),
						center + new Vector3(-halfSize.x, halfSize.y, halfSize.z)
					};
					Handles.DrawSolidRectangleWithOutline(front, faceColor, Color.clear);

					//Back face (Z-)
					Vector3[] back = {
						center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
						center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
						center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
						center + new Vector3(halfSize.x, halfSize.y, -halfSize.z)
					};
					Handles.DrawSolidRectangleWithOutline(back, faceColor, Color.clear);

					//Top face (Y+)
					Vector3[] top = {
						center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
						center + new Vector3(halfSize.x, halfSize.y, -halfSize.z),
						center + new Vector3(halfSize.x, halfSize.y, halfSize.z),
						center + new Vector3(-halfSize.x, halfSize.y, halfSize.z)
					};
					Handles.DrawSolidRectangleWithOutline(top, faceColor, Color.clear);

					//Bottom face (Y-)
					Vector3[] bottom = {
						center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
						center + new Vector3(halfSize.x, -halfSize.y, halfSize.z),
						center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
						center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z)
					};
					Handles.DrawSolidRectangleWithOutline(bottom, faceColor, Color.clear);

					//Right face (X+)
					Vector3[] right = {
						center + new Vector3(halfSize.x, -halfSize.y, halfSize.z),
						center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
						center + new Vector3(halfSize.x, halfSize.y, -halfSize.z),
						center + new Vector3(halfSize.x, halfSize.y, halfSize.z)
					};
					Handles.DrawSolidRectangleWithOutline(right, faceColor, Color.clear);

					//Left face (X-)
					Vector3[] left = {
						center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
						center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
						center + new Vector3(-halfSize.x, halfSize.y, halfSize.z),
						center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z)
					};
					Handles.DrawSolidRectangleWithOutline(left, faceColor, Color.clear);

					//Wireframe
					Handles.color = new Color(0, 1, 0, 0.8f);
					Handles.DrawWireCube(center, size);
				}
			}

			//World3DZ plane
			float extent = 10000f;
			Vector3[] plane = {
				new Vector3(-extent, -extent, g.world3DZ),
				new Vector3(extent, -extent, g.world3DZ),
				new Vector3(extent, extent, g.world3DZ),
				new Vector3(-extent, extent, g.world3DZ),
			};
			Handles.color = Color.white; //reset from previous use of Handles
			Handles.DrawSolidRectangleWithOutline(plane, new Color(1, 0, 0, 0.02f), Color.clear);
		}

		//3d projection
		if (view.in2DMode) {
			GameObject active = Selection.activeGameObject;
			if (active != null) {
				Platform platform = active.GetComponentInParent<Platform>();
				if (platform != null) {
					Renderer[] renderers = platform.GetComponentsInChildren<Renderer>();
					Handles.color = Color.cyan;
					float z = g.world2DZ - 10;
					foreach (Renderer r in renderers) {
						if (r is SpriteRenderer) continue;
						Vector3 min = r.bounds.min;
						Vector3 max = r.bounds.max;
						Vector3 bl = new Vector3(min.x, min.y, z);
						Vector3 br = new Vector3(max.x, min.y, z);
						Vector3 tr = new Vector3(max.x, max.y, z);
						Vector3 tl = new Vector3(min.x, max.y, z);
						Handles.DrawLine(bl, br);
						Handles.DrawLine(br, tr);
						Handles.DrawLine(tr, tl);
						Handles.DrawLine(tl, bl);
					}
				}
			}
		}

		if (!bIsEnabled || bIsSyncing) return;
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
		if (Mathf.Approximately(viewX, lastSyncedX)) return;
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
