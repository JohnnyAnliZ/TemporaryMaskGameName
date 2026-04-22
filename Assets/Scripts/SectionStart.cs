using UnityEngine;
using System.Linq;

public class SectionStart : MonoBehaviour
{
	public Section section;

	void OnDrawGizmos() {
		Gizmos.color = Color.green;
		Gizmos.DrawWireSphere(transform.position, 0.5f);
		Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
	}
}

#if UNITY_EDITOR
public class SectionsWindow : UnityEditor.EditorWindow
{
	Section selected;
	Vector2 scroll;
	UnityEditor.SerializedObject cachedSerializedAsset;
	SectionAsset cachedAsset;

	[UnityEditor.MenuItem("Window/Sections")]
	static void Open() => GetWindow<SectionsWindow>("Sections");

	void OnEnable() {
		foreach (Section s in System.Enum.GetValues(typeof(Section))) LoadOrCreateSectionAsset(s);
	}

	void OnGUI() {
		//Section picker toolbar
		UnityEditor.EditorGUILayout.BeginHorizontal();
		foreach (Section s in System.Enum.GetValues(typeof(Section))) {
			bool isSel = s == selected;
			GUI.backgroundColor = isSel ? Color.cyan : Color.white;
			if (GUILayout.Button(s.ToString(), GUILayout.Height(28))) {
				selected = s;
			}
		}
		GUI.backgroundColor = Color.white;
		UnityEditor.EditorGUILayout.EndHorizontal();

		UnityEditor.EditorGUILayout.Space();

		//Play button
		GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
		if (GUILayout.Button($"▶ Play at {selected}", GUILayout.Height(36))) {
			UnityEditor.SessionState.SetInt("startSection", (int)selected);
			if (!UnityEditor.EditorApplication.isPlaying) UnityEditor.EditorApplication.isPlaying = true;
		}
		GUI.backgroundColor = Color.white;

		UnityEditor.EditorGUILayout.Space();

		//Subsections only
		SectionAsset asset = LoadOrCreateSectionAsset(selected);
		if (cachedAsset != asset || cachedSerializedAsset == null) {
			cachedAsset = asset;
			cachedSerializedAsset = new UnityEditor.SerializedObject(asset);
		}
		scroll = UnityEditor.EditorGUILayout.BeginScrollView(scroll);
		cachedSerializedAsset.Update();
		UnityEditor.EditorGUILayout.PropertyField(cachedSerializedAsset.FindProperty("subsections"), includeChildren: true);
		cachedSerializedAsset.ApplyModifiedProperties();
		UnityEditor.EditorGUILayout.EndScrollView();

		//Add subsection dropdown (auto-discovers concrete Subsection types)
		UnityEditor.EditorGUILayout.Space();
		if (GUILayout.Button("+ Add Subsection")) {
			var menu = new UnityEditor.GenericMenu();
			foreach (var t in System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
				.Where(t => typeof(Subsection).IsAssignableFrom(t) && !t.IsAbstract)) {
				System.Type capturedType = t;
				menu.AddItem(new GUIContent(t.Name), false, () => {
					UnityEditor.Undo.RecordObject(asset, $"Add {capturedType.Name}");
					var instance = (Subsection)System.Activator.CreateInstance(capturedType);
					instance.name = capturedType.Name;
					asset.subsections.Add(instance);
					UnityEditor.EditorUtility.SetDirty(asset);
				});
			}
			menu.ShowAsContext();
		}
	}

	static SectionAsset LoadOrCreateSectionAsset(Section section) {
		string resourcePath = $"Sections/Section_{section}";
		var asset = Resources.Load<SectionAsset>(resourcePath);
		if (asset != null) return asset;

		string dir = "Assets/Resources/Sections";
		if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
		asset = ScriptableObject.CreateInstance<SectionAsset>();
		asset.section = section;
		UnityEditor.AssetDatabase.CreateAsset(asset, $"{dir}/Section_{section}.asset");
		UnityEditor.AssetDatabase.SaveAssets();
		UnityEditor.AssetDatabase.Refresh();
		return asset;
	}
}
#endif
