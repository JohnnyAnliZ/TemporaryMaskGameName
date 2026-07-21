using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

public class UltimateUnusedAssetsCleaner : EditorWindow
{
    // Class, not struct: rows are mutated in place (selection) while iterating the list,
    // and reference semantics keep selection intact across re-sorts.
    private class AssetRow
    {
        public string path;
        public long size;
        public bool selected;
        public bool used;
    }

    private List<AssetRow> assets = new List<AssetRow>();
    private long totalSize;
    private Vector2 scrollPos;
    private bool scanAllScenes = true;
    private bool ignoreScripts = true;
    private string excludedFolders = "_Recovery";
    private int listMode; // 0 = unused only, 1 = all assets
    private bool sortBySize;
    private int lastClickedIndex = -1;
    private bool dragSelecting;
    private bool dragSelectValue;

    [MenuItem("WhiteRabbit/Unused Assets", false, 21)]
    public static void ShowWindow()
    {
        GetWindow<UltimateUnusedAssetsCleaner>("Unused Assets");
    }

    private void OnGUI()
    {
        GUILayout.Label("Find Unused Assets", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Uses deep dependency tracking and raw text GUID scanning to protect Project Settings, URP assets, Addressables, and shaders loaded by string via Shader.Find().", MessageType.Info);
        
        EditorGUILayout.Space();
        
        listMode = GUILayout.Toolbar(listMode, new[] { "Unused Only", "All Assets" });

        EditorGUILayout.Space();

        scanAllScenes = EditorGUILayout.Toggle(new GUIContent("Scan All Scenes", "If false, only scans scenes enabled in the Build Settings."), scanAllScenes);
        ignoreScripts = EditorGUILayout.Toggle(new GUIContent("Ignore Scripts (.cs)", "Safest to keep checked to avoid breaking reflection/string instantiations."), ignoreScripts);
        excludedFolders = EditorGUILayout.TextField(new GUIContent("Exclude Folders", "Comma-separated folder names. Nothing inside them counts as a root, so stale scenes (crash-recovery backups, imported samples) can't keep dead assets alive. Matched anywhere in the path."), excludedFolders);

        EditorGUILayout.Space();

        if (GUILayout.Button(listMode == 0 ? "Scan for Unused Assets" : "Scan All Assets", GUILayout.Height(30)))
        {
            ScanAssets();
        }

        if (assets.Count > 0)
        {
            int selectedCount = 0;
            int selectedUsed = 0;
            long selectedBytes = 0;
            foreach (var a in assets)
            {
                if (!a.selected) continue;

                selectedCount++;
                selectedBytes += a.size;
                if (a.used) selectedUsed++;
            }

            EditorGUILayout.Space();
            GUILayout.Label($"{assets.Count} {(listMode == 0 ? "unused assets" : "assets")} ({FormatSize(totalSize)}):", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", GUILayout.Width(80))) SetAllSelected(true);
            if (GUILayout.Button("Select None", GUILayout.Width(80))) SetAllSelected(false);

            // A view option, so it belongs with the results — not with the pre-scan toggles.
            EditorGUI.BeginChangeCheck();
            sortBySize = GUILayout.Toggle(sortBySize, "Sort by size", EditorStyles.miniButton, GUILayout.Width(90));
            if (EditorGUI.EndChangeCheck()) SortAssets();

            GUILayout.FlexibleSpace();
            GUILayout.Label($"{selectedCount} selected ({FormatSize(selectedBytes)})");
            EditorGUILayout.EndHorizontal();

            GUILayout.Label("Click a row to toggle · drag to paint · shift-click to select a range", EditorStyles.miniLabel);
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, "box");
            for (int i = 0; i < assets.Count; i++)
            {
                AssetRow asset = assets[i];

                // BeginHorizontal's rect is valid during Repaint, so the highlight can be drawn
                // here — behind the row contents that follow.
                Rect rowRect = EditorGUILayout.BeginHorizontal();
                if (asset.selected && Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.90f, 0.25f));
                }

                asset.selected = EditorGUILayout.Toggle(asset.selected, GUILayout.Width(18));
                GUILayout.Label(asset.path, GUILayout.ExpandWidth(true));
                if (listMode == 1)
                {
                    GUILayout.Label(asset.used ? "used" : "unused", EditorStyles.miniLabel, GUILayout.Width(45));
                }
                GUILayout.Label(FormatSize(asset.size), GUILayout.Width(70));
                if (GUILayout.Button("Locate", GUILayout.Width(60)))
                {
                    Object obj = AssetDatabase.LoadAssetAtPath<Object>(asset.path);
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                }
                EditorGUILayout.EndHorizontal();

                HandleRowInput(rowRect, i);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(selectedCount == 0))
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button($"Delete Selected ({selectedCount} — {FormatSize(selectedBytes)})", GUILayout.Height(30)))
                {
                    string warning = selectedUsed > 0
                        ? $"\n\nWARNING: {selectedUsed} of these are still referenced by the project — deleting them will break it."
                        : "";

                    if (EditorUtility.DisplayDialog("Delete Assets",
                        $"Delete {selectedCount} selected asset(s), reclaiming {FormatSize(selectedBytes)}?{warning}\n\nEnsure your project is committed to source control first.",
                        "Yes, Delete", "Cancel"))
                    {
                        DeleteSelectedAssets();
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }
        else if (assets.Capacity > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(listMode == 0 ? "No unused assets found! Your project is clean." : "No assets found.", MessageType.Info);
        }

        // rawType still reports MouseUp even when a control consumed the event.
        if (Event.current.rawType == EventType.MouseUp) dragSelecting = false;
    }

