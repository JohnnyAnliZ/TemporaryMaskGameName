using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class MassFixMaterialsToURPLit
{
    [MenuItem("WhiteRabbit/Materials/Fix All Broken Materials To URP Lit", false, 60)]
    public static void FixAllBrokenMaterialsToURPLit()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

        if (urpLit == null)
        {
            Log.Error("Could not find URP Lit shader. Make sure URP is installed and active in Project Settings > Graphics.");
            return;
        }

        string[] materialGuids = AssetDatabase.FindAssets("t:Material");

        int fixedCount = 0;
        int skippedCount = 0;

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                skippedCount++;
                continue;
            }

            // Detect broken/purple/missing shader-ish materials.
            // You can make this broader by just setting every material to URP Lit.
            bool shaderMissingOrStandard =
                mat.shader == null ||
                mat.shader.name == "Hidden/InternalErrorShader" ||
                mat.shader.name == "Standard";

            if (shaderMissingOrStandard)
            {
                Undo.RecordObject(mat, "Fix Material Shader To URP Lit");

                mat.shader = urpLit;

                EditorUtility.SetDirty(mat);
                fixedCount++;

                Log.Info($"Fixed material: {path}");
            }
            else
            {
                skippedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Log.Info($"Material fix complete. Fixed: {fixedCount}, Skipped: {skippedCount}");
    }
}

public class FixMissingRendererMaterials
{
    private const string FallbackMaterialPath = "Assets/Recovered_URP_Lit_Fallback.mat";

    [MenuItem("WhiteRabbit/Materials/Fix Missing Renderer Material Slots In Open Scenes", false, 61)]
    public static void FixMissingSlotsInOpenScenes()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

        if (urpLit == null)
        {
            Log.Error("URP Lit shader still cannot be found. Fix URP before running this.");
            return;
        }

        Material fallback = GetOrCreateFallbackMaterial(urpLit);

        int renderersChecked = 0;
        int missingSlotsFound = 0;
        int relinkedFromGuid = 0;
        int replacedWithFallback = 0;

