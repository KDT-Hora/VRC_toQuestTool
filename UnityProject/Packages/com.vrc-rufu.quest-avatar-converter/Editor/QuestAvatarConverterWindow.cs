using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Materials;
using VrcRufu.QuestAvatarConverter.Pipeline;
using VrcRufu.QuestAvatarConverter.Textures;

namespace VrcRufu.QuestAvatarConverter
{
    /// <summary>
    /// T030-T045 (User Stories 1, 2 &amp; 3): Source Avatar field, Generate button, the exposed
    /// settings controls (target shader, placement offset, max texture size, and the Merge
    /// Textures / Resize Textures / Add AAO Component toggles — FR-005/FR-004/FR-009/FR-018), a
    /// post-generation Report panel (performance metrics + individually-listed, per-item
    /// Remove/Keep compatibility findings — FR-015/FR-016/FR-016a/FR-019/FR-021), and a
    /// no-disk-writes Material Preview panel (FR-017) — all wired to the pipeline built in
    /// Phase 2.
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
        private ConversionReport.Report _lastReport;

        private List<ShaderConversionRuleSet> _availableRules = new List<ShaderConversionRuleSet>();
        private string[] _ruleDisplayNames = System.Array.Empty<string>();
        private int _selectedRuleIndex;

        private List<QuestCompatibilityRules> _availableCompatibilityRules = new List<QuestCompatibilityRules>();

        private Vector3 _placementOffset = DefaultPlacementOffset;
        private int _maxTextureSize = 1024;
        private bool _mergeTexturesEnabled = true;
        private bool _resizeTexturesEnabled = true;
        private bool _addAaoComponentEnabled = true;

        private Material _previewMaterial;
        private readonly List<PreviewResult> _previewResults = new List<PreviewResult>();
        private Vector2 _reportScroll;

        private sealed class PreviewResult
        {
            public TextureClassification Classification;
            public int SourceCount;
            public int SourceCanvasWidth;
            public int SourceCanvasHeight;
            public int FinalWidth;
            public int FinalHeight;
            public Texture2D ComposedPreview; // in-memory only, never saved (FR-017)
        }

        [MenuItem("Tools/VRC Rufu/Quest Avatar Converter")]
        public static void ShowWindow()
        {
            GetWindow<QuestAvatarConverterWindow>("Quest Avatar Converter");
        }

        private void OnEnable()
        {
            RefreshAvailableRules();
            RefreshAvailableCompatibilityRules();
        }