    // Row-level selection: a plain click toggles the row and starts a drag-paint, dragging applies
    // that same value to every row it crosses, and shift-click selects the range from the last click.
    // Clicks landing on the row's own checkbox or Locate button are consumed by those controls
    // first, so they never reach this.
    private void HandleRowInput(Rect rowRect, int index)
    {
        Event e = Event.current;
        if (!rowRect.Contains(e.mousePosition)) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (e.shift && lastClickedIndex >= 0)
            {
                int from = Mathf.Min(lastClickedIndex, index);
                int to = Mathf.Max(lastClickedIndex, index);
                for (int i = from; i <= to; i++) assets[i].selected = true;
            }
            else
            {
                dragSelectValue = !assets[index].selected;
                assets[index].selected = dragSelectValue;
                dragSelecting = true;
                lastClickedIndex = index;
            }
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseDrag && dragSelecting)
        {
            if (assets[index].selected != dragSelectValue)
            {
                assets[index].selected = dragSelectValue;
                Repaint();
            }
            e.Use();
        }
    }

    private void ScanAssets()
    {
        assets.Clear();
        assets.Capacity = 1; 
        totalSize = 0;
        List<string> roots = new List<string>();
        string[] allPaths = AssetDatabase.GetAllAssetPaths();

        EditorUtility.DisplayProgressBar("Scanning", "Gathering root dependencies...", 0.2f);

        // 1. Gather Scenes
        if (scanAllScenes)
        {
            roots.AddRange(AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath));
        }
        else
        {
            roots.AddRange(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
        }

        // 2. Gather dynamically loaded folders and Addressables
        foreach (string path in allPaths)
        {
            if (!path.StartsWith("Assets/")) continue;
            
            if (path.Contains("/Resources/") || path.Contains("/AddressableAssetsData/"))
            {
                roots.Add(path);
            }
        }

        // 2.5 Code-referenced shaders: Shader.Find("Name") loads a shader by string, leaving
        // no GUID for the dependency graph to follow. Scan scripts for Shader.Find literals and
        // root whatever shader asset each name resolves to (Shader.Find works in-editor too).
        EditorUtility.DisplayProgressBar("Scanning", "Scanning scripts for Shader.Find() references...", 0.3f);
        foreach (string scriptPath in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(scriptPath), "Shader\\.Find\\s*\\(\\s*\"([^\"]+)\"\\s*\\)"))
            {
                Shader shader = Shader.Find(m.Groups[1].Value);
                if (shader == null) continue;
                string shaderPath = AssetDatabase.GetAssetPath(shader);
                if (!string.IsNullOrEmpty(shaderPath) && shaderPath.StartsWith("Assets/"))
                {
                    roots.Add(shaderPath);
                }
            }
        }

        // 3. Bulletproof Project Settings Shield (Raw Text GUID Search)
        EditorUtility.DisplayProgressBar("Scanning", "Parsing Project Settings YAML...", 0.4f);
        if (Directory.Exists("ProjectSettings"))
        {
            string[] projectSettingsFiles = Directory.GetFiles("ProjectSettings", "*.*", SearchOption.AllDirectories);
            HashSet<string> projectSettingsText = new HashSet<string>();

            // Read all settings files into memory
            foreach (var file in projectSettingsFiles)
            {
                projectSettingsText.Add(File.ReadAllText(file));
            }

            // Check every asset in the project. If its GUID is inside ANY project setting text, it's a root.
            foreach (string path in allPaths)
            {
                if (!path.StartsWith("Assets/")) continue;
                
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid) && projectSettingsText.Any(text => text.Contains(guid)))
                {
                    roots.Add(path);
                }
            }
        }

        // 3.5 Excluded folders never act as roots. Without this, a stale crash-recovery scene
        // still referencing old geometry keeps hundreds of MB of dead assets alive. Their own
        // contents stay listable, so the junk itself shows up as unused and can be cleaned out.
        string[] excluded = excludedFolders.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        if (excluded.Length > 0) roots.RemoveAll(p => IsExcluded(p, excluded));

        // 4. Build the master dependency graph
        EditorUtility.DisplayProgressBar("Scanning", "Calculating dependency graph...", 0.7f);
        string[] dependencies = AssetDatabase.GetDependencies(roots.ToArray(), true);
        HashSet<string> usedAssetsSet = new HashSet<string>(dependencies);

        // 5. Filter and isolate the unused assets
        EditorUtility.DisplayProgressBar("Scanning", "Filtering unused assets...", 0.9f);
        foreach (string path in allPaths)
        {
            if (!path.StartsWith("Assets/")) continue;
            if (AssetDatabase.IsValidFolder(path)) continue; 

            // Hard Exclusions
            if (path.Contains("/Editor/") || path.Contains("/Plugins/") || path.Contains("/StreamingAssets/")) continue; 
            if (ignoreScripts && path.EndsWith(".cs")) continue;
            
            // "All Assets" keeps used entries too, tagged so they stand out in the list.
            bool isUsed = usedAssetsSet.Contains(path);
            if (isUsed && listMode == 0) continue;

            // Size on disk, read once here rather than in OnGUI (which repaints constantly).
            long size = 0;
            try { size = new FileInfo(path).Length; } catch { }

            assets.Add(new AssetRow { path = path, size = size, used = isUsed });
            totalSize += size;
        }

        SortAssets();

        EditorUtility.ClearProgressBar();
    }

    // GetAllAssetPaths returns import/GUID order, so the list always needs sorting. Size ties
    // break on path to keep the order deterministic (List.Sort is unstable, and a lot of assets
    // share a size — 0-byte entries especially).
    private void SetAllSelected(bool value)
    {
        foreach (var asset in assets) asset.selected = value;
    }

    private void SortAssets()
    {
        // Rows move, so any anchor held for shift-range selection is stale.
        lastClickedIndex = -1;

        if (sortBySize)
        {
            assets.Sort((a, b) =>
            {
                int bySize = b.size.CompareTo(a.size);
                return bySize != 0 ? bySize : System.StringComparer.OrdinalIgnoreCase.Compare(a.path, b.path);
            });
        }
        else
        {
            assets.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.path, b.path));
        }
    }

    private void DeleteSelectedAssets()
    {
        HashSet<string> deletedPaths = new HashSet<string>();
        long deletedBytes = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var asset in assets)
            {
                if (!asset.selected) continue;

                if (AssetDatabase.DeleteAsset(asset.path))
                {
                    deletedPaths.Add(asset.path);
                    deletedBytes += asset.size;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }
        
        // Drop only what actually deleted; unselected rows are still unused, so keep listing them.
        assets.RemoveAll(a => deletedPaths.Contains(a.path));
        totalSize -= deletedBytes;
        lastClickedIndex = -1;
        Log.Info($"Deleted {deletedPaths.Count} unused assets, reclaiming {FormatSize(deletedBytes)}.");
    }

    private static bool IsExcluded(string path, string[] excluded)
    {
        foreach (string folder in excluded)
        {
            if (path.IndexOf("/" + folder + "/", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1L << 30) return $"{bytes / (float)(1L << 30):F2} GB";
        if (bytes >= 1L << 20) return $"{bytes / (float)(1L << 20):F1} MB";
        if (bytes >= 1L << 10) return $"{bytes / (float)(1L << 10):F0} KB";
        return $"{bytes} B";
    }
}