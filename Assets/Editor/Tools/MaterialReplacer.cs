using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Material tooling for the loaded scene(s):
//  - Replace: swap every use of one material for another.
//  - Usage list: every material in the scene, expandable to the objects using it.
//    Click an object row to select+ping it; click a material row to select all its users.
// Its own file (stateful EditorWindow) rather than the MaterialHelpers menu-command
// bucket; the shared "Material Tools" submenu is joined via the MenuItem path.
public class MaterialReplacer : EditorWindow
{
    class Usage
    {
        public Material material;
        public List<Renderer> renderers = new List<Renderer>();
    }

    Material from;
    Material to;
    bool includeInactive = true;

    // Cache is derived from live scene objects, so it must never persist across a
    // domain reload as stale references — rebuild fresh instead.
    [System.NonSerialized] int count = -1;             // cached slot count for `from`; -1 = recompute
    [System.NonSerialized] List<Usage> usages;         // null = rebuild
    [System.NonSerialized] HashSet<Object> selectedSet;
    readonly HashSet<Material> expanded = new HashSet<Material>();
    Vector2 scroll;

    static readonly Color headerBg = new Color(0.5f, 0.5f, 0.5f, 0.1f);
    static readonly Color hoverBg = new Color(0.4f, 0.65f, 1f, 0.13f);
    static readonly Color selectedBg = new Color(0.24f, 0.48f, 0.90f, 0.25f);

    [MenuItem("WhiteRabbit/Material Tools/Replace Material", false, 26)]
    static void Open() => GetWindow<MaterialReplacer>("Replace Material");

    void OnEnable() => wantsMouseMove = true;         // for row hover highlighting
    void OnFocus() { count = -1; usages = null; }     // scene may have changed while unfocused
    void OnHierarchyChange() { count = -1; usages = null; Repaint(); }
    void OnProjectChange() { count = -1; usages = null; Repaint(); }  // material asset deleted / reimported
    void OnSelectionChange() => Repaint();            // keep the selected-row highlight live

    void OnGUI()
    {
        selectedSet = new HashSet<Object>(Selection.objects);
        DrawReplaceSection();
        EditorGUILayout.Space();
        DrawUsageSection();
    }

    // ---- Replace ------------------------------------------------------------

    void DrawReplaceSection()
    {
        EditorGUILayout.HelpBox(
            "Swaps every renderer material slot in the loaded scene(s) that uses 'From' for 'To'. " +
            "Undoable. UI (Image/Graphic) materials are not affected.", MessageType.Info);

        EditorGUI.BeginChangeCheck();
        from = (Material)EditorGUILayout.ObjectField("From", from, typeof(Material), false);
        to = (Material)EditorGUILayout.ObjectField("To", to, typeof(Material), false);
        includeInactive = EditorGUILayout.Toggle(
            new GUIContent("Include Inactive", "Also include disabled GameObjects / renderers."), includeInactive);
        if (EditorGUI.EndChangeCheck()) { count = -1; usages = null; }

        if (from == null)
            EditorGUILayout.LabelField("Assign a 'From' material to scan.");
        else
        {
            if (count < 0) count = CountSlots();
            EditorGUILayout.LabelField($"{count} slot(s) in the loaded scene(s) use '{from.name}'.");
        }

        if (from != null && from == to)
            EditorGUILayout.HelpBox("'From' and 'To' are the same material.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(from == null || to == null || from == to))
            if (GUILayout.Button("Replace", GUILayout.Height(26)))
                Replace();
    }

    void Replace()
    {
        int changedSlots = 0, changedRenderers = 0;
        HashSet<Scene> dirtyScenes = new HashSet<Scene>();

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        foreach (Renderer r in LoadedRenderers())
        {
            Material[] mats = r.sharedMaterials; // copy; assign back to apply
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != from) continue;
                mats[i] = to;
                changed = true;
                changedSlots++;
            }
            if (!changed) continue;

            Undo.RecordObject(r, "Replace Material");
            r.sharedMaterials = mats;
            EditorUtility.SetDirty(r);
            if (r.gameObject.scene.IsValid()) dirtyScenes.Add(r.gameObject.scene);
            changedRenderers++;
        }

        Undo.SetCurrentGroupName($"Replace Material '{from.name}' -> '{to.name}'");
        Undo.CollapseUndoOperations(group);

        foreach (Scene s in dirtyScenes) EditorSceneManager.MarkSceneDirty(s);
        count = -1;
        usages = null;

