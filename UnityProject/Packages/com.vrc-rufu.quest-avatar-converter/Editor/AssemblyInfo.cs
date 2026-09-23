using System.Runtime.CompilerServices;

// Lets the Editor.Tests assembly construct fixtures for internal-only, test-only factory methods
// (e.g. ShaderConversionRuleSet.CreateForTests, QuestCompatibilityRules.CreateForTests) without
// widening those types' public API surface just for testability.
[assembly: InternalsVisibleTo("com.vrc-rufu.quest-avatar-converter.Editor.Tests")]
