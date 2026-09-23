using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Materials;
using VrcRufu.QuestAvatarConverter.Pipeline;

namespace VrcRufu.QuestAvatarConverter
{
    /// <summary>
    /// T030-T037 (User Stories 1 &amp; 2): Source Avatar field, Generate button, and the exposed
    /// settings controls (target shader, placement offset, max texture size, and the Merge
    /// Textures / Resize Textures / Add AAO Component toggles — FR-005/FR-004/FR-009/FR-018),
    /// wired to <see cref="ConversionPipeline"/> (T029). US3 (Phase 5) adds the Preview/Report
    /// panels this window doesn't have yet.
    /// </summary>
    /// <remarks>
    /// FR-018 names four independent optional steps, but a "Duplicate Avatar" toggle was
    /// deliberately NOT added here: data-model.md's ConversionSettings table has no field for it,
    /// and unlike Merge/Resize/AddAAO — each a well-defined "skip this refinement" — "duplicate
    /// avatar disabled" has no safe, unambiguous meaning under Constitution I (it cannot mean
    /// "operate on the PC avatar in place"; the only sound reading, "update the existing Quest
    /// output without re-duplicating," is a materially different feature, not a simple flag).
    /// Confirmed with the project owner (2026-09-23) not to invent that behavior here — see
    /// tasks.md's Phase 2/T029 implementation note.
    /// </remarks>
    public class QuestAvatarConverterWindow : EditorWindow
    {
        /// <summary>research.md §4's default-target-shader suggestion.</summary>
        private const string DefaultTargetShaderName = "VRChat/Mobile/Toon Standard";

        /// <summary>The source document's placement-offset example (data-model.md /
        /// spec.md quickstart.md carry no other concrete default).</summary>
        private static readonly Vector3 DefaultPlacementOffset = new Vector3(2f, 0f, 0f);

        private GameObject _sourceAvatar;
        private ConversionContext _lastResult;

        private List<ShaderConversionRuleSet> _availableRules = new List<ShaderConversionRuleSet>();
        private string[] _ruleDisplayNames = System.Array.Empty<string>();
        private int _selectedRuleIndex;

        private Vector3 _placementOffset = DefaultPlacementOffset;
        private int _maxTextureSize = 1024;
        private bool _mergeTexturesEnabled = true;
        private bool _resizeTexturesEnabled = true;
        private bool _addAaoComponentEnabled = true;

        [MenuItem("Tools/VRC Rufu/Quest Avatar Converter")]
        public static void ShowWindow()
        {
            GetWindow<QuestAvatarConverterWindow>("Quest Avatar Converter");
        }

        private void OnEnable()
        {
            RefreshAvailableRules();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Source Avatar (PC Prefab)", EditorStyles.boldLabel);
            _sourceAvatar = (GameObject)EditorGUILayout.ObjectField(_sourceAvatar, typeof(GameObject), false);

            EditorGUILayout.Space();
            DrawSettings();

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

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

            // T034 (FR-005): target-shader dropdown, populated from every loaded (and
            // contract-valid) ShaderConversionRuleSet asset.
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_availableRules.Count == 0)
                {
                    EditorGUILayout.HelpBox("No valid ShaderConversionRuleSet assets found under Data/ShaderConversionRules/.", MessageType.Warning);
                }
                else
                {
                    _selectedRuleIndex = EditorGUILayout.Popup("Target Shader", _selectedRuleIndex, _ruleDisplayNames);
                }
                if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                {
                    RefreshAvailableRules();
                }
            }

            // T035 (FR-004): placement offset.
            _placementOffset = EditorGUILayout.Vector3Field("Placement Offset", _placementOffset);

            // T036 (FR-009): max texture size.
            _maxTextureSize = EditorGUILayout.IntField("Max Texture Size", _maxTextureSize);
            if (_maxTextureSize < 1)
            {
                _maxTextureSize = 1;
            }

            // T037 (FR-018): independent optional-step toggles (Duplicate Avatar excluded — see
            // this class's remarks).
            _mergeTexturesEnabled = EditorGUILayout.Toggle("Merge Textures", _mergeTexturesEnabled);
            _resizeTexturesEnabled = EditorGUILayout.Toggle("Resize Textures", _resizeTexturesEnabled);
            _addAaoComponentEnabled = EditorGUILayout.Toggle("Add AAO Component", _addAaoComponentEnabled);
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

            RefreshAvailableRules();
            var settings = BuildSettingsFromUi();

            if (settings.TargetShaderRule == null)
            {
                EditorUtility.DisplayDialog(
                    "No Shader Conversion Rule Selected",
                    "No valid ShaderConversionRuleSet is selected. Generation cannot proceed without a target shader rule.",
                    "OK");
                return;
            }

            // FR-022: existing-output overwrite requires an explicit, per-run confirmation dialog.
            bool ConfirmOverwrite(string outputRoot) => EditorUtility.DisplayDialog(
                "Existing Quest Output Found",
                $"Quest output already exists at:\n{outputRoot}\n\nOverwrite it in place?",
                "Overwrite",
                "Cancel");

            _lastResult = ConversionPipeline.Run(_sourceAvatar, settings, _availableRules, ConfirmOverwrite);

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

        private ConversionSettings BuildSettingsFromUi()
        {
            ShaderConversionRuleSet selectedRule = null;
            if (_availableRules.Count > 0 && _selectedRuleIndex >= 0 && _selectedRuleIndex < _availableRules.Count)
            {
                selectedRule = _availableRules[_selectedRuleIndex];
            }

            return new ConversionSettings
            {
                TargetShaderRule = selectedRule,
                PlacementOffset = _placementOffset,
                MaxTextureSize = _maxTextureSize,
                MergeTexturesEnabled = _mergeTexturesEnabled,
                ResizeTexturesEnabled = _resizeTexturesEnabled,
                AddAaoComponentEnabled = _addAaoComponentEnabled,
            };
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

        /// <summary>Reloads every <see cref="ShaderConversionRuleSet"/> asset in the project and
        /// keeps only the ones that pass <see cref="ShaderConversionRuleLoader"/>'s contract
        /// invariants — a raw, unvalidated rule asset is never offered in the dropdown or handed
        /// to the pipeline. Load-time errors/warnings are surfaced to the Console (contracts §1:
        /// "not silently ignored"). Preserves the current selection by TargetShader name across a
        /// refresh where possible, defaulting to research.md §4's suggested default otherwise.</summary>
        private void RefreshAvailableRules()
        {
            var previousSelectionName = _availableRules.Count > 0 && _selectedRuleIndex >= 0 && _selectedRuleIndex < _availableRules.Count
                ? _availableRules[_selectedRuleIndex].TargetShader?.name
                : DefaultTargetShaderName;

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

            _availableRules = result.ValidRules.ToList();
            _ruleDisplayNames = _availableRules
                .Select(r => $"{(r.TargetShader != null ? r.TargetShader.name : "(no target)")} ({r.name})")
                .ToArray();

            var restoredIndex = _availableRules.FindIndex(r => r.TargetShader != null && r.TargetShader.name == previousSelectionName);
            _selectedRuleIndex = restoredIndex >= 0 ? restoredIndex : 0;
        }
    }
}
