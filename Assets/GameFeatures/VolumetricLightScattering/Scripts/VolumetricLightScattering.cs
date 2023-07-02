using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

public class VolumetricLightScattering : ScriptableRendererFeature
{
    [System.Serializable]
    public class VolumetricLightScatteringSettings
    {
        [Header("Properties")]
        [Range(0.1f, 1f)]
        public float resolutionScale = 0.5f;

        [Range(0.0f, 1.0f)]
        public float intensity = 1.0f;

        [Range(0.0f, 1.0f)]
        public float blurWidth = 0.85f;
    }

    class LightScatteringPass : ScriptableRenderPass
    {
        private readonly RenderTargetHandle occluders =
            RenderTargetHandle.CameraTarget;
        private readonly float resolutionScale;
        private readonly float intensity;
        private readonly float blurWidth;
        private readonly Material occludersMaterial;
        private readonly List<ShaderTagId> shaderTagIdList =
            new List<ShaderTagId>();
        private FilteringSettings filteringSettings =
            new FilteringSettings(RenderQueueRange.opaque);
        private readonly Material radialBlurMaterial;
        private RenderTargetIdentifier cameraColorTargetIdent;

        public LightScatteringPass(VolumetricLightScatteringSettings settings)
        {
            occluders.Init("_OccludersMap");
            resolutionScale = settings.resolutionScale;
            intensity = settings.intensity;
            blurWidth = settings.blurWidth;
            occludersMaterial = new Material(Shader.Find("Hidden/RW/UnlitColor"));
            shaderTagIdList.Add(new ShaderTagId("UniversalForward"));
            shaderTagIdList.Add(new ShaderTagId("UniversalForwardOnly"));
            shaderTagIdList.Add(new ShaderTagId("LightweightForward"));
            shaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
            radialBlurMaterial = new Material(Shader.Find("Hidden/RW/RadialBlur"));
        }

        public void SetCameraColorTarget(RenderTargetIdentifier cameraColorTargetIdent)
        {
            this.cameraColorTargetIdent = cameraColorTargetIdent;
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor cameraTextureDescriptor =
                renderingData.cameraData.cameraTargetDescriptor;

            cameraTextureDescriptor.depthBufferBits = 0;

            cameraTextureDescriptor.width = Mathf.RoundToInt(
                cameraTextureDescriptor.width * resolutionScale);
            cameraTextureDescriptor.height = Mathf.RoundToInt(
                cameraTextureDescriptor.height * resolutionScale);

            cmd.GetTemporaryRT(occluders.id, cameraTextureDescriptor, FilterMode.Bilinear);

            ConfigureTarget(occluders.Identifier());
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!occludersMaterial || !radialBlurMaterial)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd,
                new ProfilingSampler("VolumetricScattering")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                Camera camera = renderingData.cameraData.camera;
                context.DrawSkybox(camera);

                DrawingSettings drawSettings = CreateDrawingSettings(shaderTagIdList,
                    ref renderingData, SortingCriteria.CommonOpaque);
                drawSettings.overrideMaterial = occludersMaterial;
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);

                Vector3 sunDirectionWorldSpace =
                    RenderSettings.sun.transform.forward;
                Vector3 cameraPositionWorldSpace =
                    camera.transform.position;
                Vector3 sunPositionWorldSpace =
                    cameraPositionWorldSpace + sunDirectionWorldSpace;
                Vector3 sunPositionViewportSpace =
                    camera.WorldToViewportPoint(sunPositionWorldSpace);

                radialBlurMaterial.SetVector("_Center", new Vector4(
                    sunPositionViewportSpace.x, sunPositionViewportSpace.y, 0, 0));
                radialBlurMaterial.SetFloat("_Intensity", intensity);
                radialBlurMaterial.SetFloat("_BlurWidth", blurWidth);
                Blit(cmd, occluders.Identifier(), cameraColorTargetIdent,
                    radialBlurMaterial);
                
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(occluders.id);
        }
    }

    LightScatteringPass m_ScriptablePass;
    public VolumetricLightScatteringSettings settings = 
        new VolumetricLightScatteringSettings();


    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new LightScatteringPass(settings);
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        m_ScriptablePass.SetCameraColorTarget(renderer.cameraColorTargetHandle);
    }
}