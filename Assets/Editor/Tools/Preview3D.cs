using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// WhiteRabbit/3D Preview -- toggles the editor between previewing the 3D pass and
// the 2D pass outside Play mode.
//
// Two URP facts make this necessary (see UniversalRenderPipeline.cs):
//  - Scene View cameras are hardcoded to volumeLayerMask = 1 ("Default" only) --
//    a Volume only previews there if it sits on that exact layer.
//  - Scene View cameras always render through the pipeline asset's *default*
//    renderer (GetRenderer() ignores any camera's own SetRenderer() for
//    CameraType.SceneView).
// So "previewing" a pass means making it the default renderer AND moving its
// Volume onto Default, while moving the other pass's Volume off of Default so it
// doesn't also leak into the Scene View preview.
static class Preview3D
{
    const string MENU = "WhiteRabbit/3D Preview";
    const string AssetPath = "Assets/Settings/PC_RPAsset.asset";
    const string PropName = "m_DefaultRendererIndex";

    const string Volume3DName = "3DVolume";
    const string Volume2DName = "2DVolume";

    [MenuItem(MENU, false, 42)]
    static void Toggle()
    {
        SerializedProperty prop = GetProperty(out UniversalRenderPipelineAsset asset, out SerializedObject so);
        if (prop == null)
        {
            Log.Error($"Could not find '{PropName}' on the PC_RP asset at {AssetPath}.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        Undo.RecordObject(asset, "Toggle 3D Preview");
        prop.intValue = prop.intValue == 0 ? 1 : 0;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        bool preview3D = prop.intValue == 1;
        SetVolumeLayers(preview3D);

        Undo.SetCurrentGroupName("Toggle 3D Preview");
        Undo.CollapseUndoOperations(group);

        Log.Info(preview3D
            ? $"3D Preview ON: default renderer = 1, '{Volume3DName}' -> Default, '{Volume2DName}' -> PP2D."
            : $"3D Preview OFF: default renderer = 0, '{Volume2DName}' -> Default, '{Volume3DName}' -> PP3D.");
    }

    static void SetVolumeLayers(bool preview3D)
    {
        int defaultLayer = LayerMask.NameToLayer("Default");
        int pp2D = LayerMask.NameToLayer("PP2D");
        int pp3D = LayerMask.NameToLayer("PP3D");

        SetLayer(GameObject.Find(Volume3DName), Volume3DName, preview3D ? defaultLayer : pp3D);
        SetLayer(GameObject.Find(Volume2DName), Volume2DName, preview3D ? pp2D : defaultLayer);
    }

    static void SetLayer(GameObject go, string expectedName, int layer)
    {
        if (go == null)
        {
            Log.Warn($"'{expectedName}' not found in the loaded scene(s); its layer was not changed.");
            return;
        }
        Undo.RecordObject(go, "Toggle 3D Preview");
        go.layer = layer;
        EditorUtility.SetDirty(go);
        if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
    }

    [MenuItem(MENU, true)]
    static bool Validate()
    {
        SerializedProperty prop = GetProperty(out _, out _);
        Menu.SetChecked(MENU, prop != null && prop.intValue == 1);
        return prop != null;
    }

    static SerializedProperty GetProperty(out UniversalRenderPipelineAsset asset, out SerializedObject serializedObject)
    {
        asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
        if (asset == null)
        {
            serializedObject = null;
            return null;
        }
        serializedObject = new SerializedObject(asset);
        return serializedObject.FindProperty(PropName);
    }
}