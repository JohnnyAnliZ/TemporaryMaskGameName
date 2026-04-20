using UnityEngine;

[CreateAssetMenu(fileName = "Globals", menuName = "Game/Globals")]
public class Globals : ScriptableObject {
	[Header("2D Camera")]
	public float cameraOrthoSize = 2f;
	public float cameraDeadzoneRight = 1f;
	public float cameraDeadzoneLeft = 2f;
	public float cameraDeadzoneTop = 2f;
	public float cameraDeadzoneBottom = 2f;
	public float cameraFollowSpeed = 8f;
	public float cameraZOffset = -10f;
	public bool cameraSnapToPixelGrid = true;

	[Header("3D Camera")]
	public float mouseSensitivity = 2f;
	public float eyeOffset = 0.6f;
	public float pitchClamp = 85f;

	[Header("World")]
	public float world2DZ = 1000f;
	public float world3DZ = 0f;
	public float camera2DNearClip = 990f;
	public float camera2DFarClip = 1010f;
	public float platformDistance = 0.8f;
	public float zOffset = 1f;
	public float projectionSize = 4f;

	[Header("Player")]
	public float playerScale = 7f;
	public float depthScale = 10f;
	public float fallThreshold = -8f;

	[Header("Misc")]
	public bool spawnAtCamera = true;
	#if UNITY_EDITOR
	public UnityEditor.SceneAsset[] gameLevelAssets;
	#endif
	[HideInInspector] public string[] gameLevels;
	public float pixelsPerUnit = 100f;
	public float pixelGridSize => 1f / pixelsPerUnit;

	static Globals _instance;
	public static Globals Instance {
		get {
			_instance ??= Resources.Load<Globals>("Globals");
			return _instance;
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics() => _instance = null;

	void OnValidate() {
		#if UNITY_EDITOR
		UpdateEditorSettings();
		SyncLevels();
		#endif
	}

	#if UNITY_EDITOR
	void UpdateEditorSettings() {
		if (pixelsPerUnit <= 0) return;

		float gridSize = pixelGridSize;
		UnityEditor.EditorSnapSettings.gridSize = new Vector3(gridSize, gridSize, gridSize);
		UnityEditor.EditorSnapSettings.gridSnapEnabled = cameraSnapToPixelGrid;
	}

	void SyncLevels() {
		if (gameLevelAssets == null) {
			gameLevels = System.Array.Empty<string>();
			return;
		}

		gameLevels = new string[gameLevelAssets.Length];
		for (int I = 0; I < gameLevelAssets.Length; I++) {
			gameLevels[I] = gameLevelAssets[I] != null ? gameLevelAssets[I].name : "";
		}
	}

	public class GlobalsWindow : UnityEditor.EditorWindow
	{
		UnityEditor.Editor cachedEditor;
		Vector2 scroll;

		[UnityEditor.MenuItem("Window/Globals")]
		static void Open() => GetWindow<GlobalsWindow>("Globals");

		void OnGUI() {
			scroll = UnityEditor.EditorGUILayout.BeginScrollView(scroll);
			UnityEditor.Editor.CreateCachedEditor(Instance, null, ref cachedEditor);
			cachedEditor.OnInspectorGUI();
			UnityEditor.EditorGUILayout.EndScrollView();
		}
	}
	#endif
}
