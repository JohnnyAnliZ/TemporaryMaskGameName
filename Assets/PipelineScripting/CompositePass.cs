using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CameraGatePass : ScriptableRenderPass
{
    public Material mat;


    class PassData {
        public bool isBaseCamera;
        
    }


    public CameraGatePass(Material CompositeMaterial)
    {
        mat = CompositeMaterial;

    }

    public override void RecordRenderGraph(
        RenderGraph rg,
        ContextContainer ctx)
    {
        var cameraData = ctx.Get<UniversalCameraData>();

        

        using var builder =
            rg.AddRasterRenderPass<PassData>("Camera Gate", out var data);

        data.isBaseCamera = cameraData.postProcessEnabled;
        builder.AllowPassCulling(false);
        builder.AllowGlobalStateModification(true);
        builder.SetRenderFunc((PassData data, RasterGraphContext gctx) =>
        {
            // Set global for THIS camera's graph execution
            //Debug.Log(" setting IsBaseCamera to " + data.isBaseCamera);
            mat.SetFloat("_IsBaseCamera", data.isBaseCamera ? 1f : 0f);
        }); 
    }   
}