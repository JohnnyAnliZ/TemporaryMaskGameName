using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine;
public class CameraGateFeature : ScriptableRendererFeature
{
    public Material CompositeMaterial;
    private CameraGatePass pass;
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pass);
    }

    public override void Create()
    {
        pass = new CameraGatePass(CompositeMaterial);
        pass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
    }

}
