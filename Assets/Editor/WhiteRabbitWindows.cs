using UnityEngine;
using UnityEditor;
using System.Linq;

//Must live here in editor assembly to persist across runtime assembly reloads
public class SectionsWindow : EditorWindow
{
	Section selected;
	Vector2 scroll;
	SerializedObject cachedSerializedAsset;
	SectionAsset cachedAsset;

	[MenuItem("WhiteRabbit/Sections", false, 0)]
	static void Open() => GetWindow<SectionsWindow>("Sections");

	void OnEnable() {
		foreach (Section s in System.Enum.GetValues(typeof(Section))) LoadOrCreateSectionAsset(s);
	}

	void OnGUI() {
		//Section picker toolbar
		EditorGUILayout.BeginHorizontal();
		foreach (Section s in System.Enum.GetValues(typeof(Section))) {
			bool isSel = s == selected;
			GUI.backgroundColor = isSel ? Color.darkBlue : Color.white;
			if (GUILayout.Button(s.ToString(), GUILayout.Height(28))) {
				selected = s;
			}
		}
		GUI.backgroundColor = Color.white;
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		//Subsections
		SectionAsset asset = LoadOrCreateSectionAsset(selected);
		if (cachedAsset != asset || cachedSerializedAsset == null) {
			cachedAsset = asset;
			cachedSerializedAsset = new SerializedObject(asset);
		}
		scroll = EditorGUILayout.BeginScrollView(scroll);
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
			Undo.RecordObject(asset, "Delete Subsection");
			asset.subsections.RemoveAt(pendingDelete);
			EditorUtility.SetDirty(asset);
			cachedSerializedAsset.Update();
		}
		if (pendingMoveFrom >= 0) {
			Undo.RecordObject(asset, "Reorder Subsection");
			var tmp = asset.subsections[pendingMoveFrom];
			asset.subsections[pendingMoveFrom] = asset.subsections[pendingMoveTo];
			asset.subsections[pendingMoveTo] = tmp;
			EditorUtility.SetDirty(asset);
			cachedSerializedAsset.Update();
		}
		cachedSerializedAsset.ApplyModifiedProperties();
		EditorGUILayout.EndScrollView();

		//Add subsection dropdown (auto-discovers concrete Subsection types)
		EditorGUILayout.Space();
		if (GUILayout.Button("+ Add Subsection")) {
			var menu = new GenericMenu();
			foreach (var t in System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
				.Where(t => typeof(Subsection).IsAssignableFrom(t) && !t.IsAbstract)) {
				System.Type capturedType = t;
				menu.AddItem(new GUIContent(t.Name), false, () => {
					Undo.RecordObject(asset, $"Add {capturedType.Name}");
					var instance = (Subsection)System.Activator.CreateInstance(capturedType);
					instance.name = capturedType.Name;
					asset.subsections.Add(instance);
					EditorUtility.SetDirty(asset);
				});
			}
			menu.ShowAsContext();
		}
	}

	enum SubsectionAction { None, Delete, MoveUp, MoveDown }

	SubsectionAction DrawSubsection(SerializedProperty element, int index, int count) {
		float lineH = EditorGUIUtility.singleLineHeight;
		Rect headerRect = EditorGUILayout.GetControlRect(false, lineH + 6);
		EditorGUI.DrawRect(headerRect, new Color(0.21f, 0.31f, 0.5f));

		Rect row = new Rect(headerRect.x + 6, headerRect.y + 3, headerRect.width - 12, lineH);
		Rect deleteRect = new Rect(row.xMax - 26, row.y, 26, row.height);
		Rect playRect = new Rect(deleteRect.x - 30, row.y, 26, row.height);
		Rect downRect = new Rect(playRect.x - 26, row.y, 22, row.height);
		Rect upRect = new Rect(downRect.x - 26, row.y, 22, row.height);
		Rect arrowRect = new Rect(row.x, row.y, 14, row.height);
		Rect nameRect = new Rect(arrowRect.xMax + 2, row.y, upRect.x - arrowRect.xMax - 6, row.height);

		element.isExpanded = EditorGUI.Foldout(arrowRect, element.isExpanded, GUIContent.none, true);

		var nameProp = element.FindPropertyRelative("name");
		if (nameProp != null) {
			var nameStyle = new GUIStyle(EditorStyles.textField);
			nameStyle.fontStyle = FontStyle.Bold;
			nameProp.stringValue = EditorGUI.TextField(nameRect, nameProp.stringValue, nameStyle);
		} else {
			EditorGUI.LabelField(nameRect, $"Subsection {index}");
		}

		SubsectionAction action = SubsectionAction.None;

		GUI.backgroundColor = Color.white;
		EditorGUI.BeginDisabledGroup(index == 0);
		if (GUI.Button(upRect, "▲")) action = SubsectionAction.MoveUp;
		EditorGUI.EndDisabledGroup();
		EditorGUI.BeginDisabledGroup(index == count - 1);
		if (GUI.Button(downRect, "▼")) action = SubsectionAction.MoveDown;
		EditorGUI.EndDisabledGroup();

		GUI.backgroundColor = new Color(0.3f, 1f, 0.3f);
		if (GUI.Button(playRect, "▶")) {
			SessionState.SetInt("startSection", (int)selected);
			SessionState.SetInt("startSubsection", index);
			if (EditorApplication.isPlaying) {
				EditorApplication.playModeStateChanged += RestartAfterExit;
				EditorApplication.ExitPlaymode();
			} else {
				EditorApplication.EnterPlaymode();
			}
		}

		GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
		if (GUI.Button(deleteRect, "×")) action = SubsectionAction.Delete;
		GUI.backgroundColor = Color.white;

		if (element.isExpanded) {
			EditorGUI.indentLevel++;
			var end = element.GetEndProperty();
			var child = element.Copy();
			bool enter = true;
			while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end)) {
				enter = false;
				if (child.name == "name") continue;
				EditorGUILayout.PropertyField(child, true);
			}
			EditorGUI.indentLevel--;
			EditorGUILayout.Space(2);
		}

		return action;
	}

	static void RestartAfterExit(PlayModeStateChange state) {
		if (state != PlayModeStateChange.EnteredEditMode) return;
		EditorApplication.playModeStateChanged -= RestartAfterExit;
		EditorApplication.EnterPlaymode();
	}

	static SectionAsset LoadOrCreateSectionAsset(Section section) {
		string resourcePath = $"Sections/Section_{section}";
		var asset = Resources.Load<SectionAsset>(resourcePath);
		if (asset != null) return asset;

		string dir = "Assets/Resources/Sections";
		if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
		asset = ScriptableObject.CreateInstance<SectionAsset>();
		asset.section = section;
		AssetDatabase.CreateAsset(asset, $"{dir}/Section_{section}.asset");
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		return asset;
	}
}

