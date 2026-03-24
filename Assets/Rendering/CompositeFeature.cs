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

	//Because urp forces feature to run on every camera we exclude here
	//So 2 main cameras only render to rendertextures and only third composite camera doesn't render and runs this composite pass
	//Camera 3 is ok because we use cullingMask = 0 so nothing actually gets sent to the gpu
	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
		if (compositeMaterial == null) return;
		if (renderingData.cameraData.camera.name != "CompositeOutputCamera") return;
		renderer.EnqueuePass(pass);
	}
}
