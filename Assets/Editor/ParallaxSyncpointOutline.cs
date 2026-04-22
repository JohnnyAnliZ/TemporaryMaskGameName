using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class ParallaxSyncpointOutline {
	static readonly MethodInfo drawOutline;
	static readonly ParameterInfo[] drawOutlineParams;
	static readonly Color outlineColor = new Color(0.2f, 0.8f, 1f, 0.5f);
	static bool didWarn;

	static ParallaxSyncpointOutline() {
		MethodInfo[] candidates = typeof(Handles).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (MethodInfo m in candidates) {
			if (m.Name != "DrawOutline") continue;
			ParameterInfo[] p = m.GetParameters();
			if (p.Length < 1) continue;
			System.Type t = p[0].ParameterType;
			if (t == typeof(GameObject[]) || t == typeof(IEnumerable<GameObject>) || t == typeof(List<GameObject>)) {
				drawOutline = m;
				drawOutlineParams = p;
				break;
			}
		}
		SceneView.duringSceneGui += OnSceneGui;
	}

	static void OnSceneGui(SceneView view) {
		if (drawOutline == null) {
			if (!didWarn) {
				didWarn = true;
				Debug.LogWarning("[ParallaxSyncpointOutline] Handles.DrawOutline not found via reflection");
			}
			return;
		}

		GameObject sel = Selection.activeGameObject;
		if (sel == null) return;

		List<GameObject> hits = null;
		ParallaxLayer[] layers = Object.FindObjectsByType<ParallaxLayer>(FindObjectsSortMode.None);
		foreach (ParallaxLayer layer in layers) {
			if (layer.syncPoint == null) continue;
			if (layer.syncPoint.gameObject != sel) continue;
			hits ??= new List<GameObject>();
			hits.Add(layer.gameObject);
		}
		if (hits == null) return;

		object firstArg;
		System.Type firstType = drawOutlineParams[0].ParameterType;
		if (firstType == typeof(GameObject[])) firstArg = hits.ToArray();
		else firstArg = hits;

		object[] args = new object[drawOutlineParams.Length];
		args[0] = firstArg;
		for (int I = 1; I < drawOutlineParams.Length; I++) {
			System.Type pt = drawOutlineParams[I].ParameterType;
			if (pt == typeof(Color)) args[I] = outlineColor;
			else if (pt == typeof(float)) args[I] = 0f;
			else if (drawOutlineParams[I].HasDefaultValue) args[I] = drawOutlineParams[I].DefaultValue;
			else args[I] = null;
		}

		drawOutline.Invoke(null, args);
	}
}
