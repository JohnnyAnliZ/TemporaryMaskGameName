#if UNITY_EDITOR
using UnityEditor;

//Because auto compile is disabled because it's a bitch
internal static class RefreshAssetsOnEnterPlaymode {
	const string AutoRefreshKey = "kAutoRefreshMode";

	static bool IsAutoRefreshDisabled => EditorPrefs.GetInt(AutoRefreshKey, -1) == 0;

	[InitializeOnLoadMethod]
	static void InitOnLoad() {
		EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
		EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
	}

	static void OnPlayModeStateChanged(PlayModeStateChange state) {
		if (state == PlayModeStateChange.ExitingEditMode && IsAutoRefreshDisabled) {
			AssetDatabase.Refresh();
		}
	}
}
#endif