        if (changedSlots == 0)
            Log.Warn($"No renderers in the loaded scene(s) use '{from.name}'. Nothing changed.");
        else
            Log.Info($"Replaced '{from.name}' -> '{to.name}' in {changedSlots} slot(s) on {changedRenderers} renderer(s).");
    }

    // ---- Usage list ---------------------------------------------------------

    void DrawUsageSection()
    {
        if (usages == null) RebuildUsages();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Materials in loaded scene(s): {usages.Count}", EditorStyles.boldLabel);
        if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(60))) RebuildUsages();
        EditorGUILayout.EndHorizontal();

        if (usages.Count == 0)
        {
            EditorGUILayout.HelpBox("No materials found on renderers in the loaded scene(s).", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Click a material to select all its objects · click an object to select it", EditorStyles.miniLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            foreach (Usage u in usages) DrawUsage(u);
        }
        finally
        {
            EditorGUILayout.EndScrollView(); // keep layout balanced even if a row throws
        }
    }

    void DrawUsage(Usage u)
    {
        // Material may have been destroyed since the list was built (asset deleted, etc.).
        // Unity's overloaded == catches destroyed objects; skip and let a rebuild prune it.
        if (u.material == null) return;

        Event e = Event.current;
        float h = EditorGUIUtility.singleLineHeight + 4;
        Rect row = EditorGUILayout.GetControlRect(false, h);

        if (e.type == EventType.Repaint) EditorGUI.DrawRect(row, headerBg);

        Rect arrow = new Rect(row.x + 2, row.y + 2, 14, EditorGUIUtility.singleLineHeight);
        bool exp = expanded.Contains(u.material);
        bool now = EditorGUI.Foldout(arrow, exp, GUIContent.none, true);
        if (now != exp) { if (now) expanded.Add(u.material); else expanded.Remove(u.material); }

        Rect countRect = new Rect(row.xMax - 54, row.y, 50, h);
        Rect nameRect = new Rect(arrow.xMax + 2, row.y, countRect.x - arrow.xMax - 6, h);
        Rect clickable = new Rect(nameRect.x, row.y, row.xMax - nameRect.x, h);

        if (e.type == EventType.Repaint && clickable.Contains(e.mousePosition))
        {
            EditorGUI.DrawRect(clickable, hoverBg);
            Repaint();
        }

        GUI.Label(nameRect, u.material.name, EditorStyles.boldLabel);
        GUI.Label(countRect, u.renderers.Count.ToString(), RightMini);

        if (e.type == EventType.MouseDown && e.button == 0 && clickable.Contains(e.mousePosition))
        {
            SelectAll(u);
            e.Use();
        }

        if (!exp) return;
        foreach (Renderer r in u.renderers)
            if (r != null) DrawObjectRow(r);
    }

    void DrawObjectRow(Renderer r)
    {
        Event e = Event.current;
        Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        Rect area = new Rect(row.x + 22, row.y, row.width - 22, row.height);

        if (e.type == EventType.Repaint)
        {
            if (selectedSet.Contains(r.gameObject)) EditorGUI.DrawRect(area, selectedBg);
            else if (area.Contains(e.mousePosition)) { EditorGUI.DrawRect(area, hoverBg); Repaint(); }
        }

        GUI.Label(new Rect(area.x + 4, area.y, area.width - 4, area.height), r.gameObject.name);

        if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition))
        {
            Selection.activeGameObject = r.gameObject;
            EditorGUIUtility.PingObject(r.gameObject);
            e.Use();
        }
    }

    void SelectAll(Usage u)
    {
        Object[] gos = u.renderers.Where(r => r != null).Select(r => (Object)r.gameObject).ToArray();
        Selection.objects = gos;
        if (gos.Length > 0) EditorGUIUtility.PingObject(gos[0]);
    }

    // ---- Scene scan ---------------------------------------------------------

    // FindObjectsByType searches loaded scenes only (not prefab assets or unloaded scenes),
    // which is exactly "the level".
    Renderer[] LoadedRenderers() => Object.FindObjectsByType<Renderer>(
        includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
        FindObjectsSortMode.None);

    int CountSlots()
    {
        int n = 0;
        foreach (Renderer r in LoadedRenderers())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] == from) n++;
        }
        return n;
    }

    void RebuildUsages()
    {
        Dictionary<Material, Usage> map = new Dictionary<Material, Usage>();
        HashSet<Material> seen = new HashSet<Material>();
        foreach (Renderer r in LoadedRenderers())
        {
            seen.Clear();
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !seen.Add(m)) continue; // skip null + duplicate slots on same renderer
                if (!map.TryGetValue(m, out Usage u))
                {
                    u = new Usage { material = m };
                    map[m] = u;
                }
                u.renderers.Add(r);
            }
        }
        usages = map.Values.OrderBy(u => u.material.name).ToList();
    }

    GUIStyle _rightMini;
    GUIStyle RightMini => _rightMini ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
}