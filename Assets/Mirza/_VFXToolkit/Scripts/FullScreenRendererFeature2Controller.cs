using UnityEngine;

using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mirza.VFXToolKit
{
    public abstract class FullScreenRenderPassProcessor : MonoBehaviour
    {
        public abstract void OnExecutePass(FullScreenRendererFeature2 feature, ContextContainer frameData, RenderingData? renderingData);
    }

    [ExecuteAlways]
    public class FullScreenRendererFeature2Controller : MonoBehaviour
    {
        public FullScreenRendererFeature2 feature;
        public FullScreenRenderPassProcessor[] processors;

        void Start()
        {

        }

        void Update()
        {
            feature.settings.controller = this;
        }

        // Called by the assigned renderer feature.
        // RenderingData useful for compatibility mode.

        public void OnExecutePass(ContextContainer frameData, RenderingData? renderingData)
        {
            if (processors == null)
            {
                return;
            }

            for (int i = 0; i < processors.Length; i++)
            {
                processors[i].OnExecutePass(feature, frameData, renderingData);
            }
        }
    }
}
