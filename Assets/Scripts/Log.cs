using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class Log {
	static string Format(string msg, string file, string member) {
		return $"[{Path.GetFileNameWithoutExtension(file)}.{member}] {msg}";
	}

	[Conditional("DEBUG")]
	public static void Info(string msg, bool screen = false, float screenDuration = 2f,
		[CallerFilePath] string file = "", [CallerMemberName] string member = "") {
		string formatted = Format(msg, file, member);
		Debug.Log(formatted);

		#if UNITY_EDITOR
		if (screen) PushScreen(formatted, Color.white, screenDuration);
		#endif
	}

	[Conditional("DEBUG")]
	public static void Info(string msg, Color screenColor, float screenDuration = 2f,
		[CallerFilePath] string file = "", [CallerMemberName] string member = "") {
		string formatted = Format(msg, file, member);
		Debug.Log(formatted);

		#if UNITY_EDITOR
		PushScreen(formatted, screenColor, screenDuration);
		#endif
	}

	[Conditional("DEBUG")]
	public static void Warn(string msg, bool screen = false, float screenDuration = 2f,
		[CallerFilePath] string file = "", [CallerMemberName] string member = "") {
		string formatted = Format(msg, file, member);
		Debug.LogWarning(formatted);

		#if UNITY_EDITOR
		if (screen) PushScreen(formatted, Color.yellow, screenDuration);
		#endif
	}

	[Conditional("DEBUG")]
	public static void Error(string msg, bool screen = false, float screenDuration = 2f,
		[CallerFilePath] string file = "", [CallerMemberName] string member = "") {
		string formatted = Format(msg, file, member);
		Debug.LogError(formatted);

		#if UNITY_EDITOR
		if (screen) PushScreen(formatted, Color.red, screenDuration);
		#endif
	}

	#if UNITY_EDITOR
	struct ScreenMsg {
		public string text;
		public Color color;
		public float expireTime;
	}

	const int FontSize = 18;
	const int LineHeight = FontSize + 6;
	const int Padding = 10;

	//Must use a getter because we are static but Screen.height is not
	static int GetMaxMessages() {
		return (Screen.height - Padding * 2) / LineHeight;
	}

	static ScreenMsg[] _msgs = new ScreenMsg[64];
	static int _head;
	static GUIStyle _style;

	static void PushScreen(string msg, Color color, float duration = 2f) {
		if (_head >= _msgs.Length) {
			System.Array.Resize(ref _msgs, _msgs.Length * 2);
		}
		_msgs[_head] = new ScreenMsg {
			text = msg,
			color = color,
			expireTime = Time.unscaledTime + duration,
		};
		_head++;
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics() {
		_msgs = new ScreenMsg[64];
		_head = 0;
		_style = null;
	}

	//Because we need access to OnGui callback which is only called for MonoBehaviours so we just create this lil guy
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	static void InitScreenLogger() {
		GameObject go = new GameObject("ScreenLog");
		go.hideFlags = HideFlags.HideAndDontSave;
		Object.DontDestroyOnLoad(go);
		go.AddComponent<ScreenLogger>();
	}
	class ScreenLogger : MonoBehaviour {
		void OnGUI() => DrawScreenMessages();
	}

	static void DrawScreenMessages() {
		_style ??= new GUIStyle(GUI.skin.label) {
			fontSize = FontSize,
			fontStyle = FontStyle.Bold,
		};

		float y = Padding;
		int maxVisible = GetMaxMessages();
		int drawn = 0;
		for (int I = _head - 1; I >= 0 && drawn < maxVisible; I--) {
			ref var m = ref _msgs[I];
			if (Time.unscaledTime > m.expireTime) continue;

			Color prev = GUI.color;
			GUI.color = m.color;
			GUI.Label(new Rect(Padding, y, Screen.width, LineHeight), m.text, _style);
			GUI.color = prev;
			y += LineHeight;
			drawn++;
		}
	}
	#endif
}