        // Cache scene text so we can read old serialized GUIDs.
        Dictionary<string, string> sceneTextCache = new Dictionary<string, string>();

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);

            if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
                continue;

            string sceneText = "";

            if (File.Exists(scene.path))
            {
                sceneText = File.ReadAllText(scene.path);
                sceneTextCache[scene.path] = sceneText;
            }

            GameObject[] roots = scene.GetRootGameObjects();

            foreach (GameObject root in roots)
            {
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

                foreach (Renderer renderer in renderers)
                {
                    renderersChecked++;

                    Material[] mats = renderer.sharedMaterials;
                    bool changed = false;

                    long rendererLocalId = GetLocalFileId(renderer);
                    List<string> serializedGuids = ExtractMaterialGuidsForRenderer(sceneText, rendererLocalId);

                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] != null)
                            continue;

                        missingSlotsFound++;

                        Material recovered = null;
                        string oldGuid = i < serializedGuids.Count ? serializedGuids[i] : "";

                        if (!string.IsNullOrEmpty(oldGuid) && oldGuid != "00000000000000000000000000000000")
                        {
                            string oldPath = AssetDatabase.GUIDToAssetPath(oldGuid);

                            if (!string.IsNullOrEmpty(oldPath))
                            {
                                recovered = AssetDatabase.LoadAssetAtPath<Material>(oldPath);

                                if (recovered != null)
                                {
                                    mats[i] = recovered;
                                    relinkedFromGuid++;
                                    changed = true;

                                    Log.Info(
                                        $"Relinked missing material slot.\n" +
                                        $"GameObject: {GetGameObjectPath(renderer.gameObject)}\n" +
                                        $"Slot: {i}\n" +
                                        $"GUID: {oldGuid}\n" +
                                        $"Path: {oldPath}"
                                    );
                                }
                            }
                            else
                            {
                                Log.Warn(
                                    $"Dead material GUID found; asset no longer exists in project.\n" +
                                    $"GameObject: {GetGameObjectPath(renderer.gameObject)}\n" +
                                    $"Slot: {i}\n" +
                                    $"Old GUID: {oldGuid}\n" +
                                    $"Replacing with fallback material."
                                );
                            }
                        }
                        else
                        {
                            Log.Warn(
                                $"Missing material slot has no readable GUID in scene text.\n" +
                                $"GameObject: {GetGameObjectPath(renderer.gameObject)}\n" +
                                $"Slot: {i}\n" +
                                $"Replacing with fallback material."
                            );
                        }

                        if (mats[i] == null)
                        {
                            mats[i] = fallback;
                            replacedWithFallback++;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        Undo.RecordObject(renderer, "Fix Missing Renderer Material Slots");
                        renderer.sharedMaterials = mats;
                        EditorUtility.SetDirty(renderer);
                        EditorSceneManager.MarkSceneDirty(scene);
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();

        Log.Info(
            $"Missing material scan complete.\n" +
            $"Renderers checked: {renderersChecked}\n" +
            $"Missing slots found: {missingSlotsFound}\n" +
            $"Relinked from GUID: {relinkedFromGuid}\n" +
            $"Replaced with fallback: {replacedWithFallback}"
        );
    }

    private static Material GetOrCreateFallbackMaterial(Shader urpLit)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(FallbackMaterialPath);

        if (existing != null)
            return existing;

        Material mat = new Material(urpLit);
        mat.name = "Recovered_URP_Lit_Fallback";

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", Color.white);

        AssetDatabase.CreateAsset(mat, FallbackMaterialPath);
        AssetDatabase.SaveAssets();

        Log.Info($"Created fallback material at {FallbackMaterialPath}");

        return mat;
    }

    private static long GetLocalFileId(Object obj)
    {
        // This works for scene/prefab objects in the editor.
        GlobalObjectId gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
        return (long)gid.targetObjectId;
    }

    private static List<string> ExtractMaterialGuidsForRenderer(string sceneText, long rendererLocalId)
    {
        List<string> guids = new List<string>();

        if (string.IsNullOrEmpty(sceneText) || rendererLocalId == 0)
            return guids;

        // Find the YAML block for this renderer:
        // --- !u!23 &123456789
        // MeshRenderer:
        //   ...
        //
        // Also works for SkinnedMeshRenderer because we search by local ID.
        string pattern =
            @"--- !u!\d+ &" + rendererLocalId + @"\s*\n(?<block>.*?)(?=\n--- !u!|\z)";

        Match match = Regex.Match(sceneText, pattern, RegexOptions.Singleline);

        if (!match.Success)
            return guids;

        string block = match.Groups["block"].Value;

        Match materialsMatch = Regex.Match(
            block,
            @"m_Materials:\s*\n(?<materials>(?:\s*-\s*\{.*?\}\s*\n?)+)",
            RegexOptions.Singleline
        );

        if (!materialsMatch.Success)
            return guids;

        string materialsBlock = materialsMatch.Groups["materials"].Value;

        MatchCollection guidMatches = Regex.Matches(
            materialsBlock,
            @"guid:\s*([a-fA-F0-9]{32})"
        );

        foreach (Match guidMatch in guidMatches)
        {
            guids.Add(guidMatch.Groups[1].Value);
        }

        return guids;
    }

    private static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}

public class DeepFindBrokenShaderReferences
{
    [MenuItem("WhiteRabbit/Materials/Deep Search For Internal Error Shader", false, 62)]
    public static void DeepSearch()
    {
        Shader errorShader = Shader.Find("Hidden/InternalErrorShader");

        int foundCount = 0;

        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();

        foreach (string path in allAssetPaths)
        {
            if (!path.StartsWith("Assets/") && !path.StartsWith("ProjectSettings/"))
                continue;

            Object[] assets;

            try
            {
                assets = AssetDatabase.LoadAllAssetsAtPath(path);
            }
            catch
            {
                continue;
            }

            foreach (Object asset in assets)
            {
                if (asset == null)
                    continue;

                SerializedObject so;

                try
                {
                    so = new SerializedObject(asset);
                }
                catch
                {
                    continue;
                }

                SerializedProperty prop = so.GetIterator();
                bool enterChildren = true;

                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    if (prop.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    Object obj = prop.objectReferenceValue;

                    if (obj == null)
                        continue;

                    if (obj is Shader shader)
                    {
                        if (shader.name == "Hidden/InternalErrorShader" ||
                            shader.name.Contains("InternalError") ||
                            shader.name.Contains("ErrorShader"))
                        {
                            foundCount++;

                            Log.Warn(
                                $"Found InternalErrorShader reference\n" +
                                $"Asset path: {path}\n" +
                                $"Asset object: {asset.name} ({asset.GetType().Name})\n" +
                                $"Property: {prop.propertyPath}"
                            );
                        }
                    }
                }
            }
        }

        Log.Info($"Deep search complete. Found: {foundCount}");
    }
}