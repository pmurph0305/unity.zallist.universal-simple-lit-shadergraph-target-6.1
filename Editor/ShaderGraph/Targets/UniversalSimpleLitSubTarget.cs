#if UNITY_2022_2_OR_NEWER && !(UNITY_2022_2_0 || UNITY_2022_2_1 || UNITY_2022_2_2 || UNITY_2022_2_3 || UNITY_2022_2_4 || UNITY_2022_2_5 || UNITY_2022_2_6 || UNITY_2022_2_7 || UNITY_2022_2_8 || UNITY_2022_2_9 || UNITY_2022_2_10 || UNITY_2022_2_11 || UNITY_2022_2_12 || UNITY_2022_2_13 || UNITY_2022_2_14)
// This is a fix for https://github.com/Zallist/unity.zallist.universal-simple-lit-shadergraph-target/issues/20
// All changes are related to https://github.com/Unity-Technologies/Graphics/commit/584e10efb36cb33d6f67461da75eb3b035f3798f#diff-1536bdf9492174a8dce30e912411f0e2234cedf60cfbe1fafa482d5bdfd639a6
#define UNITY_2022_2_15_OR_NEWER
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Legacy;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using static Unity.Rendering.Universal.ShaderUtils;
using static UnityEditor.Rendering.Universal.ShaderGraph.SubShaderUtils;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    sealed class UniversalSimpleLitSubTarget : UniversalSubTarget/*, ILegacyTarget*/
    {
        static readonly GUID kSourceCodeGuid = new GUID("d6c78107b64145745805d963de80cc28"); // UniversalSimpleLitSubTarget.cs

        // Should be in UniversalTarget
        public const string kSimpleLitMaterialTypeTag = "\"UniversalMaterialType\" = \"SimpleLit\"";

#if UNITY_2022_2_OR_NEWER
        public override int latestVersion => 2;
#elif UNITY_2022_1_OR_NEWER
        public override int latestVersion => 1;
#endif

        //[SerializeField]
        static WorkflowMode m_WorkflowMode = WorkflowMode.Specular;

        [SerializeField]
        bool m_SpecularHighlights = false;

        [SerializeField]
        NormalDropOffSpace m_NormalDropOffSpace = NormalDropOffSpace.Tangent;

#if UNITY_2022_1_OR_NEWER
        [SerializeField]
        bool m_BlendModePreserveSpecular = true;
#endif

        public UniversalSimpleLitSubTarget()
        {
            displayName = "Simple Lit";
        }

        // This should really be a dedicated ShaderID with relevant logic in ShaderUtils
        protected override ShaderID shaderID => ShaderID.Unknown;

        public static WorkflowMode workflowMode
        {
            get => m_WorkflowMode;
            //set => m_WorkflowMode = value;
        }

        public bool specularHighlights
        {
            get => m_SpecularHighlights;
            set => m_SpecularHighlights = value;
        }

        public NormalDropOffSpace normalDropOffSpace
        {
            get => m_NormalDropOffSpace;
            set => m_NormalDropOffSpace = value;
        }

#if UNITY_2022_1_OR_NEWER
        public bool blendModePreserveSpecular
        {
            get => m_BlendModePreserveSpecular;
            set => m_BlendModePreserveSpecular = value;
        }
#endif

        public override bool IsActive() => true;

        public override void Setup(ref TargetSetupContext context)
        {
            context.AddAssetDependency(kSourceCodeGuid, AssetCollection.Flags.SourceDependency);
            base.Setup(ref context);

            var universalRPType = typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset);
            if (!context.HasCustomEditorForRenderPipeline(universalRPType))
            {
                var gui = typeof(ShaderGraphSimpleLitGUI);
#if HAS_VFX_GRAPH
                if (TargetsVFX())
                    gui = typeof(VFXShaderGraphSimpleLitGUI);
#endif
                context.AddCustomEditorForRenderPipeline(gui.FullName, universalRPType);
            }

            // Process SubShaders
#if UNITY_2022_2_15_OR_NEWER
            context.AddSubShader(PostProcessSubShader(SubShaders.SimpleLitSubShader(target, target.renderType, target.renderQueue, blendModePreserveSpecular, specularHighlights)));
#elif UNITY_2022_1_OR_NEWER
            context.AddSubShader(PostProcessSubShader(SubShaders.SimpleLitComputeDotsSubShader(target, target.renderType, target.renderQueue, blendModePreserveSpecular, specularHighlights)));
            context.AddSubShader(PostProcessSubShader(SubShaders.SimpleLitGLESSubShader(target, target.renderType, target.renderQueue, blendModePreserveSpecular, specularHighlights)));
#else
            context.AddSubShader(PostProcessSubShader(SubShaders.SimpleLitComputeDotsSubShader(target, target.renderType, target.renderQueue, specularHighlights)));
            context.AddSubShader(PostProcessSubShader(SubShaders.SimpleLitGLESSubShader(target, target.renderType, target.renderQueue, specularHighlights)));
#endif
        }

        public override void ProcessPreviewMaterial(Material material)
        {
            if (target.allowMaterialOverride)
            {
                // copy our target's default settings into the material
                // (technically not necessary since we are always recreating the material from the shader each time,
                // which will pull over the defaults from the shader definition)
                // but if that ever changes, this will ensure the defaults are set
                material.SetFloat(Property.SpecularWorkflowMode, (float)workflowMode);
                material.SetFloat(SimpleLitProperty.SpecularHighlights, specularHighlights ? 1.0f : 0.0f);
                material.SetFloat(Property.CastShadows, target.castShadows ? 1.0f : 0.0f);
                material.SetFloat(Property.ReceiveShadows, target.receiveShadows ? 1.0f : 0.0f);
                material.SetFloat(Property.SurfaceType, (float)target.surfaceType);
                material.SetFloat(Property.BlendMode, (float)target.alphaMode);
                material.SetFloat(Property.AlphaClip, target.alphaClip ? 1.0f : 0.0f);
                material.SetFloat(Property.CullMode, (int)target.renderFace);
                material.SetFloat(Property.ZWriteControl, (float)target.zWriteControl);
                material.SetFloat(Property.ZTest, (float)target.zTestMode);
            }

            // We always need these properties regardless of whether the material is allowed to override
            // Queue control & offset enable correct automatic render queue behavior
            // Control == 0 is automatic, 1 is user-specified render queue
            material.SetFloat(Property.QueueOffset, 0.0f);
            material.SetFloat(Property.QueueControl, (float)BaseShaderGUI.QueueControl.Auto);

            // call the full unlit material setup function
            ShaderGraphSimpleLitGUI.UpdateMaterial(material, MaterialUpdateType.CreatedNewMaterial);
        }

        public override void GetFields(ref TargetFieldContext context)
        {
            base.GetFields(ref context);

            var descs = context.blocks.Select(x => x.descriptor);

            // SimpleLit -- always controlled by subtarget
            context.AddField(UniversalFields.NormalDropOffOS, normalDropOffSpace == NormalDropOffSpace.Object);
            context.AddField(UniversalFields.NormalDropOffTS, normalDropOffSpace == NormalDropOffSpace.Tangent);
            context.AddField(UniversalFields.NormalDropOffWS, normalDropOffSpace == NormalDropOffSpace.World);
            context.AddField(UniversalFields.Normal, descs.Contains(BlockFields.SurfaceDescription.NormalOS) ||
                descs.Contains(BlockFields.SurfaceDescription.NormalTS) ||
                descs.Contains(BlockFields.SurfaceDescription.NormalWS));
            // Complex Lit
        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            context.AddBlock(BlockFields.SurfaceDescription.Smoothness);
            context.AddBlock(BlockFields.SurfaceDescription.NormalOS, normalDropOffSpace == NormalDropOffSpace.Object);
            context.AddBlock(BlockFields.SurfaceDescription.NormalTS, normalDropOffSpace == NormalDropOffSpace.Tangent);
            context.AddBlock(BlockFields.SurfaceDescription.NormalWS, normalDropOffSpace == NormalDropOffSpace.World);
            context.AddBlock(BlockFields.SurfaceDescription.Emission);

            // when the surface options are material controlled, we must show all of these blocks
            // when target controlled, we can cull the unnecessary blocks
            context.AddBlock(BlockFields.SurfaceDescription.Specular, specularHighlights || target.allowMaterialOverride);
            context.AddBlock(BlockFields.SurfaceDescription.Alpha, (target.surfaceType == SurfaceType.Transparent || target.alphaClip) || target.allowMaterialOverride);
            context.AddBlock(BlockFields.SurfaceDescription.AlphaClipThreshold, (target.alphaClip) || target.allowMaterialOverride);
        }

        public override void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            // if using material control, add the material property to control workflow mode
            if (target.allowMaterialOverride)
            {
                collector.AddFloatProperty(Property.SpecularWorkflowMode, (float)workflowMode);
                collector.AddFloatProperty(SimpleLitProperty.SpecularHighlights, specularHighlights ? 1.0f : 0.0f);
                collector.AddFloatProperty(Property.CastShadows, target.castShadows ? 1.0f : 0.0f);
                collector.AddFloatProperty(Property.ReceiveShadows, target.receiveShadows ? 1.0f : 0.0f);

                // setup properties using the defaults
                collector.AddFloatProperty(Property.SurfaceType, (float)target.surfaceType);
                collector.AddFloatProperty(Property.BlendMode, (float)target.alphaMode);
                collector.AddFloatProperty(Property.AlphaClip, target.alphaClip ? 1.0f : 0.0f);
#if UNITY_2022_1_OR_NEWER
                collector.AddFloatProperty(Property.BlendModePreserveSpecular, blendModePreserveSpecular ? 1.0f : 0.0f);
#endif
                collector.AddFloatProperty(Property.SrcBlend, 1.0f);    // always set by material inspector, ok to have incorrect values here
                collector.AddFloatProperty(Property.DstBlend, 0.0f);    // always set by material inspector, ok to have incorrect values here
                collector.AddToggleProperty(Property.ZWrite, (target.surfaceType == SurfaceType.Opaque));
                collector.AddFloatProperty(Property.ZWriteControl, (float)target.zWriteControl);
                collector.AddFloatProperty(Property.ZTest, (float)target.zTestMode);    // ztest mode is designed to directly pass as ztest
                collector.AddFloatProperty(Property.CullMode, (float)target.renderFace);    // render face enum is designed to directly pass as a cull mode

#if UNITY_2022_2_OR_NEWER
                bool enableAlphaToMask = (target.alphaClip && (target.surfaceType == SurfaceType.Opaque));
                collector.AddFloatProperty(Property.AlphaToMask, enableAlphaToMask ? 1.0f : 0.0f);
#endif
            }

            // We always need these properties regardless of whether the material is allowed to override other shader properties.
            // Queue control & offset enable correct automatic render queue behavior. Control == 0 is automatic, 1 is user-specified.
            // We initialize queue control to -1 to indicate to UpdateMaterial that it needs to initialize it properly on the material.
            collector.AddFloatProperty(Property.QueueOffset, 0.0f);
            collector.AddFloatProperty(Property.QueueControl, -1.0f);
        }

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange, Action<String> registerUndo)
        {
            var universalTarget = (target as UniversalTarget);
            universalTarget.AddDefaultMaterialOverrideGUI(ref context, onChange, registerUndo);

            context.AddProperty("Specular Highlights", new Toggle() { value = specularHighlights }, (evt) =>
            {
                if (Equals(specularHighlights, evt.newValue))
                    return;

                registerUndo("Change Specular Highlights");
                specularHighlights = evt.newValue;
                onChange();
            });

            universalTarget.AddDefaultSurfacePropertiesGUI(ref context, onChange, registerUndo, showReceiveShadows: true);

            context.AddProperty("Fragment Normal Space", new EnumField(NormalDropOffSpace.Tangent) { value = normalDropOffSpace }, (evt) =>
            {
                if (Equals(normalDropOffSpace, evt.newValue))
                    return;

                registerUndo("Change Fragment Normal Space");
                normalDropOffSpace = (NormalDropOffSpace)evt.newValue;
                onChange();
            });

#if UNITY_2022_1_OR_NEWER
            if (target.surfaceType == SurfaceType.Transparent)
            {
                if (target.alphaMode == AlphaMode.Alpha || target.alphaMode == AlphaMode.Additive)
                    context.AddProperty("Preserve Specular Lighting", new Toggle() { value = blendModePreserveSpecular }, (evt) =>
                    {
                        if (Equals(blendModePreserveSpecular, evt.newValue))
                            return;

                        registerUndo("Change Preserve Specular");
                        blendModePreserveSpecular = evt.newValue;
                        onChange();
                    });
            }
#endif

        }

        protected override int ComputeMaterialNeedsUpdateHash()
        {
            int hash = base.ComputeMaterialNeedsUpdateHash();
            hash = hash * 23 + target.allowMaterialOverride.GetHashCode();
            return hash;
        }

