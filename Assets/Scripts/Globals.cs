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

	[Header("First Person Camera")]
	public float mouseSensitivity = 2f;
	public float eyeOffset = 0.6f;
	public float pitchClamp = 85f;

	[Header("Misc")]
	[SerializeField] float _pixelsPerUnit = 100f;

	static Globals _instance;
	public static Globals Instance {
		get {
			_instance ??= Resources.Load<Globals>("Globals");
			return _instance;
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics() => _instance = null;

	public float pixelsPerUnit {
		get => _pixelsPerUnit;
		set {
			if (_pixelsPerUnit != value) {
				_pixelsPerUnit = value;
				#if UNITY_EDITOR
				UpdateEditorGrid();
				#endif
			}
		}
	}

	public float pixelGridSize => 1f / _pixelsPerUnit;

	void OnValidate() {
		#if UNITY_EDITOR
		UpdateEditorGrid();
		#endif
	}

	#if UNITY_EDITOR
	void UpdateEditorGrid() {
		if (_pixelsPerUnit <= 0) return;

		float newGridSize = 1f / _pixelsPerUnit;
		UnityEditor.EditorSnapSettings.gridSize = new Vector3(newGridSize, newGridSize, newGridSize);

		Log.Info($"Updated scene grid to {newGridSize} (PPU: {_pixelsPerUnit})");
	}

	public class GlobalsWindow : UnityEditor.EditorWindow
	{
		UnityEditor.Editor cachedEditor;

		[UnityEditor.MenuItem("Window/Globals")]
		static void Open() => GetWindow<GlobalsWindow>("Globals");

		void OnGUI() {
			UnityEditor.Editor.CreateCachedEditor(Instance, null, ref cachedEditor);
			cachedEditor.OnInspectorGUI();
		}
	}
	#endif
}
