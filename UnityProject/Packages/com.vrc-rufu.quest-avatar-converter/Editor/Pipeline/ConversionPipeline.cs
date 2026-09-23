using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Materials;
using VrcRufu.QuestAvatarConverter.Textures;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    /// <summary>
    /// T029: orchestrates T013-T028 into one end-to-end conversion run, populating
    /// <see cref="ConversionContext"/> throughout and never writing to any PC-namespace asset
    /// (Constitution I).
    /// </summary>
    /// <remarks>
    /// Actual stage order deviates slightly from this task's summary phrase ("duplicate avatar →
    /// resolve assets"): AssetResolver (T013) must run BEFORE AvatarDuplicator (T014), since
    /// AvatarDuplicator needs the PC avatar's already-resolved Renderer list to build the matching
    /// Quest-side RendererRefs. The task summary's ordering isn't otherwise load-bearing — the
    /// two stages don't depend on each other's OUTPUT, only on this one input relationship, so
    /// resolving first is a correctness fix, not a design choice up for debate.
    ///
    /// Phase 2 scope note: FR-018's independent step toggles are only partially honored here —
    /// <c>ResizeTexturesEnabled</c>, <c>MergeTexturesEnabled</c>, and <c>AddAaoComponentEnabled</c>
    /// are respected, but a "Duplicate Avatar" toggle has no <see cref="ConversionSettings"/> field
    /// to honor (data-model.md's ConversionSettings table does not list one, despite T037
    /// mentioning it as a fourth toggle — a pre-existing spec inconsistency, flagged for whoever
    /// implements T037 in Phase 4, not resolved here). Duplication always runs.
    /// </remarks>
    public static class ConversionPipeline
    {
        public static ConversionContext Run(
            GameObject pcAvatarRoot,
            ConversionSettings settings,
            IReadOnlyList<ShaderConversionRuleSet> loadedShaderRules,
            Func<string, bool> confirmOverwrite)
        {
            if (pcAvatarRoot == null)
            {
                throw new ArgumentNullException(nameof(pcAvatarRoot));
            }

            var outputRoot = QuestOutputPaths.GetOutputRoot(pcAvatarRoot.name);
            var sourceAvatar = AssetResolver.Resolve(pcAvatarRoot);
            var context = new ConversionContext(sourceAvatar, outputRoot, settings);

            // AAO presence check — halt before any output is produced (FR-013), unconditionally
            // (FR-013's requirement is not itself gated by AddAaoComponentEnabled).
            if (!AAOIntegrator.IsAaoInstalled())
            {
                context.Log.Add(ConversionLogEntry.Error(
                    "Avatar Optimizer (AAO) is not installed in this project. Install " +
                    "com.anatawa12.avatar-optimizer before generating a Quest avatar — Trace And " +
                    "Optimize is a required part of every conversion run (FR-012/FR-013)."));
                return context;
            }

            // Existing-output check — no silent overwrite (FR-022).
            if (ExistingOutputDetector.HasExistingOutput(outputRoot))
            {
                var approved = confirmOverwrite != null && confirmOverwrite(outputRoot);
                if (!approved)
                {
                    context.Log.Add(ConversionLogEntry.Info(
                        $"Generation cancelled: prior Quest output exists at '{outputRoot}' and overwriting it was not approved."));
                    return context;
                }
            }

            // Duplicate avatar (FR-023/FR-004/FR-002).
            context.QuestAvatar = AvatarDuplicator.Duplicate(sourceAvatar, outputRoot, settings.PlacementOffset);

            // Convert materials/textures, once per distinct source Material (FR-011).
            foreach (var material in CollectDistinctMaterials(sourceAvatar))
            {
                ConvertOneMaterial(context, material, loadedShaderRules);
            }

            // Replace renderer materials (FR-010/FR-011).
            RendererMaterialReplacer.ReplaceAll(context);

            // Add AAO component (FR-012), if the user left this optional step enabled.
            if (settings.AddAaoComponentEnabled)
            {
                AAOIntegrator.AddTraceAndOptimizeComponent(context);
            }

            PrefabUtility.SavePrefabAsset(context.QuestAvatar.RootPrefab);
            context.Log.Add(ConversionLogEntry.Info($"Quest avatar generated at '{outputRoot}'."));

            return context;
        }

        private static void ConvertOneMaterial(ConversionContext context, PCMaterial sourceMaterial, IReadOnlyList<ShaderConversionRuleSet> loadedShaderRules)
        {
            var targetShader = context.Settings.TargetShaderRule != null ? context.Settings.TargetShaderRule.TargetShader : null;
            var rule = FindRule(sourceMaterial.Shader, targetShader, loadedShaderRules);

            if (rule != null)
            {
                var mappingResult = ShaderPropertyMapper.Map(rule, sourceMaterial);
                var classifiedTextures = TextureTypeClassifier.Classify(mappingResult);
                TextureUvTilingDetector.DetectAndLog(context, sourceMaterial, classifiedTextures);

                var maxSize = context.Settings.ResizeTexturesEnabled ? context.Settings.MaxTextureSize : int.MaxValue;
                var texturesFolder = QuestOutputPaths.GetTexturesFolder(context.OutputRoot);

                foreach (var entry in classifiedTextures)
                {
                    var classification = entry.Key;
                    var sources = entry.Value;

                    // Merge Textures disabled: still process the single primary source (resize +
                    // write it), just without combining additional same-classification sources
                    // into one atlas.
                    if (!context.Settings.MergeTexturesEnabled && sources.Count > 1)
                    {
                        sources = new List<PCTexture> { sources[0] };
                    }

                    var questTexture = new QuestTexture(sources, classification);
                    var layout = TextureAtlasGenerator.Generate(sources);
                    var baseFileName = SanitizeFileName($"{DescribeMaterial(sourceMaterial)}_{classification}");

                    TextureAssetWriter.Write(questTexture, layout, maxSize, texturesFolder, baseFileName);
                    context.TextureMap[(sourceMaterial, classification)] = questTexture;
                }
            }

            // MaterialConverter independently re-resolves the rule and records the FR-006
            // failure itself when none matches — see its own doc comment for why that duplicate
            // (cheap) lookup is an acceptable tradeoff for keeping it callable standalone.
            MaterialConverter.Convert(context, sourceMaterial, loadedShaderRules);

            if (context.MaterialMap.TryGetValue(sourceMaterial, out var questMaterial))
            {
                GpuInstancingApplier.Apply(questMaterial);
            }
        }

        private static IEnumerable<PCMaterial> CollectDistinctMaterials(PCAvatar sourceAvatar)
        {
            var seen = new HashSet<PCMaterial>();
            foreach (var rendererRef in sourceAvatar.Renderers)
            {
                foreach (var material in rendererRef.SourceMaterials)
                {
                    if (material != null && seen.Add(material))
                    {
                        yield return material;
                    }
                }
            }
        }

        private static ShaderConversionRuleSet FindRule(Shader sourceShader, Shader targetShader, IReadOnlyList<ShaderConversionRuleSet> loadedRules)
        {
            if (sourceShader == null || targetShader == null || loadedRules == null)
            {
                return null;
            }

            return loadedRules.FirstOrDefault(r => r != null && r.SourceShader == sourceShader && r.TargetShader == targetShader);
        }

        private static string DescribeMaterial(PCMaterial material) => material.Asset != null ? material.Asset.name : "Material";

        private static string SanitizeFileName(string name)
        {
            foreach (var invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }
            return name;
        }
    }
}
