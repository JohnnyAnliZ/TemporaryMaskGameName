using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CompositePass : ScriptableRenderPass
{
	Material material;
	class PassData {
		public Material material; //explicitly declare this here because we are good citizens
	}

	public CompositePass(Material compositeMaterial) {
		material = compositeMaterial;
		profilingSampler = new ProfilingSampler("CompositePass");
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
		if (material == null) return;

		var resourceData = frameData.Get<UniversalResourceData>();
		TextureHandle destination = resourceData.activeColorTexture;

		using var builder = renderGraph.AddRasterRenderPass<PassData>("CompositePass", out var passData);
		passData.material = material;
		builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
		builder.AllowPassCulling(false);
		builder.AllowGlobalStateModification(true);
		builder.SetRenderFunc((PassData data, RasterGraphContext gctx) => {
			gctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1); //triangle better than quad
		});
	}
}
