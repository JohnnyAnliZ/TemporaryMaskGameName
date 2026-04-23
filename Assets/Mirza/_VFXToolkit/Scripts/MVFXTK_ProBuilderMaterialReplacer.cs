using UnityEngine;

namespace Mirza.VFXToolKit
{
    // Executes in edit mode.

    // Attach to parent of any ProBuilder objects to replace their materials
    // Useful for replacing the default ProBuilder material with a custom one,
    // so on export the material is correct and not missing.

    // ProBuilder itself has a menu option to strip all its scripts from selection.

    // Use this in conjunction with that to prepare ProBuilder objects for export.
    // This way, PB won't be needed on import.

    // Simply delete/remove it from the GameObject when done.

    [ExecuteAlways]
    public class MVFXTK_ProBuilderMaterialReplacer : MonoBehaviour
    {
        public Material material;

        void Update()
        {
            MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();

            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshRenderer meshRenderer = meshRenderers[i];

                // If no material, or if material is default ProBuilder material, replace with specified material.

                if (!meshRenderer.sharedMaterial || meshRenderer.sharedMaterial.name == "ProBuilderDefault")
                {
                    meshRenderer.sharedMaterial = material;
                }
            }
        }
    }
}
