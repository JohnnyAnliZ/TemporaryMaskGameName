using UnityEngine;
using System.Linq;

public class SectionStart : MonoBehaviour
{
	public Section section;
	public GameplayStart gameplayStart;

	void OnDrawGizmos() {
		Gizmos.color = Color.darkGreen;
		Vector3 p = transform.position;
		Gizmos.DrawWireSphere(p, 0.5f);
		Gizmos.DrawWireSphere(new Vector3(p.x, p.y, Globals.Instance.world2DZ), 0.5f);
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
			GUI.backgroundColor = isSel ? Color.darkBlue : Color.white;
			if (GUILayout.Button(s.ToString(), GUILayout.Height(28))) {
				selected = s;
			}
		}
		GUI.backgroundColor = Color.white;
		UnityEditor.EditorGUILayout.EndHorizontal();

		UnityEditor.EditorGUILayout.Space();

		//Subsections
		SectionAsset asset = LoadOrCreateSectionAsset(selected);
		if (cachedAsset != asset || cachedSerializedAsset == null) {
			cachedAsset = asset;
			cachedSerializedAsset = new UnityEditor.SerializedObject(asset);
		}
		scroll = UnityEditor.EditorGUILayout.BeginScrollView(scroll);
		cachedSerializedAsset.Update();
		var subsectionsProp = cachedSerializedAsset.FindProperty("subsections");
		int pendingDelete = -1;
		int pendingMoveFrom = -1, pendingMoveTo = -1;
		for (int i = 0; i < subsectionsProp.arraySize; i++) {
			SubsectionAction action = DrawSubsection(subsectionsProp.GetArrayElementAtIndex(i), i, subsectionsProp.arraySize);
			if (action == SubsectionAction.Delete) pendingDelete = i;
			else if (action == SubsectionAction.MoveUp) { pendingMoveFrom = i; pendingMoveTo = i - 1; }
			else if (action == SubsectionAction.MoveDown) { pendingMoveFrom = i; pendingMoveTo = i + 1; }
		}
		if (pendingDelete >= 0) {
			UnityEditor.Undo.RecordObject(asset, "Delete Subsection");
			asset.subsections.RemoveAt(pendingDelete);
			UnityEditor.EditorUtility.SetDirty(asset);
			cachedSerializedAsset.Update();
		}
		if (pendingMoveFrom >= 0) {
			UnityEditor.Undo.RecordObject(asset, "Reorder Subsection");
			var tmp = asset.subsections[pendingMoveFrom];
			asset.subsections[pendingMoveFrom] = asset.subsections[pendingMoveTo];
			asset.subsections[pendingMoveTo] = tmp;
			UnityEditor.EditorUtility.SetDirty(asset);
			cachedSerializedAsset.Update();
		}
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

	enum SubsectionAction { None, Delete, MoveUp, MoveDown }

	SubsectionAction DrawSubsection(UnityEditor.SerializedProperty element, int index, int count) {
		float lineH = UnityEditor.EditorGUIUtility.singleLineHeight;
		Rect headerRect = UnityEditor.EditorGUILayout.GetControlRect(false, lineH + 6);
		UnityEditor.EditorGUI.DrawRect(headerRect, new Color(0.21f, 0.31f, 0.5f));

		Rect row = new Rect(headerRect.x + 6, headerRect.y + 3, headerRect.width - 12, lineH);
		Rect deleteRect = new Rect(row.xMax - 26, row.y, 26, row.height);
		Rect playRect = new Rect(deleteRect.x - 30, row.y, 26, row.height);
		Rect downRect = new Rect(playRect.x - 26, row.y, 22, row.height);
		Rect upRect = new Rect(downRect.x - 26, row.y, 22, row.height);
		Rect arrowRect = new Rect(row.x, row.y, 14, row.height);
		Rect nameRect = new Rect(arrowRect.xMax + 2, row.y, upRect.x - arrowRect.xMax - 6, row.height);

		element.isExpanded = UnityEditor.EditorGUI.Foldout(arrowRect, element.isExpanded, GUIContent.none, true);

		var nameProp = element.FindPropertyRelative("name");
		if (nameProp != null) {
			var nameStyle = new GUIStyle(UnityEditor.EditorStyles.textField);
			nameStyle.fontStyle = FontStyle.Bold;
			nameProp.stringValue = UnityEditor.EditorGUI.TextField(nameRect, nameProp.stringValue, nameStyle);
		} else {
			UnityEditor.EditorGUI.LabelField(nameRect, $"Subsection {index}");
		}

		SubsectionAction action = SubsectionAction.None;

		GUI.backgroundColor = Color.white;
		UnityEditor.EditorGUI.BeginDisabledGroup(index == 0);
		if (GUI.Button(upRect, "▲")) action = SubsectionAction.MoveUp;
		UnityEditor.EditorGUI.EndDisabledGroup();
		UnityEditor.EditorGUI.BeginDisabledGroup(index == count - 1);
		if (GUI.Button(downRect, "▼")) action = SubsectionAction.MoveDown;
		UnityEditor.EditorGUI.EndDisabledGroup();

		GUI.backgroundColor = new Color(0.3f, 1f, 0.3f);
		if (GUI.Button(playRect, "▶")) {
			UnityEditor.SessionState.SetInt("startSection", (int)selected);
			UnityEditor.SessionState.SetInt("startSubsection", index);
			if (UnityEditor.EditorApplication.isPlaying) {
				UnityEditor.EditorApplication.playModeStateChanged += RestartAfterExit;
				UnityEditor.EditorApplication.ExitPlaymode();
			} else {
				UnityEditor.EditorApplication.EnterPlaymode();
			}
		}

		GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
		if (GUI.Button(deleteRect, "×")) action = SubsectionAction.Delete;
		GUI.backgroundColor = Color.white;

		if (element.isExpanded) {
			UnityEditor.EditorGUI.indentLevel++;
			var end = element.GetEndProperty();
			var child = element.Copy();
			bool enter = true;
			while (child.NextVisible(enter) && !UnityEditor.SerializedProperty.EqualContents(child, end)) {
				enter = false;
				if (child.name == "name") continue;
				UnityEditor.EditorGUILayout.PropertyField(child, true);
			}
			UnityEditor.EditorGUI.indentLevel--;
			UnityEditor.EditorGUILayout.Space(2);
		}

		return action;
	}

	static void RestartAfterExit(UnityEditor.PlayModeStateChange state) {
		if (state != UnityEditor.PlayModeStateChange.EnteredEditMode) return;
		UnityEditor.EditorApplication.playModeStateChanged -= RestartAfterExit;
		UnityEditor.EditorApplication.EnterPlaymode();
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
