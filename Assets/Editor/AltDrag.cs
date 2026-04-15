using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class UnrealAltDrag
{
	private enum DragConstraint { Free, AxisX, AxisY, AxisZ, PlaneXY, PlaneXZ, PlaneYZ }

	private static bool bIsDuplicating = false;
	private static Vector3[] startPositions;
	private static Vector2 dragStartMouse;
	private static DragConstraint dragConstraint = DragConstraint.Free;
	private static Quaternion handleRotation = Quaternion.identity;

	private const float kPickDistance = 20f;

	static UnrealAltDrag() {
		SceneView.beforeSceneGui -= OnBeforeSceneGUI;
		SceneView.beforeSceneGui += OnBeforeSceneGUI;
	}

	private static void OnBeforeSceneGUI(SceneView sceneView) {
		Event e = Event.current;

		if ((e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) && e.button == 0) {
			bIsDuplicating = false;
			return;
		}

		if (!e.alt || e.button != 0) return;

		if (e.type == EventType.MouseDown) {
			bIsDuplicating = false;
			if (Selection.gameObjects.Length > 0) {
				dragStartMouse = e.mousePosition;
				dragConstraint = DetectConstraint(e.mousePosition);
				handleRotation = (Tools.pivotRotation == PivotRotation.Local && Selection.activeTransform != null)
					? Selection.activeTransform.rotation
					: Quaternion.identity;
			}
			e.Use();
			return;
		}

		if (e.type == EventType.MouseDrag) {
			if (!bIsDuplicating && Selection.gameObjects.Length > 0) {
				bIsDuplicating = true;
				Undo.IncrementCurrentGroup();
				EditorApplication.ExecuteMenuItem("Edit/Duplicate");
				Undo.SetCurrentGroupName("Alt Drag Duplicate");

				var selected = Selection.gameObjects;
				startPositions = new Vector3[selected.Length];
				for (int i = 0; i < selected.Length; i++)
					startPositions[i] = selected[i].transform.position;
				dragStartMouse = e.mousePosition;
			}

			if (bIsDuplicating) {
				Vector3 worldStart = GUIToWorldOnDragPlane(sceneView.camera, dragStartMouse);
				Vector3 worldCurrent = GUIToWorldOnDragPlane(sceneView.camera, e.mousePosition);
				Vector3 worldDelta = ConstrainDelta(worldCurrent - worldStart);

				var objs = Selection.gameObjects;
				for (int i = 0; i < objs.Length && i < startPositions.Length; i++) {
					Undo.RecordObject(objs[i].transform, "Alt Drag Duplicate");
					objs[i].transform.position = startPositions[i] + worldDelta;
				}
			}

			e.Use();
		}
	}

	private static DragConstraint DetectConstraint(Vector2 mouseGUI) {
		if (Tools.current != Tool.Move || Selection.activeTransform == null)
			return DragConstraint.Free;

		Vector3 pivot = Tools.handlePosition;
		float size = HandleUtility.GetHandleSize(pivot);
		Quaternion rot = (Tools.pivotRotation == PivotRotation.Local)
			? Selection.activeTransform.rotation
			: Quaternion.identity;

		Vector3[] axes = { rot * Vector3.right, rot * Vector3.up, rot * Vector3.forward };
		Vector2 center = HandleUtility.WorldToGUIPoint(pivot);

		// Plane handles sit at ~15% along each axis pair
		int[,] pairs = { {0,1}, {0,2}, {1,2} };
		DragConstraint[] planeCs = { DragConstraint.PlaneXY, DragConstraint.PlaneXZ, DragConstraint.PlaneYZ };
		float bestPlaneDist = float.MaxValue;
		DragConstraint bestPlane = DragConstraint.Free;
		for (int i = 0; i < 3; i++) {
			Vector2 pc = HandleUtility.WorldToGUIPoint(
				pivot + (axes[pairs[i, 0]] + axes[pairs[i, 1]]) * size * 0.15f);
			float d = Vector2.Distance(mouseGUI, pc);
			if (d < bestPlaneDist) { bestPlaneDist = d; bestPlane = planeCs[i]; }
		}
		if (bestPlaneDist < kPickDistance) return bestPlane;

		// Axis lines
		DragConstraint[] axisCs = { DragConstraint.AxisX, DragConstraint.AxisY, DragConstraint.AxisZ };
		float bestAxisDist = float.MaxValue;
		DragConstraint bestAxis = DragConstraint.Free;
		for (int i = 0; i < 3; i++) {
			Vector2 end = HandleUtility.WorldToGUIPoint(pivot + axes[i] * size);
			float d = DistToSegment(mouseGUI, center, end);
			if (d < bestAxisDist) { bestAxisDist = d; bestAxis = axisCs[i]; }
		}
		if (bestAxisDist < kPickDistance) return bestAxis;

		return DragConstraint.Free;
	}

	private static Vector3 ConstrainDelta(Vector3 delta) {
		Vector3 x = handleRotation * Vector3.right;
		Vector3 y = handleRotation * Vector3.up;
		Vector3 z = handleRotation * Vector3.forward;
		return dragConstraint switch {
			DragConstraint.AxisX   => Vector3.Project(delta, x),
			DragConstraint.AxisY   => Vector3.Project(delta, y),
			DragConstraint.AxisZ   => Vector3.Project(delta, z),
			DragConstraint.PlaneXY => delta - Vector3.Project(delta, z),
			DragConstraint.PlaneXZ => delta - Vector3.Project(delta, y),
			DragConstraint.PlaneYZ => delta - Vector3.Project(delta, x),
			_ => delta,
		};
	}

	private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b) {
		Vector2 ab = b - a;
		float sqrLen = ab.sqrMagnitude;
		if (sqrLen < 0.001f) return Vector2.Distance(p, a);
		float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqrLen);
		return Vector2.Distance(p, a + ab * t);
	}

	private static Vector3 GUIToWorldOnDragPlane(Camera cam, Vector2 guiPoint) {
		Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);
		Plane plane = new Plane(-cam.transform.forward, startPositions[0]);
		if (plane.Raycast(ray, out float dist))
			return ray.GetPoint(dist);
		return ray.origin;
	}
}
