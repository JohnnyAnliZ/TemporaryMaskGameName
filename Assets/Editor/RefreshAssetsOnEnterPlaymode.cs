#if UNITY_EDITOR
using UnityEditor;

//Because auto compile is disabled because it's a bitch
internal static class RefreshAssetsOnEnterPlaymode {
	static bool bIsAutoRefreshDisabled => EditorPrefs.GetInt("kAutoRefreshMode", -1) == 0;

	[InitializeOnLoadMethod]
	static void InitOnLoad() {
		EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
		EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
	}

	static void OnPlayModeStateChanged(PlayModeStateChange state) {
		if (state == PlayModeStateChange.ExitingEditMode && bIsAutoRefreshDisabled) {
			AssetDatabase.Refresh();
		}
	}
}
#endif
