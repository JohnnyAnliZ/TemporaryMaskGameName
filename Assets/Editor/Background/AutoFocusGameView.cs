using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class AutoFocusGameView {
	static AutoFocusGameView() {
		EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
	}

	static void OnPlayModeStateChanged(PlayModeStateChange state) {
		if (state != PlayModeStateChange.EnteredPlayMode) return;

		Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
		if (gameViewType == null) return;

		EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, null, true);
		if (gameView == null) return;

		gameView.Focus();

		//Unity won't fuck with Cursor.lockState until an actual click lands in the Game view
		Rect pos = gameView.position;
		Event click = new Event {
			type = EventType.MouseDown,
			mousePosition = new Vector2(pos.width * 0.5f, pos.height * 0.5f),
			button = 0,
			clickCount = 1,
		};
		gameView.SendEvent(click);
		Event release = new Event {
			type = EventType.MouseUp,
			mousePosition = click.mousePosition,
			button = 0,
			clickCount = 1,
		};
		gameView.SendEvent(release);
	}
}
