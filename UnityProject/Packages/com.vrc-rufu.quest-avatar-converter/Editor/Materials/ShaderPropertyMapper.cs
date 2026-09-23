using System.Collections.Generic;
using VrcRufu.QuestAvatarConverter.Pipeline;

namespace VrcRufu.QuestAvatarConverter.Materials
{
    /// <summary>One source Material property successfully resolved to a target shader property
    /// via a <see cref="PropertyMapping"/>.</summary>
    public sealed class MappedMaterialProperty
    {
        public string TargetPropertyName { get; }

        /// <summary>The full source property (name, kind, value) this mapping carries over.</summary>
        public MaterialProperty SourceProperty { get; }

        /// <summary>The routing classification for a Texture-kind property (FR-008); not
        /// meaningful for Color/Scalar-kind properties.</summary>
        public TextureClassification TargetClassification { get; }

        public MappedMaterialProperty(string targetPropertyName, MaterialProperty sourceProperty, TextureClassification targetClassification)
        {
            TargetPropertyName = targetPropertyName;
            SourceProperty = sourceProperty;
            TargetClassification = targetClassification;
        }
    }

    /// <summary>The result of mapping one source Material's properties through one
    /// <see cref="ShaderConversionRuleSet"/>.</summary>
    public sealed class ShaderPropertyMappingResult
    {
        public IReadOnlyList<MappedMaterialProperty> MappedProperties { get; }

        /// <summary>One entry per source property that IS used (see remarks on
        /// <see cref="ShaderPropertyMapper.Map"/>) but has no matching PropertyMapping — surfaced
        /// per spec.md's Edge Cases ("the tool must surface this as a warning ... rather than
        /// silently dropping the value"), never silently dropped.</summary>
        public IReadOnlyList<string> Warnings { get; }

        public ShaderPropertyMappingResult(IReadOnlyList<MappedMaterialProperty> mappedProperties, IReadOnlyList<string> warnings)
        {
            MappedProperties = mappedProperties;
            Warnings = warnings;
        }
    }

    /// <summary>
    /// T016: pure, Editor-independent resolution logic (Constitution Principle VI) — takes a
    /// <see cref="ShaderConversionRuleSet"/> and a source Material's captured property values,
    /// returns the resolved target property values. Does not touch AssetDatabase or create any
    /// Unity asset; MaterialConverter (T018) is the thin Editor-facing glue that applies this
    /// result to an actual target Material.
    /// </summary>
    public static class ShaderPropertyMapper
    {
        public static ShaderPropertyMappingResult Map(ShaderConversionRuleSet rule, PCMaterial sourceMaterial)
        {
            var mappingsBySourceName = new Dictionary<string, PropertyMapping>();
            foreach (var mapping in rule.PropertyMappings)
            {
                // A rule asset that passed ShaderConversionRuleLoader validation has already had
                // its unresolvable SourcePropertyName entries reported as load-time warnings
                // (contracts §1); duplicates within one rule's own PropertyMappings are not a
                // documented invariant, so the last one wins here rather than erroring.
                mappingsBySourceName[mapping.SourcePropertyName] = mapping;
            }

            var mapped = new List<MappedMaterialProperty>();
            var warnings = new List<string>();

            foreach (var property in sourceMaterial.Properties)
            {
                if (mappingsBySourceName.TryGetValue(property.Name, out var mapping))
                {
                    mapped.Add(new MappedMaterialProperty(mapping.TargetPropertyName, property, mapping.TargetClassification));
                    continue;
                }

                if (IsUsed(property))
                {
                    warnings.Add(
                        $"Material '{sourceMaterial.Asset?.name}': Property '{property.Name}' has no mapping to " +
                        $"target shader '{rule.TargetShader?.name}' in rule '{rule.name}' and was not carried over.");
                }
            }

            return new ShaderPropertyMappingResult(mapped, warnings);
        }

        /// <summary>A Texture-kind property is "used" (spec.md Edge Cases) only when an actual
        /// Texture is assigned — an empty texture slot carries no value worth warning about.
        /// Color/Scalar-kind properties are always considered used: Unity Materials always carry
        /// a concrete value for every declared property (there is no reliable "still at the
        /// shader's default" signal to distinguish), and silently dropping one is exactly the
        /// data loss this warning exists to catch.</summary>
        private static bool IsUsed(MaterialProperty property)
        {
            return property.Kind != MaterialPropertyKind.Texture || property.TextureValue != null;
        }
    }
}
