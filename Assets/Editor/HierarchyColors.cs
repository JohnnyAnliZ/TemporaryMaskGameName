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
		{ "Platforms",		new Color(0.7f, 0.4f, 1.4f, 0.2f) },
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
