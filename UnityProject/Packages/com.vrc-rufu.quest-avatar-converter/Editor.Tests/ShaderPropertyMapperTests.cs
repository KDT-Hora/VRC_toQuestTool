using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Materials;
using VrcRufu.QuestAvatarConverter.Pipeline;
using Object = UnityEngine.Object;

namespace VrcRufu.QuestAvatarConverter.Tests
{
    // T017: EditMode tests for ShaderPropertyMapper (T016) resolution logic.
    public class ShaderPropertyMapperTests
    {
        private readonly List<Object> _createdInstances = new List<Object>();
        private Shader _sourceShader;
        private Shader _targetShader;
        private Texture2D _mainTex;
        private Texture2D _bumpTex;

        [SetUp]
        public void SetUp()
        {
            _sourceShader = Shader.Find("Standard");
            _targetShader = Shader.Find("VRChat/Mobile/Toon Lit");
            Assert.NotNull(_sourceShader);
            Assert.NotNull(_targetShader);

            _mainTex = new Texture2D(4, 4);
            _bumpTex = new Texture2D(4, 4);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var instance in _createdInstances)
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }
            }
            _createdInstances.Clear();

            Object.DestroyImmediate(_mainTex);
            Object.DestroyImmediate(_bumpTex);
        }

        private ShaderConversionRuleSet CreateRule(params PropertyMapping[] mappings)
        {
            var rule = ShaderConversionRuleSet.CreateForTests(_sourceShader, _targetShader, mappings);
            _createdInstances.Add(rule);
            return rule;
        }

        [Test]
        public void MappedProperty_TranslatesToTargetNameAndClassification()
        {
            var rule = CreateRule(new PropertyMapping
            {
                SourcePropertyName = "_MainTex",
                TargetPropertyName = "_MainTex",
                TargetClassification = TextureClassification.Color,
            });

            var sourceMaterial = new PCMaterial(null, new[]
            {
                MaterialProperty.ForTexture("_MainTex", _mainTex, Vector2.one, Vector2.zero),
            });

            var result = ShaderPropertyMapper.Map(rule, sourceMaterial);

            Assert.AreEqual(1, result.MappedProperties.Count);
            var mapped = result.MappedProperties[0];
            Assert.AreEqual("_MainTex", mapped.TargetPropertyName);
            Assert.AreSame(_mainTex, mapped.SourceProperty.TextureValue);
            Assert.AreEqual(TextureClassification.Color, mapped.TargetClassification);
            Assert.IsEmpty(result.Warnings);
        }

        [Test]
        public void UnmappedUsedTextureProperty_ProducesWarning_AndIsNotCarriedOver()
        {
            var rule = CreateRule(new PropertyMapping
            {
                SourcePropertyName = "_MainTex",
                TargetPropertyName = "_MainTex",
                TargetClassification = TextureClassification.Color,
            });

            var sourceMaterial = new PCMaterial(null, new[]
            {
                MaterialProperty.ForTexture("_MainTex", _mainTex, Vector2.one, Vector2.zero),
                MaterialProperty.ForTexture("_BumpMap", _bumpTex, Vector2.one, Vector2.zero),
            });

            var result = ShaderPropertyMapper.Map(rule, sourceMaterial);

            Assert.AreEqual(1, result.MappedProperties.Count, "Only the mapped property should be carried over.");
            Assert.IsNotEmpty(result.Warnings);
            StringAssert.Contains("_BumpMap", result.Warnings[0]);
        }

        [Test]
        public void UnmappedUnassignedTextureProperty_ProducesNoWarning()
        {
            var rule = CreateRule(); // no mappings at all

            var sourceMaterial = new PCMaterial(null, new[]
            {
                MaterialProperty.ForTexture("_BumpMap", null, Vector2.one, Vector2.zero),
            });

            var result = ShaderPropertyMapper.Map(rule, sourceMaterial);

            Assert.IsEmpty(result.MappedProperties);
            Assert.IsEmpty(result.Warnings, "An empty texture slot has nothing to warn about.");
        }

        [Test]
        public void UnmappedColorProperty_AlwaysProducesWarning()
        {
            var rule = CreateRule(); // no mappings at all

            var sourceMaterial = new PCMaterial(null, new[]
            {
                MaterialProperty.ForColor("_Color", Color.white),
            });

            var result = ShaderPropertyMapper.Map(rule, sourceMaterial);

            Assert.IsEmpty(result.MappedProperties);
            Assert.IsNotEmpty(result.Warnings);
            StringAssert.Contains("_Color", result.Warnings[0]);
        }
    }
}