#if UNITY_2022_1_OR_NEWER
        internal override void OnAfterParentTargetDeserialized()
        {
            Assert.IsNotNull(target);

            if (this.sgVersion < latestVersion)
            {
                // Upgrade old incorrect Premultiplied blend into
                // equivalent Alpha + Preserve Specular blend mode.
                if (this.sgVersion < 1)
                {
                    if (target.alphaMode == AlphaMode.Premultiply)
                    {
                        target.alphaMode = AlphaMode.Alpha;
                        blendModePreserveSpecular = true;
                    }
                    else
                        blendModePreserveSpecular = false;
                }
                ChangeVersion(latestVersion);
            }
        }
#endif

        #region SubShader
        static class SubShaders
        {
#if UNITY_2022_2_15_OR_NEWER
            public static SubShaderDescriptor SimpleLitSubShader(UniversalTarget target, string renderType, string renderQueue, bool blendModePreserveSpecular, bool specularHighlights)
            // SM 4.5, compute with dots instancing
#elif UNITY_2022_1_OR_NEWER
            public static SubShaderDescriptor SimpleLitComputeDotsSubShader(UniversalTarget target, string renderType, string renderQueue, bool blendModePreserveSpecular, bool specularHighlights)
#else
            public static SubShaderDescriptor SimpleLitComputeDotsSubShader(UniversalTarget target, string renderType, string renderQueue, bool specularHighlights)
#endif
            {
                SubShaderDescriptor result = new SubShaderDescriptor()
                {
                    pipelineTag = UniversalTarget.kPipelineTag,
                    customTags = kSimpleLitMaterialTypeTag,
                    renderType = renderType,
                    renderQueue = renderQueue,
                    generatesPreview = true,
                    passes = new PassCollection()
                };

#if UNITY_2022_2_15_OR_NEWER
                result.passes.Add(SimpleLitPasses.Forward(target, blendModePreserveSpecular, specularHighlights, CorePragmas.Forward, SimpleLitKeywords.Forward));
#elif UNITY_2022_2_OR_NEWER
                result.passes.Add(SimpleLitPasses.Forward(target, blendModePreserveSpecular, specularHighlights, CorePragmas.ForwardSM45, SimpleLitKeywords.DOTSForward));
#elif UNITY_2022_1_OR_NEWER
                result.passes.Add(SimpleLitPasses.Forward(target, blendModePreserveSpecular, specularHighlights, CorePragmas.DOTSForward));
#else
                result.passes.Add(SimpleLitPasses.Forward(target, specularHighlights, CorePragmas.DOTSForward));
#endif

#if UNITY_2022_1_OR_NEWER
                result.passes.Add(SimpleLitPasses.GBuffer(target, blendModePreserveSpecular, specularHighlights));
#else
                result.passes.Add(SimpleLitPasses.GBuffer(target, specularHighlights));
#endif

                // cull the shadowcaster pass if we know it will never be used
                if (target.castShadows || target.allowMaterialOverride)
#if UNITY_2022_2_15_OR_NEWER
                    result.passes.Add(PassVariant(CorePasses.ShadowCaster(target), CorePragmas.Instanced));
#elif UNITY_2022_2_OR_NEWER
                    result.passes.Add(PassVariant(CorePasses.ShadowCaster(target), CorePragmas.InstancedSM45));
#else
                    result.passes.Add(PassVariant(CorePasses.ShadowCaster(target), CorePragmas.DOTSInstanced));
#endif

                if (target.mayWriteDepth)
#if UNITY_2022_2_15_OR_NEWER
                    result.passes.Add(PassVariant(CorePasses.DepthOnly(target), CorePragmas.Instanced));
#elif UNITY_2022_2_OR_NEWER
                    result.passes.Add(PassVariant(CorePasses.DepthOnly(target), CorePragmas.InstancedSM45));
#else
                    result.passes.Add(PassVariant(CorePasses.DepthOnly(target), CorePragmas.DOTSInstanced));
#endif

#if UNITY_2022_2_15_OR_NEWER
                result.passes.Add(PassVariant(SimpleLitPasses.DepthNormal(target), CorePragmas.Instanced));
#elif UNITY_2022_2_OR_NEWER
                result.passes.Add(PassVariant(SimpleLitPasses.DepthNormal(target), CorePragmas.InstancedSM45));
#else
                result.passes.Add(PassVariant(SimpleLitPasses.DepthNormal(target), CorePragmas.DOTSInstanced));
#endif

#if UNITY_2022_2_15_OR_NEWER
                result.passes.Add(PassVariant(SimpleLitPasses.Meta(target), CorePragmas.Default));
#elif UNITY_2022_2_OR_NEWER
                result.passes.Add(PassVariant(SimpleLitPasses.Meta(target), CorePragmas.DefaultSM45));
#else
                result.passes.Add(PassVariant(SimpleLitPasses.Meta(target), CorePragmas.DOTSDefault));
#endif

                // Currently neither of these passes (selection/picking) can be last for the game view for
                // UI shaders to render correctly. Verify [1352225] before changing this order.
#if UNITY_2022_2_15_OR_NEWER
                result.passes.Add(PassVariant(CorePasses.SceneSelection(target), CorePragmas.Default));
#elif UNITY_2022_2_OR_NEWER
                result.passes.Add(PassVariant(CorePasses.SceneSelection(target), CorePragmas.DefaultSM45));
#else
                result.passes.Add(PassVariant(CorePasses.SceneSelection(target), CorePragmas.DOTSDefault));
#endif

#if UNITY_2022_2_15_OR_NEWER
                result.passes.Add(PassVariant(CorePasses.ScenePicking(target), CorePragmas.Default));
#elif UNITY_2022_2_OR_NEWER
                result.passes.Add(PassVariant(CorePasses.ScenePicking(target), CorePragmas.DefaultSM45));
#else
                result.passes.Add(PassVariant(CorePasses.ScenePicking(target), CorePragmas.DOTSDefault));
#endif

#if UNITY_2022_2_15_OR_NEWER
                result.passes.Add(PassVariant(SimpleLitPasses._2D(target), CorePragmas.Default));
#elif UNITY_2022_2_OR_NEWER
                result.passes.Add(PassVariant(SimpleLitPasses._2D(target), CorePragmas.DefaultSM45));
#else
                result.passes.Add(PassVariant(SimpleLitPasses._2D(target), CorePragmas.DOTSDefault));
#endif


                return result;
            }

#if !UNITY_2022_2_15_OR_NEWER
#if UNITY_2022_1_OR_NEWER
            public static SubShaderDescriptor SimpleLitGLESSubShader(UniversalTarget target, string renderType, string renderQueue, bool blendModePreserveSpecular, bool specularHighlights)
#else
            public static SubShaderDescriptor SimpleLitGLESSubShader(UniversalTarget target, string renderType, string renderQueue, bool specularHighlights)
#endif
            {
                // SM 2.0, GLES

                // ForwardOnly pass is used as complex Lit SM 2.0 fallback for GLES.
                // Drops advanced features and renders materials as SimpleLit.

                SubShaderDescriptor result = new SubShaderDescriptor()
                {
                    pipelineTag = UniversalTarget.kPipelineTag,
                    customTags = kSimpleLitMaterialTypeTag,
                    renderType = renderType,
                    renderQueue = renderQueue,
                    generatesPreview = true,
                    passes = new PassCollection()
                };

#if UNITY_2022_2_OR_NEWER
                result.passes.Add(SimpleLitPasses.Forward(target, blendModePreserveSpecular, specularHighlights, CorePragmas.Forward, SimpleLitKeywords.Forward));
#elif UNITY_2022_1_OR_NEWER
                result.passes.Add(SimpleLitPasses.Forward(target, blendModePreserveSpecular, specularHighlights));
#else
                result.passes.Add(SimpleLitPasses.Forward(target, specularHighlights));
#endif

                // cull the shadowcaster pass if we know it will never be used
                if (target.castShadows || target.allowMaterialOverride)
                    result.passes.Add(CorePasses.ShadowCaster(target));

                if (target.mayWriteDepth)
                    result.passes.Add(CorePasses.DepthOnly(target));

                result.passes.Add(CorePasses.DepthNormal(target));
                result.passes.Add(SimpleLitPasses.Meta(target));
                // Currently neither of these passes (selection/picking) can be last for the game view for
                // UI shaders to render correctly. Verify [1352225] before changing this order.
                result.passes.Add(CorePasses.SceneSelection(target));
                result.passes.Add(CorePasses.ScenePicking(target));

                result.passes.Add(SimpleLitPasses._2D(target));

                return result;
            }
#endif
        }
        #endregion

        #region Passes
        static class SimpleLitPasses
        {
            static void AddWorkflowModeControlToPass(ref PassDescriptor pass, UniversalTarget target, WorkflowMode workflowMode)
            {
                //if (target.allowMaterialOverride)
                //    pass.keywords.Add(LitDefines.SpecularSetup);
                //else if (workflowMode == WorkflowMode.Specular)
                pass.defines.Add(SimpleLitDefines.SpecularSetup, 1);
            }

            static void AddSpecularHighlightsControlToPass(ref PassDescriptor pass, UniversalTarget target, bool specularHighlights)
            {
                if (target.allowMaterialOverride)
                    pass.keywords.Add(SimpleLitDefines.SpecularColor);
                else if (specularHighlights)
                    pass.defines.Add(SimpleLitDefines.SpecularColor, 1);
            }

            static void AddReceiveShadowsControlToPass(ref PassDescriptor pass, UniversalTarget target, bool receiveShadows)
            {
                if (target.allowMaterialOverride)
                    pass.keywords.Add(SimpleLitKeywords.ReceiveShadowsOff);
                else if (!receiveShadows)
                    pass.defines.Add(SimpleLitKeywords.ReceiveShadowsOff, 1);
            }

#if UNITY_2022_2_OR_NEWER
            public static PassDescriptor Forward(
                UniversalTarget target,
                bool blendModePreserveSpecular,
                bool specularHighlights,
                PragmaCollection pragmas,
                KeywordCollection keywords)
#elif UNITY_2022_1_OR_NEWER
            public static PassDescriptor Forward(UniversalTarget target, bool blendModePreserveSpecular, bool specularHighlights, PragmaCollection pragmas = null)
#else
            public static PassDescriptor Forward(UniversalTarget target, bool specularHighlights, PragmaCollection pragmas = null)
#endif
            {
                var result = new PassDescriptor()
                {
                    // Definition
                    displayName = "Universal Forward",
                    referenceName = "SHADERPASS_FORWARD",
                    lightMode = "UniversalForward",
                    useInPreview = true,

                    // Template
                    passTemplatePath = UniversalTarget.kUberTemplatePath,
                    sharedTemplateDirectories = UniversalTarget.kSharedTemplateDirectories,

                    // Port Mask
                    validVertexBlocks = CoreBlockMasks.Vertex,
                    validPixelBlocks = SimpleLitBlockMasks.FragmentSimpleLit,

                    // Fields
                    structs = CoreStructCollections.Default,
                    requiredFields = SimpleLitRequiredFields.Forward,
                    fieldDependencies = CoreFieldDependencies.Default,

                    // Conditional State
#if UNITY_2022_1_OR_NEWER
                    renderStates = CoreRenderStates.UberSwitchedRenderState(target, blendModePreserveSpecular),
#else
                    renderStates = CoreRenderStates.UberSwitchedRenderState(target/*, blendModePreserveSpecular*/),
#endif
                    pragmas = pragmas ?? CorePragmas.Forward,     // NOTE: SM 2.0 only GL
                    defines = new DefineCollection() { CoreDefines.UseFragmentFog },
#if UNITY_2022_2_OR_NEWER
                    keywords = new KeywordCollection() { keywords },
#else
                    keywords = new KeywordCollection() { SimpleLitKeywords.Forward },
#endif
                    includes = SimpleLitIncludes.Forward,

                    // Custom Interpolator Support
                    customInterpolators = CoreCustomInterpDescriptors.Common
                };

#if UNITY_2022_1_OR_NEWER
                CorePasses.AddTargetSurfaceControlsToPass(ref result, target, blendModePreserveSpecular);
#else
                CorePasses.AddTargetSurfaceControlsToPass(ref result, target/*, blendModePreserveSpecular*/);
#endif
#if UNITY_2022_2_OR_NEWER
                CorePasses.AddAlphaToMaskControlToPass(ref result, target);
#endif
                AddWorkflowModeControlToPass(ref result, target, workflowMode);
                AddSpecularHighlightsControlToPass(ref result, target, specularHighlights);
                AddReceiveShadowsControlToPass(ref result, target, target.receiveShadows);
#if UNITY_2022_2_OR_NEWER
                CorePasses.AddLODCrossFadeControlToPass(ref result, target);
#endif

                return result;
            }

            // Deferred only in SM4.5, MRT not supported in GLES2
#if UNITY_2022_1_OR_NEWER
            public static PassDescriptor GBuffer(UniversalTarget target, bool blendModePreserveSpecular, bool specularHighlights)
#else
            public static PassDescriptor GBuffer(UniversalTarget target, bool specularHighlights)
#endif
            {
                var result = new PassDescriptor
                {
                    // Definition
                    displayName = "GBuffer",
                    referenceName = "SHADERPASS_GBUFFER",
                    lightMode = "UniversalGBuffer",
#if UNITY_2022_2_OR_NEWER
                    useInPreview = true,
#endif

                    // Template
                    passTemplatePath = UniversalTarget.kUberTemplatePath,
                    sharedTemplateDirectories = UniversalTarget.kSharedTemplateDirectories,

                    // Port Mask
                    validVertexBlocks = CoreBlockMasks.Vertex,
                    validPixelBlocks = SimpleLitBlockMasks.FragmentSimpleLit,

                    // Fields
                    structs = CoreStructCollections.Default,
                    requiredFields = SimpleLitRequiredFields.GBuffer,
                    fieldDependencies = CoreFieldDependencies.Default,

                    // Conditional State
#if UNITY_2022_1_OR_NEWER
                    renderStates = CoreRenderStates.UberSwitchedRenderState(target, blendModePreserveSpecular),
#else
                    renderStates = CoreRenderStates.UberSwitchedRenderState(target/*, blendModePreserveSpecular*/),
#endif
#if UNITY_2022_2_15_OR_NEWER
                    pragmas = CorePragmas.GBuffer,
#elif UNITY_2022_2_OR_NEWER
                    pragmas = CorePragmas.GBufferSM45,
#else
                    pragmas = CorePragmas.DOTSGBuffer,
#endif
                    defines = new DefineCollection() { CoreDefines.UseFragmentFog },
                    keywords = new KeywordCollection() { SimpleLitKeywords.GBuffer },
                    includes = SimpleLitIncludes.GBuffer,

                    // Custom Interpolator Support
                    customInterpolators = CoreCustomInterpDescriptors.Common
                };

#if UNITY_2022_1_OR_NEWER
                CorePasses.AddTargetSurfaceControlsToPass(ref result, target, blendModePreserveSpecular);
#else
                CorePasses.AddTargetSurfaceControlsToPass(ref result, target/*, blendModePreserveSpecular*/);
#endif
                AddWorkflowModeControlToPass(ref result, target, workflowMode);
                AddSpecularHighlightsControlToPass(ref result, target, specularHighlights);
                AddReceiveShadowsControlToPass(ref result, target, target.receiveShadows);
#if UNITY_2022_2_OR_NEWER
                CorePasses.AddLODCrossFadeControlToPass(ref result, target);
#endif

                return result;
            }

            public static PassDescriptor Meta(UniversalTarget target)
            {
                var result = new PassDescriptor()
                {
                    // Definition
                    displayName = "Meta",
                    referenceName = "SHADERPASS_META",
                    lightMode = "Meta",

                    // Template
                    passTemplatePath = UniversalTarget.kUberTemplatePath,
                    sharedTemplateDirectories = UniversalTarget.kSharedTemplateDirectories,

                    // Port Mask
                    validVertexBlocks = CoreBlockMasks.Vertex,
                    validPixelBlocks = SimpleLitBlockMasks.FragmentMeta,

                    // Fields
                    structs = CoreStructCollections.Default,
                    requiredFields = SimpleLitRequiredFields.Meta,
                    fieldDependencies = CoreFieldDependencies.Default,

                    // Conditional State
                    renderStates = CoreRenderStates.Meta,
                    pragmas = CorePragmas.Default,
                    defines = new DefineCollection() { CoreDefines.UseFragmentFog },
                    keywords = new KeywordCollection() { CoreKeywordDescriptors.EditorVisualization },
                    includes = SimpleLitIncludes.Meta,

                    // Custom Interpolator Support
                    customInterpolators = CoreCustomInterpDescriptors.Common
                };

                CorePasses.AddAlphaClipControlToPass(ref result, target);

                return result;
            }

            public static PassDescriptor _2D(UniversalTarget target)
            {
                var result = new PassDescriptor()
                {
                    // Definition
                    referenceName = "SHADERPASS_2D",
                    lightMode = "Universal2D",

                    // Template
                    passTemplatePath = UniversalTarget.kUberTemplatePath,
                    sharedTemplateDirectories = UniversalTarget.kSharedTemplateDirectories,

                    // Port Mask
                    validVertexBlocks = CoreBlockMasks.Vertex,
                    validPixelBlocks = CoreBlockMasks.FragmentColorAlpha,

                    // Fields
                    structs = CoreStructCollections.Default,
                    fieldDependencies = CoreFieldDependencies.Default,

                    // Conditional State
                    renderStates = CoreRenderStates.UberSwitchedRenderState(target),
                    pragmas = CorePragmas.Instanced,
                    defines = new DefineCollection(),
                    keywords = new KeywordCollection(),
                    includes = SimpleLitIncludes._2D,

                    // Custom Interpolator Support
                    customInterpolators = CoreCustomInterpDescriptors.Common
                };

                CorePasses.AddAlphaClipControlToPass(ref result, target);

                return result;
            }

            public static PassDescriptor DepthNormal(UniversalTarget target)
            {
                var result = new PassDescriptor()
                {
                    // Definition
                    displayName = "DepthNormals",
                    referenceName = "SHADERPASS_DEPTHNORMALS",
                    lightMode = "DepthNormals",
                    useInPreview = false,

                    // Template
                    passTemplatePath = UniversalTarget.kUberTemplatePath,
                    sharedTemplateDirectories = UniversalTarget.kSharedTemplateDirectories,

                    // Port Mask
                    validVertexBlocks = CoreBlockMasks.Vertex,
                    validPixelBlocks = CoreBlockMasks.FragmentDepthNormals,

                    // Fields
                    structs = CoreStructCollections.Default,
                    requiredFields = CoreRequiredFields.DepthNormals,
                    fieldDependencies = CoreFieldDependencies.Default,

                    // Conditional State
                    renderStates = CoreRenderStates.DepthNormalsOnly(target),
                    pragmas = CorePragmas.Instanced,
                    defines = new DefineCollection(),
#if UNITY_2022_2_15_OR_NEWER
                    keywords = new KeywordCollection(),
#elif UNITY_2022_2_OR_NEWER
                    keywords = new KeywordCollection() { CoreKeywords.DOTSDepthNormal },
#else
                    keywords = new KeywordCollection(),
#endif
                    includes = CoreIncludes.DepthNormalsOnly,

                    // Custom Interpolator Support
                    customInterpolators = CoreCustomInterpDescriptors.Common
                };

                CorePasses.AddAlphaClipControlToPass(ref result, target);
#if UNITY_2022_2_OR_NEWER
                CorePasses.AddLODCrossFadeControlToPass(ref result, target);
#endif

                return result;
            }
        }
        #endregion

        #region PortMasks
        static class SimpleLitBlockMasks
        {
            public static readonly BlockFieldDescriptor[] FragmentSimpleLit = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalOS,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Specular,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
            };

            public static readonly BlockFieldDescriptor[] FragmentMeta = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
            };
        }
        #endregion

        #region RequiredFields
        static class SimpleLitRequiredFields
        {
            public static readonly FieldCollection Forward = new FieldCollection()
            {
                StructFields.Attributes.uv1,
                StructFields.Attributes.uv2,
                StructFields.Varyings.positionWS,
                StructFields.Varyings.normalWS,
                StructFields.Varyings.tangentWS,                        // needed for vertex lighting
                UniversalStructFields.Varyings.staticLightmapUV,
                UniversalStructFields.Varyings.dynamicLightmapUV,
                UniversalStructFields.Varyings.sh,
                UniversalStructFields.Varyings.fogFactorAndVertexLight, // fog and vertex lighting, vert input is dependency
                UniversalStructFields.Varyings.shadowCoord,             // shadow coord, vert input is dependency
            };

            public static readonly FieldCollection GBuffer = new FieldCollection()
            {
                StructFields.Attributes.uv1,
                StructFields.Attributes.uv2,
                StructFields.Varyings.positionWS,
                StructFields.Varyings.normalWS,
                StructFields.Varyings.tangentWS,                        // needed for vertex lighting
                UniversalStructFields.Varyings.staticLightmapUV,
                UniversalStructFields.Varyings.dynamicLightmapUV,
                UniversalStructFields.Varyings.sh,
                UniversalStructFields.Varyings.fogFactorAndVertexLight, // fog and vertex lighting, vert input is dependency
                UniversalStructFields.Varyings.shadowCoord,             // shadow coord, vert input is dependency
            };

            public static readonly FieldCollection Meta = new FieldCollection()
            {
                StructFields.Attributes.positionOS,
                StructFields.Attributes.normalOS,
                StructFields.Attributes.uv0,                            //
                StructFields.Attributes.uv1,                            // needed for meta vertex position
                StructFields.Attributes.uv2,                            // needed for meta UVs
                StructFields.Attributes.instanceID,                     // needed for rendering instanced terrain
                StructFields.Varyings.positionCS,
                StructFields.Varyings.texCoord0,                        // needed for meta UVs
                StructFields.Varyings.texCoord1,                        // VizUV
                StructFields.Varyings.texCoord2,                        // LightCoord
            };
        }
        #endregion

        #region Defines
        static class SimpleLitDefines
        {
            public static readonly KeywordDescriptor SpecularSetup = new KeywordDescriptor()
            {
                displayName = "Specular Setup",
                referenceName = "_SPECULAR_SETUP",
                type = KeywordType.Boolean,
                definition = KeywordDefinition.ShaderFeature,
                scope = KeywordScope.Local,
                stages = KeywordShaderStage.Fragment
            };

            public static readonly KeywordDescriptor SpecularColor = new KeywordDescriptor()
            {
                displayName = "Specular Color",
                referenceName = SimpleLitProperty.SpecularColorKeyword,
                type = KeywordType.Boolean,
                definition = KeywordDefinition.ShaderFeature,
                scope = KeywordScope.Local,
                stages = KeywordShaderStage.Fragment
            };
        }
        #endregion

        #region Keywords
        static class SimpleLitKeywords
        {
            public static readonly KeywordDescriptor ReceiveShadowsOff = new KeywordDescriptor()
            {
                displayName = "Receive Shadows Off",
                referenceName = ShaderKeywordStrings._RECEIVE_SHADOWS_OFF,
                type = KeywordType.Boolean,
                definition = KeywordDefinition.ShaderFeature,
                scope = KeywordScope.Local,
            };

#if UNITY_2022_2_OR_NEWER
#else
            public static readonly KeywordDescriptor ScreenSpaceAmbientOcclusion = new KeywordDescriptor()
            {
                displayName = "Screen Space Ambient Occlusion",
                referenceName = "_SCREEN_SPACE_OCCLUSION",
                type = KeywordType.Boolean,
                definition = KeywordDefinition.MultiCompile,
                scope = KeywordScope.Global,
                stages = KeywordShaderStage.Fragment,
            };
#endif

            public static readonly KeywordCollection Forward = new KeywordCollection
            {
#if UNITY_2022_2_OR_NEWER
                { CoreKeywordDescriptors.ScreenSpaceAmbientOcclusion },
#else
                { ScreenSpaceAmbientOcclusion },
#endif
                { CoreKeywordDescriptors.StaticLightmap },
                { CoreKeywordDescriptors.DynamicLightmap },
                { CoreKeywordDescriptors.DirectionalLightmapCombined },
                { CoreKeywordDescriptors.MainLightShadows },
                { CoreKeywordDescriptors.AdditionalLights },
                { CoreKeywordDescriptors.AdditionalLightShadows },
                { CoreKeywordDescriptors.ReflectionProbeBlending },
                { CoreKeywordDescriptors.ReflectionProbeBoxProjection },
                { CoreKeywordDescriptors.ShadowsSoft },
                { CoreKeywordDescriptors.LightmapShadowMixing },
                { CoreKeywordDescriptors.ShadowsShadowmask },
                { CoreKeywordDescriptors.DBuffer },
                { CoreKeywordDescriptors.LightLayers },
                { CoreKeywordDescriptors.DebugDisplay },
                { CoreKeywordDescriptors.LightCookies },
#if UNITY_6000_1_OR_NEWER
              { CoreKeywordDescriptors.ClusterLightLoop },
#elif UNITY_2022_2_OR_NEWER
              { CoreKeywordDescriptors.ForwardPlus },
#else
                { CoreKeywordDescriptors.ClusteredRendering },
#endif
            };

#if UNITY_2022_2_15_OR_NEWER
#elif UNITY_2022_2_OR_NEWER
            public static readonly KeywordCollection DOTSForward = new KeywordCollection
            {
                { Forward },
                { CoreKeywordDescriptors.WriteRenderingLayers },
            };
#endif

            public static readonly KeywordCollection GBuffer = new KeywordCollection
            {
                { CoreKeywordDescriptors.StaticLightmap },
                { CoreKeywordDescriptors.DynamicLightmap },
                { CoreKeywordDescriptors.DirectionalLightmapCombined },
                { CoreKeywordDescriptors.MainLightShadows },
                { CoreKeywordDescriptors.ReflectionProbeBlending },
                { CoreKeywordDescriptors.ReflectionProbeBoxProjection },
                { CoreKeywordDescriptors.ShadowsSoft },
                { CoreKeywordDescriptors.LightmapShadowMixing },
                { CoreKeywordDescriptors.MixedLightingSubtractive },
                { CoreKeywordDescriptors.DBuffer },
                { CoreKeywordDescriptors.GBufferNormalsOct },
#if UNITY_2022_2_15_OR_NEWER
                { CoreKeywordDescriptors.LightLayers },
#elif UNITY_2022_2_OR_NEWER
                { CoreKeywordDescriptors.WriteRenderingLayers },
#else
                { CoreKeywordDescriptors.LightLayers },
#endif
                { CoreKeywordDescriptors.RenderPassEnabled },
                { CoreKeywordDescriptors.DebugDisplay },
            };
        }
        #endregion

        #region Includes
        static class SimpleLitIncludes
        {
            const string kShadows = "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl";
            const string kMetaInput = "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl";
            const string kForwardPass = "Packages/com.zallist.universal-shadergraph-extensions/Editor/ShaderGraph/Includes/SimpleLitForwardPass.hlsl";
            const string kGBuffer = "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl";
            const string kSimpleLitGBufferPass = "Packages/com.zallist.universal-shadergraph-extensions/Editor/ShaderGraph/Includes/SimpleLitGBufferPass.hlsl";
            const string kLightingMetaPass = "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl";
            // TODO : Replace 2D for Simple one
            const string k2DPass = "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBR2DPass.hlsl";

            public static readonly IncludeCollection Forward = new IncludeCollection
            {
                // Pre-graph
#if UNITY_2022_2_15_OR_NEWER
                { CoreIncludes.DOTSPregraph },
                { CoreIncludes.WriteRenderLayersPregraph },
#endif
                { CoreIncludes.CorePregraph },
                { kShadows, IncludeLocation.Pregraph },
                { CoreIncludes.ShaderGraphPregraph },
                { CoreIncludes.DBufferPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kForwardPass, IncludeLocation.Postgraph },
            };

            public static readonly IncludeCollection GBuffer = new IncludeCollection
            {
                // Pre-graph
#if UNITY_2022_2_15_OR_NEWER
                { CoreIncludes.DOTSPregraph },
                { CoreIncludes.WriteRenderLayersPregraph },
#endif
                { CoreIncludes.CorePregraph },
                { kShadows, IncludeLocation.Pregraph },
                { CoreIncludes.ShaderGraphPregraph },
                { CoreIncludes.DBufferPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kGBuffer, IncludeLocation.Postgraph },
                //{ kPBRGBufferPass, IncludeLocation.Postgraph },
                { kSimpleLitGBufferPass, IncludeLocation.Postgraph },
            };

            public static readonly IncludeCollection Meta = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                { CoreIncludes.ShaderGraphPregraph },
                { kMetaInput, IncludeLocation.Pregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kLightingMetaPass, IncludeLocation.Postgraph },
            };

            public static readonly IncludeCollection _2D = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                { CoreIncludes.ShaderGraphPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { k2DPass, IncludeLocation.Postgraph },
            };
        }
        #endregion
    }

    public static class SimpleLitProperty
    {
        public static readonly string SpecularColorKeyword = "_SPECULAR_COLOR";
        public static readonly string SpecularHighlights = "_SpecularHighlights";
    }
}
