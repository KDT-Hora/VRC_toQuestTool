using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Materials;
using VrcRufu.QuestAvatarConverter.Pipeline;

namespace VrcRufu.QuestAvatarConverter
{
    /// <summary>
    /// T030-T032 (User Story 1 / MVP): the tool's only end-user-facing UI so far — a Source
    /// Avatar field and a Generate button, wired to <see cref="ConversionPipeline"/> (T029) with
    /// default <see cref="ConversionSettings"/> (T031). US2 (Phase 4) adds the settings controls
    /// this window doesn't expose yet; US3 (Phase 5) adds the Preview/Report panels.
    /// </summary>
    public class QuestAvatarConverterWindow : EditorWindow
    {
        /// <summary>research.md §4's default-target-shader suggestion.</summary>
        private const string DefaultTargetShaderName = "VRChat/Mobile/Toon Standard";

        /// <summary>The source document's placement-offset example (data-model.md /
        /// spec.md quickstart.md carry no other concrete default).</summary>
        private static readonly Vector3 DefaultPlacementOffset = new Vector3(2f, 0f, 0f);

        private GameObject _sourceAvatar;
        private ConversionContext _lastResult;

        [MenuItem("Tools/VRC Rufu/Quest Avatar Converter")]
        public static void ShowWindow()
        {
            GetWindow<QuestAvatarConverterWindow>("Quest Avatar Converter");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Source Avatar (PC Prefab)", EditorStyles.boldLabel);
            _sourceAvatar = (GameObject)EditorGUILayout.ObjectField(_sourceAvatar, typeof(GameObject), false);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_sourceAvatar == null))
            {
                if (GUILayout.Button("Generate", GUILayout.Height(30)))
                {
                    Generate();
                }
            }

            if (_lastResult != null)
            {
                EditorGUILayout.Space();
                DrawLastResultSummary();
            }
        }

        private void Generate()
        {
            if (_sourceAvatar == null)
            {
                return;
            }

            // FR-013: halt before ANY output is produced if AAO is absent — checked here too
            // (not just inside ConversionPipeline) so the user gets this as a dialog, not only a
            // Console log entry, before Generate does anything at all.
            if (!AAOIntegrator.IsAaoInstalled())
            {
                EditorUtility.DisplayDialog(
                    "Avatar Optimizer Required",
                    "Avatar Optimizer (AAO) is not installed in this project. Install " +
                    "com.anatawa12.avatar-optimizer before generating a Quest avatar — Trace And " +
                    "Optimize is a required part of every conversion run (FR-012/FR-013).",
                    "OK");
                return;
            }

            var loadedRules = LoadValidShaderConversionRules();
            var settings = BuildDefaultSettings(loadedRules);

            if (settings.TargetShaderRule == null)
            {
                EditorUtility.DisplayDialog(
                    "No Shader Conversion Rules Found",
                    $"No valid ShaderConversionRuleSet asset targeting '{DefaultTargetShaderName}' was found " +
                    "under Data/ShaderConversionRules/. Generation cannot proceed without a target shader rule.",
                    "OK");
                return;
            }

            // FR-022: existing-output overwrite requires an explicit, per-run confirmation dialog.
            bool ConfirmOverwrite(string outputRoot) => EditorUtility.DisplayDialog(
                "Existing Quest Output Found",
                $"Quest output already exists at:\n{outputRoot}\n\nOverwrite it in place?",
                "Overwrite",
                "Cancel");

            _lastResult = ConversionPipeline.Run(_sourceAvatar, settings, loadedRules, ConfirmOverwrite);

            foreach (var entry in _lastResult.Log)
            {
                LogToConsole(entry);
            }

            if (_lastResult.QuestAvatar?.RootPrefab != null)
            {
                EditorUtility.DisplayDialog(
                    "Quest Avatar Generated",
                    $"Quest avatar generated at:\n{_lastResult.OutputRoot}\n\nSee the Console for the full conversion log.",
                    "OK");
            }
            else
            {
                var reason = FindMostRecentErrorMessage(_lastResult) ?? "Generation did not complete — see the Console for details.";
                EditorUtility.DisplayDialog("Generation Did Not Complete", reason, "OK");
            }
        }

        private void DrawLastResultSummary()
        {
            EditorGUILayout.LabelField("Last Run", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Output: {_lastResult.OutputRoot}");
            EditorGUILayout.LabelField($"Log entries: {_lastResult.Log.Count}");
        }

        private static void LogToConsole(ConversionLogEntry entry)
        {
            switch (entry.Level)
            {
                case ConversionLogLevel.Error:
                    Debug.LogError($"[Quest Avatar Converter] {entry.Message}", entry.RelatedAsset);
                    break;
                case ConversionLogLevel.Warning:
                    Debug.LogWarning($"[Quest Avatar Converter] {entry.Message}", entry.RelatedAsset);
                    break;
                default:
                    Debug.Log($"[Quest Avatar Converter] {entry.Message}", entry.RelatedAsset);
                    break;
            }
        }

        private static string FindMostRecentErrorMessage(ConversionContext context)
        {
            for (var i = context.Log.Count - 1; i >= 0; i--)
            {
                if (context.Log[i].Level == ConversionLogLevel.Error)
                {
                    return context.Log[i].Message;
                }
            }
            return null;
        }

        private static ConversionSettings BuildDefaultSettings(IReadOnlyList<ShaderConversionRuleSet> loadedRules)
        {
            ShaderConversionRuleSet defaultRule = null;
            foreach (var rule in loadedRules)
            {
                if (rule.TargetShader != null && rule.TargetShader.name == DefaultTargetShaderName)
                {
                    defaultRule = rule;
                    break;
                }
            }

            return new ConversionSettings
            {
                TargetShaderRule = defaultRule,
                PlacementOffset = DefaultPlacementOffset,
                MaxTextureSize = 1024,
                MergeTexturesEnabled = true,
                ResizeTexturesEnabled = true,
                AddAaoComponentEnabled = true,
            };
        }

        /// <summary>Loads every <see cref="ShaderConversionRuleSet"/> asset in the project and
        /// returns only the ones that pass <see cref="ShaderConversionRuleLoader"/>'s contract
        /// invariants — a raw, unvalidated rule asset is never handed to the pipeline. Load-time
        /// errors/warnings are surfaced to the Console (contracts §1: "not silently ignored").</summary>
        private static IReadOnlyList<ShaderConversionRuleSet> LoadValidShaderConversionRules()
        {
            var candidates = new List<ShaderConversionRuleSet>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(ShaderConversionRuleSet)}"))
            {
                var rule = AssetDatabase.LoadAssetAtPath<ShaderConversionRuleSet>(AssetDatabase.GUIDToAssetPath(guid));
                if (rule != null)
                {
                    candidates.Add(rule);
                }
            }

            var result = ShaderConversionRuleLoader.Load(candidates);
            foreach (var error in result.Errors)
            {
                Debug.LogError($"[Quest Avatar Converter] Shader rule load error: {error}");
            }
            foreach (var warning in result.Warnings)
            {
                Debug.LogWarning($"[Quest Avatar Converter] Shader rule load warning: {warning}");
            }

            return result.ValidRules;
        }
    }
}
