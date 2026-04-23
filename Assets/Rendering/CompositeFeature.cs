using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CompositeFeature : ScriptableRendererFeature
{
	public Material compositeMaterial;
	CompositePass pass;

	public override void Create() {
		pass = new CompositePass(compositeMaterial);
		pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
		if (compositeMaterial == null) return;
		renderer.EnqueuePass(pass);
	}
}
