using UnityEngine;

namespace Mirza.VFXToolKit
{
    // This script sets the emissive color of the material on the first child object
    // to match the colour of the Light component on the current GameObject.

    [ExecuteAlways]
    public class MVFXTK_LightColourMesh : MonoBehaviour
    {
        Light light;
        Material material;

        Renderer renderer;

        public float intensityScale = 1.0f;
        public bool executeInEditMode;

        void Start()
        {
            light = GetComponent<Light>();
            renderer = transform.GetChild(0).GetComponent<Renderer>();
        }

        void LateUpdate()
        {
            if (Application.isPlaying)
            {
                material = renderer.material;
            }
            else
            {
                if (!executeInEditMode)
                {
                    return;
                }

                material = renderer.sharedMaterial;
            }

            if (!material.IsKeywordEnabled("_EMISSION"))
            {
                material.EnableKeyword("_EMISSION");
            }

            float intensity = light.intensity * intensityScale;
            Color colour = light.color * intensity;

            material.SetColor("_EmissionColor", colour);
        }
    }
}