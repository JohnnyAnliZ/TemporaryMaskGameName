using UnityEngine;

[CreateAssetMenu(fileName = "Globals", menuName = "Game/Globals")]
public class Globals : ScriptableObject {
	[Header("Pixel Perfect")]
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

		Debug.Log($"[Globals] Updated scene grid to {newGridSize:F4} (PPU: {_pixelsPerUnit})"); //FIXME: Make our logging system like we used to do in unreal
	}
	#endif
}