public class GlobalsWindow : EditorWindow
{
	Editor cachedEditor;
	Vector2 scroll;

	[MenuItem("WhiteRabbit/Globals", false, 1)]
	static void Open() => GetWindow<GlobalsWindow>("Globals");

	void OnGUI() {
		scroll = EditorGUILayout.BeginScrollView(scroll);
		Editor.CreateCachedEditor(Globals.Instance, null, ref cachedEditor);
		cachedEditor.OnInspectorGUI();
		EditorGUILayout.EndScrollView();
	}
}

public class ParallaxWindow : EditorWindow
{
	Vector2 scroll;

	[MenuItem("WhiteRabbit/Parallax Layers", false, 2)]
	static void Open() => GetWindow<ParallaxWindow>("Parallax Layers");

	void OnGUI() {
		Globals g = Globals.Instance;
		if (g.parallaxLayers == null) g.parallaxLayers = System.Array.Empty<Globals.ParallaxZFactor>();

		scroll = EditorGUILayout.BeginScrollView(scroll);

		g.parallaxScaleFactor = EditorGUILayout.Slider("Scale Factor", g.parallaxScaleFactor, 0f, 1f);
		EditorGUILayout.Space();

		int removeIndex = -1;
		for (int i = 0; i < g.parallaxLayers.Length; i++) {
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Z", GUILayout.Width(14));
			g.parallaxLayers[i].z = EditorGUILayout.FloatField(g.parallaxLayers[i].z, GUILayout.Width(60));
			g.parallaxLayers[i].factor = EditorGUILayout.Slider(g.parallaxLayers[i].factor, 0f, 2f);
			if (GUILayout.Button("-", GUILayout.Width(22))) removeIndex = i;
			EditorGUILayout.EndHorizontal();
		}

		if (removeIndex >= 0) {
			var list = new System.Collections.Generic.List<Globals.ParallaxZFactor>(g.parallaxLayers);
			list.RemoveAt(removeIndex);
			g.parallaxLayers = list.ToArray();
		}

		if (GUILayout.Button("Add Layer")) {
			var list = new System.Collections.Generic.List<Globals.ParallaxZFactor>(g.parallaxLayers);
			list.Add(new Globals.ParallaxZFactor { z = 0f, factor = 0.5f });
			g.parallaxLayers = list.ToArray();
		}

		EditorGUILayout.EndScrollView();
		if (GUI.changed) EditorUtility.SetDirty(g);
	}
}