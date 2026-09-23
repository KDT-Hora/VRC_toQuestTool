using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Pipeline;

namespace VrcRufu.QuestAvatarConverter.Materials
{
    /// <summary>
    /// T018 (FR-006): converts one <see cref="PCMaterial"/> into a <see cref="QuestMaterial"/>
    /// asset. Looks up <c>PCMaterial.Shader</c> + the user-selected target shader
    /// (<c>ConversionContext.Settings.TargetShaderRule.TargetShader</c>) in the loaded
    /// <see cref="ShaderConversionRuleSet"/>s; if none matches, records this Material as an
    /// explicit conversion failure in <c>ConversionContext.Log</c> — never guesses a mapping,
    /// never silently skips it (FR-006), and leaves no entry in <c>ConversionContext.MaterialMap</c>
    /// for it (the absence of a map entry IS the failure signal for downstream stages, e.g.
    /// RendererMaterialReplacer, T027).
    /// </summary>
    /// <remarks>
    /// Texture-kind mapped properties are resolved from <c>ConversionContext.TextureMap</c> — the
    /// caller (ConversionPipeline, T029) MUST have already run the texture pipeline (T020-T026)
    /// for <paramref name="sourceMaterial"/>'s classifications before calling
    /// <see cref="Convert"/>, so that lookup can succeed. A classification with no TextureMap
    /// entry yet (e.g. Merge Textures disabled, or the source used no texture for that
    /// classification) results in that target texture property being left unset (null) — matches
    /// the flat-color-Material Edge Case: the Quest Material still generates successfully with
    /// only its mapped color/scalar properties.
    /// </remarks>
    public static class MaterialConverter
    {
        public static void Convert(ConversionContext context, PCMaterial sourceMaterial, IReadOnlyList<ShaderConversionRuleSet> loadedRules)
        {
            // FR-011: a Material shared by multiple Renderers is converted once.
            if (context.MaterialMap.ContainsKey(sourceMaterial))
            {
                return;
            }

            var targetShader = context.Settings.TargetShaderRule != null ? context.Settings.TargetShaderRule.TargetShader : null;
            var rule = FindRule(sourceMaterial.Shader, targetShader, loadedRules);
            if (rule == null)
            {
                context.Log.Add(ConversionLogEntry.Error(
                    $"Material '{DescribeMaterial(sourceMaterial)}': no loaded ShaderConversionRuleSet maps source " +
                    $"shader '{DescribeShader(sourceMaterial.Shader)}' to target shader '{DescribeShader(targetShader)}' " +
                    "— conversion failed (FR-006).",
                    sourceMaterial.Asset));
                return;
            }

            var mappingResult = ShaderPropertyMapper.Map(rule, sourceMaterial);
            foreach (var warning in mappingResult.Warnings)
            {
                context.Log.Add(ConversionLogEntry.Warning(warning, sourceMaterial.Asset));
            }

            var materialName = sourceMaterial.Asset != null ? sourceMaterial.Asset.name : "QuestMaterial";
            var materialAsset = new Material(targetShader) { name = materialName };

            foreach (var mapped in mappingResult.MappedProperties)
            {
                ApplyProperty(context, sourceMaterial, materialAsset, mapped);
            }

            var materialsFolder = QuestOutputPaths.GetMaterialsFolder(context.OutputRoot);
            QuestOutputPaths.EnsureFolder(materialsFolder);
            var assetPath = $"{materialsFolder}/{materialName}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            AssetDatabase.CreateAsset(materialAsset, assetPath);

            context.MaterialMap[sourceMaterial] = new QuestMaterial(sourceMaterial) { Asset = materialAsset };
        }

        private static void ApplyProperty(ConversionContext context, PCMaterial sourceMaterial, Material materialAsset, MappedMaterialProperty mapped)
        {
            switch (mapped.SourceProperty.Kind)
            {
                case MaterialPropertyKind.Texture:
                    Texture2D texture = null;
                    if (context.TextureMap.TryGetValue((sourceMaterial, mapped.TargetClassification), out var questTexture))
                    {
                        texture = questTexture.Asset;
                    }
                    // Deliberately NOT carrying over the source property's UvScale/UvOffset: once
                    // a texture has gone through the atlas/resize pipeline, its content already
                    // reflects the new layout, sampled over the standard 0-1 range (spec.md's
                    // Edge Cases / Assumptions) — re-applying the old tiling would double-apply it.
                    materialAsset.SetTexture(mapped.TargetPropertyName, texture);
                    break;

                case MaterialPropertyKind.Color:
                    materialAsset.SetColor(mapped.TargetPropertyName, mapped.SourceProperty.ColorValue);
                    break;

                case MaterialPropertyKind.Scalar:
                    materialAsset.SetFloat(mapped.TargetPropertyName, mapped.SourceProperty.ScalarValue);
                    break;
            }
        }

        private static ShaderConversionRuleSet FindRule(Shader sourceShader, Shader targetShader, IReadOnlyList<ShaderConversionRuleSet> loadedRules)
        {
            if (sourceShader == null || targetShader == null || loadedRules == null)
            {
                return null;
            }

            foreach (var rule in loadedRules)
            {
                if (rule != null && rule.SourceShader == sourceShader && rule.TargetShader == targetShader)
                {
                    return rule;
                }
            }

            return null;
        }

        private static string DescribeMaterial(PCMaterial material) => material.Asset != null ? material.Asset.name : "(unnamed Material)";
        private static string DescribeShader(Shader shader) => shader != null ? shader.name : "(none)";
    }
}
