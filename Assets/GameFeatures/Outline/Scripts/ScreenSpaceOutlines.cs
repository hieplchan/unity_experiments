using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenSpaceOutlines : ScriptableRendererFeature
{
    [System.Serializable]
    private class ViewSpaceNormalsTextureSettings
    {
        public RenderTextureFormat colorFormat;
        public int depthBufferBits;
        public FilterMode filterMode;
        public Color backgroundColor;
    }

    class ViewSpaceNormalsTexturePass : ScriptableRenderPass
    {
        private readonly RenderTargetHandle normals;
        private ViewSpaceNormalsTextureSettings normalsTextureSettings;
        private readonly List<ShaderTagId> shaderTagIdList;
        private readonly Material normalsMaterial;
        private FilteringSettings filteringSettings;

        public ViewSpaceNormalsTexturePass(RenderPassEvent renderPassEvent, 
            ViewSpaceNormalsTextureSettings settings, 
            LayerMask outlinesLayerMask)
        {
            this.renderPassEvent = renderPassEvent;
            normals.Init("_SceneViewSpaceNormals");
            normalsTextureSettings = settings;            
            shaderTagIdList = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("LightweightForward"),
                new ShaderTagId("SRPDefaultUnlit")
            };
            normalsMaterial = new Material(Shader.Find("Shader Graphs/ViewSpaceNormalsShader"));
            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, outlinesLayerMask);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor normalsTextureDescriptor = cameraTextureDescriptor;
            normalsTextureDescriptor.colorFormat = normalsTextureSettings.colorFormat;
            normalsTextureDescriptor.depthBufferBits = normalsTextureSettings.depthBufferBits;

            cmd.GetTemporaryRT(normals.id, cameraTextureDescriptor, normalsTextureSettings.filterMode);
            ConfigureTarget(normals.Identifier());
            ConfigureClear(ClearFlag.All, normalsTextureSettings.backgroundColor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!normalsMaterial)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, new ProfilingSampler("SceneViewSpaceNormalsTextureCreation")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                DrawingSettings drawingSettings = CreateDrawingSettings(
                    shaderTagIdList, ref renderingData, 
                    renderingData.cameraData.defaultOpaqueSortFlags);
                drawingSettings.overrideMaterial = normalsMaterial;
                context.DrawRenderers(renderingData.cullResults, 
                    ref drawingSettings, ref filteringSettings);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(normals.id);
        }
    }

    class ScreenSpaceOutlinePass : ScriptableRenderPass
    {
        private readonly Material screenSpaceOutlineMaterial;
        private RenderTargetIdentifier cameraColorTarget;
        private RenderTargetIdentifier temporaryBuffer;
        private int temporaryBufferID = 
            Shader.PropertyToID("_TemporaryBuffer");
        private List<ShaderTagId> shaderTagIdList;
        private FilteringSettings filteringSettings;

        public ScreenSpaceOutlinePass(RenderPassEvent renderPassEvent,
            LayerMask outlinesLayerMask)
        {
            this.renderPassEvent = renderPassEvent;
            screenSpaceOutlineMaterial = new Material(
                Shader.Find("Shader Graphs/OutlineShader"));

            shaderTagIdList = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("LightweightForward"),
                new ShaderTagId("SRPDefaultUnlit")
            };

            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, outlinesLayerMask);
        }


        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;
            temporaryBuffer = new RenderTargetIdentifier("_TemporaryBuffer");

            cmd.GetTemporaryRT(temporaryBufferID, renderingData.cameraData.cameraTargetDescriptor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!screenSpaceOutlineMaterial)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, new ProfilingSampler("ScreenSpaceOutlines")))
            {
                //Blit(cmd, cameraColorTarget, temporaryBuffer);
                //Blit(cmd, temporaryBuffer, cameraColorTarget, screenSpaceOutlineMaterial);

                DrawingSettings drawingSettings = CreateDrawingSettings(
                    shaderTagIdList, ref renderingData,
                    renderingData.cameraData.defaultOpaqueSortFlags);
                drawingSettings.overrideMaterial = screenSpaceOutlineMaterial;
                context.DrawRenderers(renderingData.cullResults,
                    ref drawingSettings, ref filteringSettings);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(temporaryBufferID);
        }
    }

    [SerializeField]
    private ViewSpaceNormalsTextureSettings viewSpaceNormalsTextureSettings;
    [SerializeField] 
    private RenderPassEvent renderPassEvent;
    [SerializeField]
    private LayerMask outlinesLayerMask;

    ViewSpaceNormalsTexturePass viewSpaceNormalsTexturePass;
    ScreenSpaceOutlinePass screenSpaceOutlinePass;

    public override void Create()
    {
        viewSpaceNormalsTexturePass = new ViewSpaceNormalsTexturePass(renderPassEvent, viewSpaceNormalsTextureSettings, outlinesLayerMask);
        screenSpaceOutlinePass = new ScreenSpaceOutlinePass(renderPassEvent, outlinesLayerMask);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Access to normals texture with good quality in single pass
        // is harder than depth texture, so we add seperated pass to do this
        renderer.EnqueuePass(viewSpaceNormalsTexturePass);
        renderer.EnqueuePass(screenSpaceOutlinePass);
    }
}