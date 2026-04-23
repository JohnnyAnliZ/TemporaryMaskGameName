using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mirza.VFXToolKit
{
    [System.Serializable]
    public struct NamedFloat
    {
        public string name;

        [Range(0.0f, 1.0f)]
        public float value;
    }

    // This defines a custom VolumeComponent to be used with the Core VolumeFramework
    // (see core API docs https://docs.unity3d.com/Packages/com.unity.render-pipelines.core@latest/index.html?subfolder=/api/UnityEngine.Rendering.VolumeComponent.html)
    // (see URP docs https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/Volumes.html)
    //
    // After implementing this class you can:
    // * Tweak the default values for this VolumeComponent in the URP GlobalSettings
    // * Add overrides for this VolumeComponent to any local or global scene volume profiles
    //   (see URP docs https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/Volume-Profile.html)
    //   (see URP docs https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/VolumeOverrides.html)
    // * Access the blended values of this VolumeComponent from your ScriptableRenderPasses or scripts using the VolumeManager API
    //   (see core API docs https://docs.unity3d.com/Packages/com.unity.render-pipelines.core@latest/index.html?subfolder=/api/UnityEngine.Rendering.VolumeManager.html)
    // * Override the values for this volume per-camera by placing a VolumeProfile in a dedicated layer and setting the camera's "Volume Mask" to that layer
    //
    // Things to keep in mind:
    // * Be careful when renaming, changing types or removing public fields to not break existing instances of this class (note that this class inherits from ScriptableObject so the same serialization rules apply)
    // * The 'IPostProcessComponent' interface adds the 'IsActive()' method, which is currently not strictly necessary and is for your own convenience
    // * It is recommended to only expose fields that are expected to change. Fields which are constant such as shaders, materials or LUT textures
    //   should likely be in AssetBundles or referenced by serialized fields of your custom ScriptableRendererFeatures on used renderers so they would not get stripped during builds
    [VolumeComponentMenu("Post-processing Custom/NewPostProcessEffect")]
    [VolumeRequiresRendererFeatures(typeof(FullScreenRendererFeature2))]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class FullScreenRendererFeature2VolumeComponent : VolumeComponent, IPostProcessComponent
    {
        public FullScreenRendererFeature2VolumeComponent()
        {
            displayName = "NewPostProcessEffect";
        }

        //[Tooltip("Enter the description for the property that is shown when hovered")]
        //public ClampedFloatParameter intensity = new ClampedFloatParameter(1f, 0f, 1f);

        [System.Serializable]
        public sealed class NamedFloatArrayParameter : VolumeParameter<NamedFloat[]>
        {
            public NamedFloatArrayParameter(NamedFloat[] value, bool overrideState = false)
            : base(value, overrideState) { }

            public sealed override void Interp(NamedFloat[] from, NamedFloat[] to, float t)
            {
                for (int i = 0; i < from.Length; i++)
                {
                    float thisFrom = from[i].value;
                    float thisTo = to[i].value;

                    m_Value[i].value = thisFrom + (thisTo - thisFrom) * t;
                }
            }

        }

        //public NamedFloatArrayParameter floats;

        public bool IsActive()
        {
            return true;
            //return intensity.GetValue<float>() > 0.0f;
        }
    }
}