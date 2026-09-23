using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Materials;
using VrcRufu.QuestAvatarConverter.Pipeline;
using Object = UnityEngine.Object;

namespace VrcRufu.QuestAvatarConverter.Tests
{
    // T008: EditMode tests for the T007 loader invariants
    // (contracts/extension-data-contracts.md §1).
    public class ShaderConversionRuleLoaderTests
    {
        private readonly List<Object> _createdInstances = new List<Object>();
        private Shader _sourceShader;
        private Shader _validTargetShader;
        private Shader _invalidTargetShader;

        [SetUp]
        public void SetUp()
        {
            _sourceShader = Shader.Find("Standard");
            _validTargetShader = Shader.Find("VRChat/Mobile/Toon Lit");
            _invalidTargetShader = Shader.Find("Standard");

            Assert.NotNull(_sourceShader, "Fixture requires the built-in 'Standard' shader.");
            Assert.NotNull(_validTargetShader, "Fixture requires the VRChat SDK's 'VRChat/Mobile/Toon Lit' shader.");
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
        }

        private ShaderConversionRuleSet CreateRule(Shader source, Shader target, params PropertyMapping[] mappings)
        {
            var rule = ShaderConversionRuleSet.CreateForTests(source, target, mappings);
            _createdInstances.Add(rule);
            return rule;
        }

        [Test]
        public void ValidRule_IsAccepted_WithNoErrorsOrWarnings()
        {
            var rule = CreateRule(
                _sourceShader,
                _validTargetShader,
                new PropertyMapping
                {
                    SourcePropertyName = "_MainTex",
                    TargetPropertyName = "_MainTex",
                    TargetClassification = TextureClassification.Color,
                });

            var result = ShaderConversionRuleLoader.Load(new[] { rule });

            Assert.AreEqual(1, result.ValidRules.Count);
            Assert.AreSame(rule, result.ValidRules[0]);
            Assert.IsEmpty(result.Errors);
            Assert.IsEmpty(result.Warnings);
        }

        [Test]
        public void NonVRChatMobileTarget_IsRejected()
        {
            var rule = CreateRule(_sourceShader, _invalidTargetShader);

            var result = ShaderConversionRuleLoader.Load(new[] { rule });

            Assert.IsEmpty(result.ValidRules, "A rule whose TargetShader is not VRChat/Mobile/* must not load.");
            Assert.IsNotEmpty(result.Errors);
            StringAssert.Contains("VRChat/Mobile/", result.Errors[0]);
        }

        [Test]
        public void DuplicateSourceTargetPair_IsRejected()
        {
            var first = CreateRule(_sourceShader, _validTargetShader);
            var second = CreateRule(_sourceShader, _validTargetShader);

            var result = ShaderConversionRuleLoader.Load(new[] { first, second });

            Assert.IsEmpty(result.ValidRules, "Neither rule in a duplicate SourceShader+TargetShader pair may be silently picked.");
            Assert.IsNotEmpty(result.Errors);
            StringAssert.Contains("Duplicate", result.Errors[0]);
        }

        [Test]
        public void UnresolvedSourceProperty_ProducesWarning_ButRuleStillLoads()
        {
            var rule = CreateRule(
                _sourceShader,
                _validTargetShader,
                new PropertyMapping
                {
                    SourcePropertyName = "_ThisPropertyDoesNotExistOnStandard",
                    TargetPropertyName = "_MainTex",
                    TargetClassification = TextureClassification.Color,
                });

            var result = ShaderConversionRuleLoader.Load(new[] { rule });

            Assert.AreEqual(1, result.ValidRules.Count, "An unresolved source property is a warning, not a load failure.");
            Assert.IsEmpty(result.Errors);
            Assert.IsNotEmpty(result.Warnings);
            StringAssert.Contains("_ThisPropertyDoesNotExistOnStandard", result.Warnings[0]);
        }

        [Test]
        public void MissingSourceOrTargetShader_IsRejected()
        {
            var rule = CreateRule(null, null);

            var result = ShaderConversionRuleLoader.Load(new[] { rule });

            Assert.IsEmpty(result.ValidRules);
            Assert.IsNotEmpty(result.Errors);
        }
    }
}
