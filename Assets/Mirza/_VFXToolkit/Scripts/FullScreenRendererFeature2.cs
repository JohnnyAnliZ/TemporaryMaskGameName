using System;

using UnityEngine;

using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Mirza.VFXToolKit
{
    public sealed class FullScreenRendererFeature2 : ScriptableRendererFeature
    {
        #region FEATURE_FIELDS

        /// <summary>
        /// An injection point for the full screen pass. This is similar to the RenderPassEvent enum but limited to only supported events.
        /// </summary>
        /// 

        public enum InjectionPoint
        {
            /// <summary>
            /// Inject a full screen pass before transparents are rendered.
            /// </summary>
            BeforeRenderingTransparents = RenderPassEvent.BeforeRenderingTransparents,

            /// <summary>
            /// Inject a full screen pass before post processing is rendered.
            /// </summary>
            BeforeRenderingPostProcessing = RenderPassEvent.BeforeRenderingPostProcessing,

            /// <summary>
            /// Inject a full screen pass after post processing is rendered.
            /// </summary>
            AfterRenderingPostProcessing = RenderPassEvent.AfterRenderingPostProcessing
        }

        public enum TextureFormat
        {
            // R8 = R8_UNorm.
            // RFloat = R32_SFloat.

            R8 = RenderTextureFormat.R8,
            RFloat = RenderTextureFormat.RFloat,

            // RGBA32 = R8G8B8A8_UNorm.
            // RGBA64 = R16G16B16A16_UNorm.

            RGBA32 = RenderTextureFormat.ARGB32,
            RGBA64 = RenderTextureFormat.ARGB64,

            // RGBAHalf = R16G16B16A16_SFloat.
            // RGBAFloat = R32G32B32A32_SFloat.

            RGBAHalf = RenderTextureFormat.ARGBHalf,
            RGBAFloat = RenderTextureFormat.ARGBFloat
        }

        [Serializable]
        public class Settings
        {
            public FilterMode filterMode = FilterMode.Point;
            public TextureFormat textureFormat = TextureFormat.RGBAHalf;

            [Space]

            [Range(1, 64)]
            public int downsampleLevel = 1;

            [Space]

            public Material material;

            [Space]

            public Material sendToMaterial;
            public string sendToMaterialTextureName = "_MainTex";

            [Header("READ ONLY")]
            [Space]

            public FullScreenRendererFeature2Controller controller;
        }

        public bool renderInSceneView = true;
        public InjectionPoint injectionPoint = InjectionPoint.BeforeRenderingPostProcessing;

        /// <summary>
        /// A mask of URP textures that the assigned material will need access to. Requesting unused requirements can degrade
        /// performance unnecessarily as URP might need to run additional rendering passes to generate them.
        /// </summary>
        /// 
        public ScriptableRenderPassInput requirements = ScriptableRenderPassInput.None;

        [Space]

        public Settings settings;

        // The user defined ScriptableRenderPass that is responsible for the actual rendering of the effect

        private CustomPostRenderPass fullScreenPass;

        #endregion

        #region FEATURE_METHODS

        public override void Create()
        {
            fullScreenPass = new CustomPostRenderPass(name, settings);
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Skip rendering if material or the pass instance are null for whatever reason.

            if (!settings.material || fullScreenPass == null)
            {
                return;
            }

            // This check makes sure to not render the effect to reflection probes or preview cameras as post-processing is typically not desired there.

            if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
            {
                return;
            }

            // Ignore scene view if disabed in settings.

            if (!renderInSceneView && renderingData.cameraData.cameraType == CameraType.SceneView)
            {
                return;
            }

            // You can control the rendering of your feature using custom post-processing VolumeComponents
            //
            // E.g. when controlling rendering with a VolumeComponent you will typically want to skip rendering as an optimization when the component
            // has settings which would make it imperceptible (e.g. the implementation of IsActive() might return false when some "intensity" value is 0).
            //
            // N.B. if your volume component type is actually defined in C# it is unlikely that VolumeManager would return a "null" instance of it as
            // GlobalSettings should always contain an instance of all VolumeComponents in the project even if if they're not overriden in the scene.

            FullScreenRendererFeature2VolumeComponent myVolume = VolumeManager.instance.stack?.GetComponent<FullScreenRendererFeature2VolumeComponent>();

            if (!myVolume || !myVolume.IsActive())
            {
                return;
            }

            // Here you specify at which part of the frame the effect will execute
            //
            // When creating post-processing effects you will almost always want to use on of the following injection points:
            // BeforeRenderingTransparents - in cases you want your effect to be visible behind transparent objects
            // BeforeRenderingPostProcessing - in cases where your effect is supposed to run before the URP post-processing stack
            // AfterRenderingPostProcessing - in cases where your effect is supposed to run after the URP post-processing stack, but before FXAA, upscaling or color grading

            fullScreenPass.renderPassEvent = (RenderPassEvent)injectionPoint;

            // You can specify if your effect needs scene depth, normals, motion vectors or a downscaled opaque color as input
            //
            // You specify them as a mask e.g. ScriptableRenderPassInput.Normals | ScriptableRenderPassInput.Motion and URP
            // will either reuse these if they've been generated earlier in the frame or will add passes to generate them.
            //
            // The inputs will get bound as global shader texture properties and can be sampled in the shader using using the following:
            // * Depth  - use "SampleSceneDepth" after including "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            // * Normal - use "SampleSceneNormals" after including "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            // * Opaque Scene Color - use "SampleSceneColor" after including "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl".
            //   Note that "OpaqueSceneColor" is a texture containing a possibly downscaled copy of the framebuffer from before rendering transparent objects which
            //   should not be your first choice when wanting to do a post-processing effect, for that this template will copy the active texture for sampling which is more expensive
            // * Motion Vectors - you currently need to declare and sample the texture as follows:
            //     TEXTURE2D_X(_MotionVectorTexture);
            //     ...
            //     LOAD_TEXTURE2D_X_LOD(_MotionVectorTexture, pixelCoords, 0).xy
            //
            // N.B. when using the FullScreenPass Shader Graph target you should simply use the "URP Sample Buffer" node which will handle the above for you

            fullScreenPass.ConfigureInput(requirements);

            renderer.EnqueuePass(fullScreenPass);
        }

        protected override void Dispose(bool disposing)
        {
            // We dispose the pass we created to free the resources it might be holding onto.

            fullScreenPass.Dispose();
        }

        #endregion

        private class CustomPostRenderPass : ScriptableRenderPass
        {
            #region PASS_FIELDS

            private readonly Settings settings;

            // The handle to the temporary colour copy texture (only used in the non-render graph path).

            private RTHandle copiedColourTextureHandle;

            #endregion

            public CustomPostRenderPass(string passName, Settings settings)
            {
                profilingSampler = new ProfilingSampler(passName);

                this.settings = settings;

                // * The 'requiresIntermediateTexture' field needs to be set to 'true' when a ScriptableRenderPass intends to sample
                //   the active color buffer
                // * This will make sure that URP will not apply the optimization of rendering the entire frame to the write-only backbuffer,
                //   but will instead render to intermediate textures that can be sampled, which is typically needed for post-processing

                requiresIntermediateTexture = true;
            }

            #region PASS_SHARED_RENDERING_CODE

            // This method is used to get the descriptor used for creating the temporary colour copy texture that will enable the main pass to sample the screen colour.

            private static RenderTextureDescriptor GetCopyPassTextureDescriptor(RenderTextureDescriptor desc, Settings settings)
            {
                // Unless 'desc.bindMS = true' for an MSAA texture a resolve pass will be inserted before it is bound for sampling.
                // Since our main pass shader does not expect to sample an MSAA target we will leave 'bindMS = false'.
                // If the camera target has MSAA enabled an MSAA resolve will still happen before our copy-color pass but
                // with this change we will avoid an unnecessary MSAA resolve before our main pass.

                desc.msaaSamples = 1;

                // This avoids copying the depth buffer tied to the current descriptor as the main pass in this example does not use it.

                desc.depthBufferBits = (int)DepthBits.None;

                desc.width /= settings.downsampleLevel;
                desc.height /= settings.downsampleLevel;

                desc.colorFormat = (RenderTextureFormat)settings.textureFormat;

                return desc;
            }

            #endregion

            #region PASS_NON_RENDER_GRAPH_PATH

            // This method is called before executing the render pass (non-render graph path only).
            // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
            // When empty this render pass will render to the active camera render target.
            // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
            // The render pipeline will ensure target setup and clearing happens in a performant manner.

            [Obsolete("This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.", false)]
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                // This ScriptableRenderPass manages its own RenderTarget.
                // ResetTarget here so that ScriptableRenderer's active attachment can be invalidated when processing this ScriptableRenderPass.

                ResetTarget();

                // This allocates our intermediate texture for the non-RG path and makes sure it's reallocated if some settings on the camera target change (e.g. resolution)

                RenderingUtils.ReAllocateHandleIfNeeded(ref copiedColourTextureHandle, GetCopyPassTextureDescriptor(renderingData.cameraData.cameraTargetDescriptor, settings), filterMode: settings.filterMode, name: "_CustomPostPassCopyColour");
            }

            // Here you can implement the rendering logic (non-render graph path only).

            // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
            // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
            // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.

            [Obsolete("This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.", false)]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // Controller callback.

                if (settings.controller)
                {
                    settings.controller.OnExecutePass(null, renderingData);
                }

                var cmd = CommandBufferPool.Get();

                ref CameraData cameraData = ref renderingData.cameraData;
                RTHandle cameraColourTextureHandle = cameraData.renderer.cameraColorTargetHandle;

                using (new ProfilingScope(cmd, profilingSampler))
                {
                    RasterCommandBuffer rasterCmd = CommandBufferHelpers.GetRasterCommandBuffer(cmd);

                    CoreUtils.SetRenderTarget(cmd, copiedColourTextureHandle);
                    Blitter.BlitTexture(cmd, cameraColourTextureHandle, new Vector4(1.0f, 1.0f, 0.0f, 0.0f), settings.material, 0);

                    if (settings.sendToMaterial)
                    {
                        settings.sendToMaterial.SetTexture(settings.sendToMaterialTextureName, copiedColourTextureHandle);
                    }
                    else
                    {
                        CoreUtils.SetRenderTarget(cmd, cameraColourTextureHandle);
                        Blitter.BlitTexture(cmd, copiedColourTextureHandle, new Vector4(1.0f, 1.0f, 0.0f, 0.0f), 0.0f, settings.filterMode != FilterMode.Point);
                    }
                }

                context.ExecuteCommandBuffer(cmd);

                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            // Cleanup any allocated resources that were created during the execution of this render pass (non-render graph path only).

            public override void OnCameraCleanup(CommandBuffer cmd)
            {

            }

            public void Dispose()
            {
                copiedColourTextureHandle?.Release();
            }

            #endregion

            #region PASS_RENDER_GRAPH_PATH

            // The custom copy colour pass data that will be passed at render graph execution to the lambda we set with "SetRenderFunc" during render graph setup.

            private class CopyPassData
            {
                public TextureHandle inputTexture;
                public TextureHandle outputTexture;
            }

            // The custom main pass data that will be passed at render graph execution to the lambda we set with "SetRenderFunc" during render graph setup.

            private class MainPassData
            {
                public TextureHandle inputTexture;
            }

            private void ExecuteCopyPass(CopyPassData data, RasterGraphContext context)
            {
                Blitter.BlitTexture(context.cmd, data.inputTexture, new Vector4(1.0f, 1.0f, 0.0f, 0.0f), settings.material, 0);

                if (settings.sendToMaterial)
                {
                    settings.sendToMaterial.SetTexture(settings.sendToMaterialTextureName, data.outputTexture);
                }
            }
            private void ExecuteMainPass(MainPassData data, RasterGraphContext context)
            {
                if (!settings.sendToMaterial)
                {
                    Blitter.BlitTexture(context.cmd, data.inputTexture, new Vector4(1.0f, 1.0f, 0.0f, 0.0f), 0.0f, settings.filterMode != FilterMode.Point);
                }
            }

            // Here you can implement the rendering logic for the render graph path.

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourcesData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                // Controller callback.

                if (settings.controller)
                {
                    settings.controller.OnExecutePass(frameData, null);
                }

                // ...

                TextureHandle source, destination;

                Debug.Assert(resourcesData.cameraColor.IsValid());

                TextureDesc targetDesc = new(GetCopyPassTextureDescriptor(cameraData.cameraTargetDescriptor, settings))
                {
                    name = "_CameraColourFullScreenPass" + $" - {passName}",

                    clearBuffer = false,
                    filterMode = settings.filterMode,

                    memoryless = RenderTextureMemoryless.None
                };

                Debug.Log("Camera Target Descriptor: " + passName);
                Debug.Log(targetDesc.name);

                Debug.Log("");

                source = resourcesData.activeColorTexture;
                destination = renderGraph.CreateTexture(targetDesc);

                using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("Copy Colour Full Screen RF2", out var passData, profilingSampler))
                {
                    passData.inputTexture = source;
                    passData.outputTexture = destination;

                    builder.UseTexture(passData.inputTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    builder.SetRenderFunc((CopyPassData data, RasterGraphContext context) => ExecuteCopyPass(data, context));
                }

                // Swap for next pass.

                source = destination;
                destination = resourcesData.activeColorTexture;

                // Below is an example of a typical post-processing effect which samples from the current colour. 
                // Feel free modify/rename/add additional or remove the existing passes based on the needs of your custom post-processing effect.

                using (var builder = renderGraph.AddRasterRenderPass<MainPassData>(passName, out var passData, profilingSampler))
                {
                    passData.inputTexture = source;

                    builder.UseTexture(passData.inputTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    builder.SetRenderFunc((MainPassData data, RasterGraphContext context) => ExecuteMainPass(data, context));

                    // Swap cameraColor to new temp resource (destination) for the next pass.

                    resourcesData.cameraColor = destination;
                }
            }

            #endregion
        }
    }
}