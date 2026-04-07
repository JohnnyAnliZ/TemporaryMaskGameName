using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class FixmeScannerWindow : EditorWindow {
	struct FixmeEntry {
		public string filePath;
		public int lineNumber;
		public string message;
	}

	static readonly Regex fixmePattern = new Regex(@"//FIXME:\s*(.*)$", RegexOptions.Compiled);
	List<FixmeEntry> fixmeEntries = new List<FixmeEntry>();
	Vector2 fixmeScrollPos;

	[Serializable]
	class TodoItem {
		public string id;
		public string text;
		public bool done;
		public int urgency;
		public int user;
		public string createdAt;
	}
	[Serializable]
	class TodoData {
		public List<TodoItem> items = new List<TodoItem>();
	}

	static readonly string SavePath = "ProjectSettings/TodoTracker.json";
	//default is to serialize because of window base class, will crash otherwise
	[NonSerialized] TodoData todoData = new TodoData();
	[NonSerialized] string newItemText = "";
	[NonSerialized] Vector2 todoScrollPos;
	[NonSerialized] string deleteId = null;
	[NonSerialized] string editingId = null;
	[NonSerialized] string editingText = "";
	[NonSerialized] bool editFocusPending;

	[NonSerialized] int sortMode = 0;
	static readonly string[] sortLabels = { "Date", "Todo", "Urgency", "User" };
	static readonly Color[] urgencyColors = { Color.white, Color.yellow, new Color(1f, 0.3f, 0.3f) };
	static readonly string[] userNames = { "Alex", "Jasper", "Jerry", "Johnny", "Lucy" };

	static readonly Color panelBg = new Color(0f, 0f, 0.015f, 0.15f);
	static readonly Color rowAlt = new Color(1f, 1f, 1f, 0.024f);
	static readonly Color hoverColor = new Color(0.4f, 0.65f, 1f, 0.13f);
	static readonly Color tabBarBg = new Color(0f, 0f, 0f, 0.18f);
	static readonly Color toolbarBg = new Color(0f, 0f, 0f, 0.10f);
	static readonly Color fileHeaderBg = new Color(0.4f, 0.65f, 1f, 0.06f);
	static readonly Color addFieldBg = new Color(0f, 0f, 0f, 0.06f);
	static readonly Color deleteBtnColor = new Color(1f, 0.4f, 0.4f);

	//Tabs-------------------------------------------------------------------------
	int activeTab = 0;
	static readonly string[] tabLabels = { "TODO", "FIXME" };

	[MenuItem("Tools/White Rabbit")]
	static void Open() {
		GetWindow<FixmeScannerWindow>("White Rabbit");
	}

	void OnEnable() {
		wantsMouseMove = true;
		ScanFixmes();
		LoadTodos();
	}
	void OnDisable() {
		SaveTodos();
	}

	void OnGUI() {
		// Window background tint
		EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), panelBg);

		// Tab bar with background
		Rect tabRect = EditorGUILayout.GetControlRect(false, 22);
		EditorGUI.DrawRect(tabRect, tabBarBg);
		activeTab = GUI.Toolbar(tabRect, activeTab, tabLabels);

		EditorGUILayout.Space(2);
		if (activeTab == 0) DrawTodoTab();
		else DrawFixmeTab();
	}

	//FIXME-------------------------------------------------------------------
	void ScanFixmes() {
		fixmeEntries.Clear();
		string assetsPath = Application.dataPath;
		string[] files = Directory.GetFiles(assetsPath, "*.cs", SearchOption.AllDirectories);
		foreach (string file in files) {
			if (file.EndsWith("FixmeScannerWindow.cs")) continue;
			string[] lines;
			try { lines = File.ReadAllLines(file); }
			catch { continue; }
			for (int i = 0; i < lines.Length; i++) {
				Match m = fixmePattern.Match(lines[i]);
				if (!m.Success) continue;
				fixmeEntries.Add(new FixmeEntry {
					filePath = file,
					lineNumber = i + 1,
					message = m.Groups[1].Value.Trim()
				});
			}
		}
	}

	void DrawFixmeTab() {
		// Toolbar
		Rect tbRect = EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		if (Event.current.type == EventType.Repaint)
			EditorGUI.DrawRect(tbRect, toolbarBg);
		if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
			ScanFixmes();
		GUILayout.FlexibleSpace();
		EditorGUILayout.LabelField(fixmeEntries.Count + " items", EditorStyles.miniLabel, GUILayout.Width(60));
		EditorGUILayout.EndHorizontal();

		fixmeScrollPos = EditorGUILayout.BeginScrollView(fixmeScrollPos);
		string lastFile = null;
		int rowIndex = 0;
		foreach (FixmeEntry entry in fixmeEntries) {
			string rel = "Assets" + entry.filePath.Substring(Application.dataPath.Length).Replace('\\', '/');
			if (rel != lastFile) {
				// File header with accent background
				Rect headerRect = EditorGUILayout.BeginHorizontal();
				if (Event.current.type == EventType.Repaint)
					EditorGUI.DrawRect(headerRect, fileHeaderBg);
				EditorGUILayout.LabelField(rel, EditorStyles.boldLabel);
				EditorGUILayout.EndHorizontal();
				lastFile = rel;
				rowIndex = 0;
			}
			Rect rowRect = EditorGUILayout.BeginHorizontal();
			if (Event.current.type == EventType.Repaint) {
				if (rowIndex % 2 == 1)
					EditorGUI.DrawRect(rowRect, rowAlt);
				if (rowRect.Contains(Event.current.mousePosition)) {
					EditorGUI.DrawRect(rowRect, hoverColor);
					Repaint();
				}
			}
			GUILayout.Space(12);
			string label = "L" + entry.lineNumber + "  " + entry.message;
			if (GUILayout.Button(label, EditorStyles.linkLabel))
				OpenFileAtLine(entry.filePath, entry.lineNumber);
			EditorGUILayout.EndHorizontal();
			rowIndex++;
		}
		EditorGUILayout.EndScrollView();
	}

	void OpenFileAtLine(string absPath, int line) {
		string rel = "Assets" + absPath.Substring(Application.dataPath.Length).Replace('\\', '/');
		UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rel);
		if (obj != null)
			AssetDatabase.OpenAsset(obj, line);
	}

	//TODO--------------------------------------------------------------
	void LoadTodos() {
		if (!File.Exists(SavePath)) return;
		try {
			string json = File.ReadAllText(SavePath);
			todoData = JsonUtility.FromJson<TodoData>(json) ?? new TodoData();
		} catch {
			todoData = new TodoData();
		}
	}
	void SaveTodos() {
		string json = JsonUtility.ToJson(todoData, true);
		File.WriteAllText(SavePath, json);
	}

	void DrawTodoTab() {
		if (editingId != null && Event.current.type == EventType.MouseDown) {
			var editItem = todoData.items.Find(i => i.id == editingId);
			if (editItem != null) CommitEdit(editItem);
		}
		DrawTodoToolbar();
		DrawTodoAddField();
		DrawTodoItems();
		if (deleteId != null) {
			todoData.items.RemoveAll(i => i.id == deleteId);
			deleteId = null;
			SaveTodos();
		}
	}

	void DrawTodoToolbar() {
		Rect tbRect = EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		if (Event.current.type == EventType.Repaint)
			EditorGUI.DrawRect(tbRect, toolbarBg);
		EditorGUILayout.LabelField("Sort:", EditorStyles.miniLabel, GUILayout.Width(30));
		sortMode = EditorGUILayout.Popup(sortMode, sortLabels, EditorStyles.toolbarPopup, GUILayout.Width(80));
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
	}

	void DrawTodoAddField() {
		Rect addRect = EditorGUILayout.BeginHorizontal();
		if (Event.current.type == EventType.Repaint)
			EditorGUI.DrawRect(addRect, addFieldBg);
		bool enterPressed = Event.current.type == EventType.KeyDown
			&& Event.current.keyCode == KeyCode.Return
			&& GUI.GetNameOfFocusedControl() == "TodoInput";
		GUI.SetNextControlName("TodoInput");
		newItemText = EditorGUILayout.TextField(newItemText);
		bool submit = GUILayout.Button("+", GUILayout.Width(24)) || enterPressed;
		if (submit && !string.IsNullOrEmpty(newItemText.Trim())) {
			todoData.items.Insert(0, new TodoItem {
				id = Guid.NewGuid().ToString("N").Substring(0, 8),
				text = newItemText.Trim(),
				done = false,
				createdAt = DateTime.Now.ToString("yyyy-MM-dd")
			});
			newItemText = "";
			GUI.FocusControl(null);
			SaveTodos();
		}
		EditorGUILayout.EndHorizontal();
	}

	void DrawTodoItems() {
		List<TodoItem> sorted = new List<TodoItem>(todoData.items);
		switch (sortMode) {
			case 1: // Todo first, then done
				sorted.Sort((a, b) => {
					int cmp = a.done.CompareTo(b.done);
					return cmp != 0 ? cmp : string.Compare(b.createdAt, a.createdAt);
				});
				break;
			case 2: // Urgency high->low
				sorted.Sort((a, b) => {
					int cmp = b.urgency.CompareTo(a.urgency);
					return cmp != 0 ? cmp : string.Compare(b.createdAt, a.createdAt);
				});
				break;
			case 3: // User A->Z
				sorted.Sort((a, b) => {
					int cmp = a.user.CompareTo(b.user);
					return cmp != 0 ? cmp : string.Compare(b.createdAt, a.createdAt);
				});
				break;
		}

		todoScrollPos = EditorGUILayout.BeginScrollView(todoScrollPos);
		int rowIndex = 0;
		foreach (TodoItem item in sorted) {
			Rect rowRect = EditorGUILayout.BeginHorizontal();

			// Row background: alternating + hover
			if (Event.current.type == EventType.Repaint) {
				if (rowIndex % 2 == 1)
					EditorGUI.DrawRect(rowRect, rowAlt);
				if (rowRect.Contains(Event.current.mousePosition)) {
					EditorGUI.DrawRect(rowRect, hoverColor);
					Repaint();
				}
			}

			bool wasDone = item.done;
			item.done = EditorGUILayout.Toggle(item.done, GUILayout.Width(16));
			if (item.done != wasDone) SaveTodos();

			// User dropdown
			int newUser = EditorGUILayout.Popup(item.user, userNames, GUILayout.Width(62));
			if (newUser != item.user) {
				item.user = newUser;
				SaveTodos();
			}

			// Urgency dot -- click to cycle
			Color prevBg = GUI.backgroundColor;
			GUI.backgroundColor = urgencyColors[item.urgency];
			if (GUILayout.Button("", GUILayout.Width(14), GUILayout.Height(16))) {
				item.urgency = (item.urgency + 1) % 3;
				SaveTodos();
			}
			GUI.backgroundColor = prevBg;

			// Text -- click to edit inline
			bool editing = editingId == item.id;
			if (editing) {
				string controlName = "TodoEdit_" + item.id;
				bool commitOnEnter = !editFocusPending
					&& Event.current.type == EventType.KeyDown
					&& Event.current.keyCode == KeyCode.Return;
				GUI.SetNextControlName(controlName);
				editingText = EditorGUILayout.TextField(editingText);
				if (editFocusPending) {
					EditorGUI.FocusTextInControl(controlName);
					editFocusPending = false;
				} else if (commitOnEnter) {
					CommitEdit(item);
					GUI.FocusControl(null);
				} else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) {
					editingId = null;
					GUI.FocusControl(null);
					Event.current.Use();
				}
			} else {
				GUIStyle labelStyle = item.done ? DoneStyle() : EditorStyles.label;
				if (GUILayout.Button(item.text, labelStyle))
					StartEdit(item);
			}

			GUILayout.FlexibleSpace();
			string displayDate = item.createdAt != null && item.createdAt.Length >= 10
				? item.createdAt.Substring(5, 5) : "";
			EditorGUILayout.LabelField(displayDate, EditorStyles.miniLabel, GUILayout.Width(45));

			// Delete button with red tint
			Color prevColor = GUI.contentColor;
			GUI.contentColor = deleteBtnColor;
			if (GUILayout.Button("x", GUILayout.Width(20), GUILayout.Height(18)))
				deleteId = item.id;
			GUI.contentColor = prevColor;

			EditorGUILayout.EndHorizontal();
			rowIndex++;
		}
		EditorGUILayout.EndScrollView();
	}

	void StartEdit(TodoItem item) {
		editingId = item.id;
		editingText = item.text;
		editFocusPending = true;
	}
	void CommitEdit(TodoItem item) {
		string trimmed = editingText.Trim();
		if (!string.IsNullOrEmpty(trimmed) && trimmed != item.text) {
			item.text = trimmed;
			SaveTodos();
		}
		editingId = null;
		GUI.FocusControl(null);
	}

	GUIStyle _doneStyle;
	GUIStyle DoneStyle() {
		if (_doneStyle == null) {
			_doneStyle = new GUIStyle(EditorStyles.label);
			_doneStyle.fontStyle = FontStyle.Italic;
			_doneStyle.normal.textColor = Color.gray;
		}
		return _doneStyle;
	}
}