        private void OnDisable()
        {
            ClearPreviewResults();
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

            EditorGUILayout.Space();
            DrawPreviewPanel();

            if (_lastReport != null)
            {
                EditorGUILayout.Space();
                DrawReportPanel();
            }
            else if (_lastResult != null)
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
            _lastReport = null;

            foreach (var entry in _lastResult.Log)
            {
                LogToConsole(entry);
            }

            if (_lastResult.QuestAvatar?.RootPrefab != null)
            {
                // T043/FR-019: aggregate performance metrics + compatibility findings (incl.
                // PhysBone, FR-016a) into a single displayable report right after a successful
                // generation, so the Report panel (T044) has something to show without a
                // separate step.
                RefreshAvailableCompatibilityRules();
                _lastReport = ConversionReport.Analyze(_lastResult, _availableCompatibilityRules);

                EditorUtility.DisplayDialog(
                    "Quest Avatar Generated",
                    $"Quest avatar generated at:\n{_lastResult.OutputRoot}\n\nSee the Report panel below, or the Console, for the full conversion log.",
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

        /// <summary>Reloads every <see cref="QuestCompatibilityRules"/> asset in the project and
        /// keeps only the ones that pass <see cref="QuestCompatibilityRulesLoader"/>'s contract
        /// invariants (contracts §2: "not silently skipped").</summary>
        private void RefreshAvailableCompatibilityRules()
        {
            var candidates = new List<QuestCompatibilityRules>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(QuestCompatibilityRules)}"))
            {
                var rules = AssetDatabase.LoadAssetAtPath<QuestCompatibilityRules>(AssetDatabase.GUIDToAssetPath(guid));
                if (rules != null)
                {
                    candidates.Add(rules);
                }
            }

            var result = QuestCompatibilityRulesLoader.Load(candidates);
            foreach (var error in result.Errors)
            {
                Debug.LogError($"[Quest Avatar Converter] Compatibility rule load error: {error}");
            }
            foreach (var warning in result.Warnings)
            {
                Debug.LogWarning($"[Quest Avatar Converter] Compatibility rule load warning: {warning}");
            }

            _availableCompatibilityRules = result.ValidRules.ToList();
        }

        // ===== T044: Report panel =====

        private void DrawReportPanel()
        {
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);

            var metrics = _lastReport.PerformanceMetrics;
            if (metrics != null)
            {
                EditorGUILayout.LabelField($"Triangles: {metrics.TriangleCount:N0}");
                EditorGUILayout.LabelField($"Materials: {metrics.MaterialCount}");
                EditorGUILayout.LabelField($"Skinned Mesh Renderers: {metrics.SkinnedMeshRendererCount}");
                EditorGUILayout.LabelField($"Bones: {metrics.BoneCount}");
                EditorGUILayout.LabelField($"Textures: {metrics.TextureCount}");
                EditorGUILayout.LabelField($"Estimated Texture Memory: {EditorUtility.FormatBytes(metrics.EstimatedTextureMemoryBytes)}");
            }

            var physBone = _lastReport.PhysBoneMetrics;
            if (physBone != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    $"PhysBones: {physBone.PhysBoneComponentCount} components, {physBone.PhysBoneColliderCount} colliders, " +
                    $"{physBone.PhysBoneAffectedTransformCount} affected transforms, {physBone.PhysBoneCollisionCheckCount} collision checks");
                EditorGUILayout.LabelField($"Quest Performance Rank: {physBone.ResultingRank}");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Compatibility Findings ({_lastReport.CompatibilityFindings.Count})", EditorStyles.boldLabel);

            if (_lastReport.CompatibilityFindings.Count == 0)
            {
                EditorGUILayout.HelpBox("No flagged objects.", MessageType.Info);
            }
            else
            {
                _reportScroll = EditorGUILayout.BeginScrollView(_reportScroll, GUILayout.Height(160));
                foreach (var finding in _lastReport.CompatibilityFindings)
                {
                    DrawFinding(finding);
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.HelpBox(
                    "Selecting neither Remove nor Keep leaves the object untouched (FR-021: the tool " +
                    "never auto-removes a flagged component). Apply Decisions only acts on items you've " +
                    "explicitly marked Remove.",
                    MessageType.None);

                if (GUILayout.Button("Apply Decisions"))
                {
                    ApplyCompatibilityDecisions();
                }
            }
        }

        private static void DrawFinding(CompatibilityFinding finding)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    var severityLabel = finding.Severity == CompatibilityFinding.FindingSeverity.BlockingIfUnaddressed ? "BLOCKING" : "Warning";
                    EditorGUILayout.LabelField($"[{severityLabel}] {(finding.TargetObject != null ? finding.TargetObject.name : "(unknown object)")}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(finding.Reason, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.ObjectField("Object", finding.TargetObject, typeof(Object), true);
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(70)))
                {
                    var isRemove = finding.UserDecision == CompatibilityFinding.RemovalDecision.Remove;
                    var isKeep = finding.UserDecision == CompatibilityFinding.RemovalDecision.Keep;

                    if (GUILayout.Toggle(isRemove, "Remove", "Button"))
                    {
                        finding.UserDecision = CompatibilityFinding.RemovalDecision.Remove;
                    }
                    else if (isRemove)
                    {
                        finding.UserDecision = CompatibilityFinding.RemovalDecision.Undecided;
                    }

                    if (GUILayout.Toggle(isKeep, "Keep", "Button"))
                    {
                        finding.UserDecision = CompatibilityFinding.RemovalDecision.Keep;
                    }
                    else if (isKeep)
                    {
                        finding.UserDecision = CompatibilityFinding.RemovalDecision.Undecided;
                    }
                }
            }
        }

        /// <summary>Destroys only the Components the user explicitly marked Remove — never touches
        /// an Undecided or Keep item (FR-021).</summary>
        private void ApplyCompatibilityDecisions()
        {
            var removed = 0;
            foreach (var finding in _lastReport.CompatibilityFindings)
            {
                if (finding.UserDecision != CompatibilityFinding.RemovalDecision.Remove || finding.TargetObject == null)
                {
                    continue;
                }

                if (finding.TargetObject is Component component && !(component is Transform))
                {
                    Object.DestroyImmediate(component, true);
                    removed++;
                }
            }

            if (removed > 0 && _lastResult?.QuestAvatar?.RootPrefab != null)
            {
                EditorUtility.SetDirty(_lastResult.QuestAvatar.RootPrefab);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[Quest Avatar Converter] Applied {removed} Remove decision(s).");
        }

        // ===== T045: Preview panel (FR-017 — never calls TextureAssetWriter.Write / never writes to disk) =====

        private void DrawPreviewPanel()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                var newMaterial = (Material)EditorGUILayout.ObjectField("Material", _previewMaterial, typeof(Material), false);
                if (newMaterial != _previewMaterial)
                {
                    _previewMaterial = newMaterial;
                }

                using (new EditorGUI.DisabledScope(_previewMaterial == null))
                {
                    if (GUILayout.Button("Preview", GUILayout.Width(70)))
                    {
                        RunPreview();
                    }
                }
            }

            foreach (var result in _previewResults)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    if (result.ComposedPreview != null)
                    {
                        GUILayout.Label(result.ComposedPreview, GUILayout.Width(64), GUILayout.Height(64));
                    }
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField($"{result.Classification} ({result.SourceCount} source texture(s))", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"Merged canvas: {result.SourceCanvasWidth}x{result.SourceCanvasHeight}");
                        EditorGUILayout.LabelField($"Final resolution: {result.FinalWidth}x{result.FinalHeight}");
                    }
                }
            }
        }

        /// <summary>Dry-runs ShaderPropertyMapper/TextureTypeClassifier/TextureAtlasGenerator/
        /// TextureResizer plus an in-memory-only composite (TextureAssetWriter.CompositeAndResize)
        /// for the selected Material — no asset is written to disk (FR-017).</summary>
        private void RunPreview()
        {
            ClearPreviewResults();
            if (_previewMaterial == null || _previewMaterial.shader == null)
            {
                return;
            }

            RefreshAvailableRules();
            if (_availableRules.Count == 0 || _selectedRuleIndex < 0 || _selectedRuleIndex >= _availableRules.Count)
            {
                return;
            }

            var rule = _availableRules.FirstOrDefault(r => r.SourceShader == _previewMaterial.shader)
                       ?? _availableRules[_selectedRuleIndex];

            var pcMaterial = AssetResolver.BuildPCMaterialFromAsset(_previewMaterial);
            var mappingResult = ShaderPropertyMapper.Map(rule, pcMaterial);
            var classified = TextureTypeClassifier.Classify(mappingResult);

            foreach (var entry in classified)
            {
                var sources = entry.Value;
                if (!_mergeTexturesEnabled && sources.Count > 1)
                {
                    sources = new List<PCTexture> { sources[0] };
                }

                var layout = TextureAtlasGenerator.Generate(sources);
                var maxSize = _resizeTexturesEnabled ? _maxTextureSize : int.MaxValue;
                var composed = TextureAssetWriter.CompositeAndResize(layout, maxSize, out var finalWidth, out var finalHeight);

                _previewResults.Add(new PreviewResult
                {
                    Classification = entry.Key,
                    SourceCount = sources.Count,
                    SourceCanvasWidth = layout.Width,
                    SourceCanvasHeight = layout.Height,
                    FinalWidth = finalWidth,
                    FinalHeight = finalHeight,
                    ComposedPreview = composed,
                });
            }
        }

        private void ClearPreviewResults()
        {
            foreach (var result in _previewResults)
            {
                if (result.ComposedPreview != null)
                {
                    Object.DestroyImmediate(result.ComposedPreview);
                }
            }
            _previewResults.Clear();
        }
    }
}
