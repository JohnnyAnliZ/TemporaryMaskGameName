using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class HierarchyColors {
	static readonly Dictionary<string, Color> NameColors = new() {
		{ "2DScene",		new Color(0.2f, 0.6f, 1.0f, 0.2f) },
		{ "3DScene",		new Color(0.3f, 0.9f, 0.4f, 0.2f) },
		{ "2DPlayer",		new Color(1.0f, 0.6f, 0.1f, 0.2f) },
		{ "2DCamera",		new Color(1.0f, 0.6f, 0.1f, 0.2f) },
		{ "3DPlayer",		new Color(1.0f, 0.2f, 0.0f, 0.2f) },
		{ "3DCamera",		new Color(1.0f, 0.2f, 0.0f, 0.2f) },

		//3D
		{ "Platforms",		new Color(0.7f, 0.4f, 1.4f, 0.2f) },
		{ "StartingPoints",	new Color(0.7f, 0.9f, 1.0f, 0.2f) },

		//2D
		{ "Backgrounds",	new Color(0.0f, 0.0f, 0.0f, 0.2f) },
		{ "Bg1",		new Color(1.0f, 0.0f, 0.0f, 0.2f) }, //red
		{ "Bg2",		new Color(1.0f, 0.5f, 0.0f, 0.2f) }, //orange
		{ "Bg3",		new Color(1.0f, 1.0f, 0.0f, 0.2f) }, //yellow
		{ "Plant",		new Color(0.3f, 0.9f, 0.3f, 0.2f) }, //green
		{ "FogBack",		new Color(0.2f, 0.8f, 0.8f, 0.2f) }, //teal
		{ "Darkness",		new Color(0.25f, 0.1f, 0.4f, 0.2f) }, //deep purple
		{ "Bg4",		new Color(0.2f, 0.4f, 1.0f, 0.2f) }, //blue
		{ "FogFront",		new Color(0.7f, 0.9f, 1.0f, 0.2f) }, //pale sky
		{ "SyncPoints",		new Color(1.0f, 0.0f, 0.0f, 0.2f) },

		{ "Foregrounds",	new Color(0.0f, 0.0f, 0.0f, 0.2f) },
		{ "PlatformsBack",	new Color(1.0f, 0.0f, 0.0f, 0.2f) },
		{ "Pillars",		new Color(1.0f, 1.0f, 0.0f, 0.2f) },
		{ "PlatformsFront",	new Color(0.3f, 0.9f, 0.3f, 0.2f) },
		{ "Mushrooms",		new Color(0.2f, 0.4f, 1.0f, 0.2f) },
		{ "LightBeams",		new Color(0.25f, 0.1f, 0.4f, 0.2f) },

		{ "Reference",		new Color(1.0f, 0.6f, 0.7f, 0.2f) },
	};

	static GUIStyle _labelStyle;
	static GUIStyle LabelStyle {
		get {
			if (_labelStyle == null) {
				_labelStyle = new GUIStyle(GUI.skin.FindStyle("TV Line") ?? EditorStyles.label);
			}
			return _labelStyle;
		}
	}

	static HierarchyColors() {
		EditorApplication.hierarchyWindowItemOnGUI += OnGUI;
	}

	static void OnGUI(int instanceID, Rect rect) {
		if (Event.current.type != EventType.Repaint) return;

		var go = EditorUtility.EntityIdToObject(instanceID) as GameObject;
		if (go == null) return;

		if (NameColors.TryGetValue(go.name, out var color)) {
			EditorGUI.DrawRect(rect, color);
			float iconOffset = EditorGUIUtility.singleLineHeight;
			var labelRect = new Rect(rect.x + iconOffset, rect.y, rect.width - iconOffset, rect.height);
			LabelStyle.Draw(labelRect, new GUIContent(go.name), false, false, false, false);
		}
	}
}
