using UnityEditor;
using UnityEngine;

// Hierarchy right-click -> "Reset Scale (Keep Children)".
// Sets the GameObject's localScale to (1,1,1) while every child keeps its world
// transform. Direct children are briefly reparented with worldPositionStays, so
// Unity recomputes their local values exactly as it does when you drag in the
// Hierarchy — the whole subtree stays put in world space.
static class ResetScaleKeepChildren
{
    // Trailing " %#r" binds the shortcut: % = Ctrl (Cmd on macOS), # = Shift, r = R.
    // Both MenuItem strings must stay identical (incl. the hotkey) so the validator matches.
    const string MENU = "GameObject/Reset Scale (Keep Children) %#r";
    const string UNDO = "Reset Scale (Keep Children)";

    static Transform[] Targets() =>
        Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable | SelectionMode.ExcludePrefab);

    [MenuItem(MENU, false, 0)]
    static void Apply()
    {
        // No MenuCommand parameter, so Unity runs this once for the whole selection.
        Transform[] targets = Targets();
        if (targets.Length == 0) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        foreach (Transform t in targets)
        {
            if (t.localScale == Vector3.one) continue;

            // Snapshot the direct children; their own subtrees follow them.
            Transform[] children = new Transform[t.childCount];
            for (int i = 0; i < children.Length; i++) children[i] = t.GetChild(i);

            // Detach (preserving world transform) so resetting the parent's scale
            // can't move or rescale them.
            foreach (Transform c in children)
                Undo.SetTransformParent(c, t.parent, true, UNDO);

            Undo.RecordObject(t, UNDO);
            t.localScale = Vector3.one;

            // Reattach (preserving world transform); Unity recomputes each child's
            // local scale against the parent's new (1,1,1) scale.
            foreach (Transform c in children)
                Undo.SetTransformParent(c, t, true, UNDO);
        }

        Undo.SetCurrentGroupName(UNDO);
        Undo.CollapseUndoOperations(group);
    }

    [MenuItem(MENU, true)]
    static bool Validate() => Targets().Length > 0;
}
