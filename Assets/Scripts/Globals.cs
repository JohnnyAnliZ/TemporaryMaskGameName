using UnityEngine;

[CreateAssetMenu(fileName = "Globals", menuName = "Game/Globals")]
public class Globals : ScriptableObject {
	[Header("2D Camera")]
	public float cameraDeadzoneRight = 1f;
	public float cameraDeadzoneLeft = 2f;
	public float cameraDeadzoneTop = 2f;
	public float cameraDeadzoneBottom = 2f;
	public float cameraFollowSpeed = 8f;
	public float cameraZOffset = -10f;
	public bool cameraSnapToPixelGrid = true;

	[Header("2D Break")]
	public int numBreaks = 8;
	public float shardSize = 1f;
	public float maskBlurRadius = 2f;
	public Vector2 shardSpeedRange = new Vector2(0.5f, 2f);
	public Vector2 shardSpinRange = new Vector2(-180f, 180f);

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
	public float playerMin = 0.5f;
	public float playerMax = 3f;
	public float depthScale = 10f;
	public float fadeDistance = 30f;
	public float minOpacity = 0.3f;
	public float farBrightness = 0.4f;
	public float nearDistance = 8f;
	public float nearBrightness = 2.5f;
	public float spriteZPerPlayerZ = 0.5f;
	public float fallThreshold = -8f;

	[Header("Player Movement")]
	public float moveSpeed = 5f;
	public float gravity = -30f;
	public float riseGravityMulti = 0.5f;
	public float fallGravityMulti = 1.8f;
	public float fallGravityBlend = 2f;
	public float jumpForwardBoost = 4f;
	public float airControl = 0.4f;
	public float coyoteTime = 0.1f;
	public float jumpBufferTime = 0.1f;
	public float jumpTapWindow = 0.1f;
	public float jumpChargeTime = 0.6f;
	public float jumpForceMin = 10f;
	public float jumpForceMax = 18f;
	public float chargeMoveMulti = 0.3f;

	[Header("Camera Depth Zoom")]
	public float cameraOrthoSize = 2f;
	public float zoomMaxNearSize = 3.2f;
	public float zoomMaxFarSize = 0.8f;
	public float zoomMinNear = 1f;
	public float zoomMaxNear = 8f;
	public float zoomMinFar = 1f;
	public float zoomMaxFar = 8f;
	public float zoomDeadzoneRate = 0.02f;

	[Header("Camera Bounds")]
	public float cameraBoundLeft = -1000f;
	public float cameraBoundRight = 1000f;
	public float cameraBoundBottom = -1000f;
	public float cameraBoundTop = 1000f;

	[Header("Outline")]
	public Color outlineColor = Color.white;
	public float outlineThickness = 1f;
	public float outlineMaxOpacity = 1f;
	public float angleFull = 12f;
	public float angleFade = 4f;

	[Header("Misc")]
	#if UNITY_EDITOR
	public UnityEditor.SceneAsset[] gameLevelAssets;
	#endif
	[HideInInspector] public string[] gameLevels;
	public float pixelsPerUnit = 100f;
	public float pixelGridSize => 1f / pixelsPerUnit;

	[System.Serializable]
	public struct ParallaxZFactor {
		public float z;
		[Range(0f, 2f)] public float factor;
	}
	[HideInInspector] public ParallaxZFactor[] parallaxLayers;
	[HideInInspector, Range(0f, 1f)] public float parallaxScaleFactor = 1f;

	public float GetParallaxFactorForZ(float z) {
		const float EPSILON = 0.01f;
		if (parallaxLayers != null) {
			for (int i = 0; i < parallaxLayers.Length; i++) {
				float worldZ = world2DZ + parallaxLayers[i].z * 4;
				if (Mathf.Abs(worldZ - z) < EPSILON) {
					return parallaxLayers[i].factor;
				}
			}
		}
		Log.Warn($"No parallax layer configured for z={z}");
		return 1f;
	}

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

	public class ParallaxWindow : UnityEditor.EditorWindow
	{
		Vector2 scroll;

		[UnityEditor.MenuItem("Window/Parallax Layers")]
		static void Open() => GetWindow<ParallaxWindow>("Parallax Layers");

		void OnGUI() {
			Globals g = Instance;
			if (g.parallaxLayers == null) g.parallaxLayers = System.Array.Empty<ParallaxZFactor>();

			scroll = UnityEditor.EditorGUILayout.BeginScrollView(scroll);

			g.parallaxScaleFactor = UnityEditor.EditorGUILayout.Slider("Scale Factor", g.parallaxScaleFactor, 0f, 1f);
			UnityEditor.EditorGUILayout.Space();

			int removeIndex = -1;
			for (int i = 0; i < g.parallaxLayers.Length; i++) {
				UnityEditor.EditorGUILayout.BeginHorizontal();
				UnityEditor.EditorGUILayout.LabelField("Z", GUILayout.Width(14));
				g.parallaxLayers[i].z = UnityEditor.EditorGUILayout.FloatField(g.parallaxLayers[i].z, GUILayout.Width(60));
				g.parallaxLayers[i].factor = UnityEditor.EditorGUILayout.Slider(g.parallaxLayers[i].factor, 0f, 2f);
				if (GUILayout.Button("-", GUILayout.Width(22))) removeIndex = i;
				UnityEditor.EditorGUILayout.EndHorizontal();
			}

			if (removeIndex >= 0) {
				var list = new System.Collections.Generic.List<ParallaxZFactor>(g.parallaxLayers);
				list.RemoveAt(removeIndex);
				g.parallaxLayers = list.ToArray();
			}

			if (GUILayout.Button("Add Layer")) {
				var list = new System.Collections.Generic.List<ParallaxZFactor>(g.parallaxLayers);
				list.Add(new ParallaxZFactor { z = 0f, factor = 0.5f });
				g.parallaxLayers = list.ToArray();
			}

			UnityEditor.EditorGUILayout.EndScrollView();
			if (GUI.changed) UnityEditor.EditorUtility.SetDirty(g);
		}
	}
	#endif
}
