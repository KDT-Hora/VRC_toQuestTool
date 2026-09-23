---

description: "Task list template for feature implementation"
---

# Tasks: Quest Avatar Converter

**Input**: Design documents from `/specs/001-quest-avatar-converter/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/extension-data-contracts.md, quickstart.md (all present)

**Tests**: Constitution Principle VI requires automated tests for Editor-independent core logic
(shader property mapping, atlas placement math, resize math, PhysBone threshold evaluation, and
the two extension-data loaders) alongside the feature that introduces it — these test tasks are
therefore included, scoped to that core logic only. UI/Editor-glue code is validated manually via
quickstart.md instead, per plan.md's Testing strategy.

**Organization**: Tasks are grouped by user story (US1/US2/US3, matching spec.md's P1/P2/P3) so
each story is independently implementable, testable, and deliverable as an increment.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- File paths below are relative to the UPM package root
  `UnityProject/Packages/com.vrc-rufu.quest-avatar-converter/` unless stated otherwise (see plan.md
  Project Structure). `UnityProject/` is a local Unity 2022.3.22f1 dev/test project created during
  `/speckit-implement` to host and compile this package.

---

## ✅ Setup checkpoint verified (2026-09-23)

Setup (T001-T004) is done and its checkpoint — "package compiles empty; test assembly is
discoverable by the Unity Test Runner" — **passed**: `unity test` completed with exit code 0
against Unity **2022.3.22f1** (7/7 tests passed — VRChat SDK's own EditMode tests; this
package's own `Editor.Tests` assembly is still empty at this point, populated starting T005+).

Note for future clones/machines: this git package (`com.anatawa12.avatar-optimizer`, resolved via
git URL in `Packages/manifest.json`) ships internal `csc.rsp`/`.ruleset` files as **symlinks**,
which causes compile errors (`CS2001`/`CS8035`, e.g. "Source file '...\.csc.rsp.nullsafe' could
not be found" / "Error reading ruleset file ... Data at the root level is invalid") if they don't
survive Unity's package resolution intact.

Root cause turned out to be two layered issues, discovered in this order:
1. With Windows Developer Mode off or `core.symlinks=false`, git checks these out as plain text
   files containing the link *target path* instead of real symlinks.
2. **Even after fixing (1)** — Developer Mode on, `core.symlinks=true` globally and repo-local —
   the symlinked files still came out **missing entirely**. Root cause: Unity's own internal git
   package resolver does not reliably create real Windows symlinks, independent of the system git
   config (verified: a plain `git clone` of the same repo with the system `git.exe` correctly
   creates real symlinks; only Unity's resolver drops them).

Fix for (2) — the one that actually matters once Developer Mode is on — is scripted:
`UnityProject/Scripts/fix-avatar-optimizer-symlinks.ps1`. It clones AAO fresh with the system git
(which handles the symlinks correctly) and copies the resolved file content over the
missing/broken paths under `Library/PackageCache/com.anatawa12.avatar-optimizer@*/`. Re-run it any
time this specific compile error reappears (e.g. after deleting `Library/PackageCache` or on a
fresh clone/machine).

Next step: **T005**.

---

## Phase 1: Setup

**Purpose**: Project/package initialization.

- [X] T001 Create the UPM package skeleton (`package.json`, `Editor/`, `Editor.Tests/`, `Data/`
      folders) at `UnityProject/Packages/com.vrc-rufu.quest-avatar-converter/` per plan.md Project
      Structure
- [X] T002 [P] Create `Editor/com.vrc-rufu.quest-avatar-converter.Editor.asmdef` referencing
      `VRC.SDKBase`, `VRC.SDK3A`, `VRC.SDK3A.Editor`, `VRC.SDK3.Dynamics.PhysBone`, and
      `com.anatawa12.avatar-optimizer.runtime` (research.md §2/§2b/§3) as assembly references
- [X] T003 [P] Create `Editor.Tests/com.vrc-rufu.quest-avatar-converter.Editor.Tests.asmdef`
      referencing `UnityEngine.TestRunner`/`UnityEditor.TestRunner` and the Editor asmdef above, per
      Constitution Principle VI (dedicated EditMode test assembly)
- [X] T004 [P] Declare package dependencies in `package.json` for VRChat SDK3 - Avatars 3.10.5 and
      Avatar Optimizer 1.9.19 (research.md §1–§3); `nadena.dev.ndmf` 1.14.8 and `com.vrchat.base`
      3.10.5 are declared as project-level dependencies in `UnityProject/Packages/manifest.json`
      (git URL / embedded-local respectively) since standard UPM does not read AAO's
      `vpmDependencies` field (research.md §2b); targeting Unity 2022.3.22f1 locally, matching the
      documented supported-version target (research.md §1)

**Checkpoint**: Package compiles empty; test assembly is discoverable by the Unity Test Runner.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The full non-UI conversion engine and its externalized rule data — every user story's
UI is a thin layer on top of this. No user-story phase can begin until this phase is complete.

**⚠️ CRITICAL**: This phase implements Constitution Principles I–VI directly; do not let any task
here reach into `UnityEditor` UI code.

- [X] T005 [P] Define `ConversionContext`, `ConversionSettings`, `ConversionLogEntry` in
      `Editor/Pipeline/ConversionContext.cs` exactly per data-model.md's "Core Run-Scoped Entities"
      table — `MaterialMap` MUST be keyed by `PCMaterial` (one entry per distinct source Material,
      enforcing FR-011 sharing by construction) and `TextureMap` MUST be keyed by
      `(PCMaterial, TextureClassification)` (enforcing FR-007/FR-008 per-Material-per-type scoping).
      Also defines `CompatibilityFinding`/`PhysBoneMetrics`/`PerformanceMetrics` (data-model.md's
      "Compatibility & Performance Entities") in the same file, since ConversionContext references
      them directly; the logic that *populates* them is still Phase 5 (T039-T042).
- [X] T006 [P] Define the Avatar/Renderer/Material/Texture domain types (`PCAvatar`, `QuestAvatar`,
      `RendererRef`, `PCMaterial`, `QuestMaterial`, `PCTexture`, `QuestTexture`, `MaterialProperty`,
      `TextureClassification` enum, `AtlasLayout`, `AtlasPlacement`) in
      `Editor/Pipeline/DomainTypes.cs` per data-model.md's "Avatar & Renderer Entities" / "Material &
      Shader Entities" / "Texture Entities" tables
- [X] T007 [P] Implement the `ShaderConversionRuleSet`/`PropertyMapping` ScriptableObject schema in
      `Editor/Materials/ShaderConversionRuleSet.cs` (+ loader in `ShaderConversionRuleLoader.cs` —
      **deviates from this task's originally-planned single file
      `Editor/Materials/IShaderConversionRule.cs`**: a real Unity bug was hit and fixed during
      implementation, see the "Implementation note" below) enforcing
      contracts/extension-data-contracts.md §1 invariants verbatim: `TargetShader` MUST be one of
      VRChat's own `VRChat/Mobile/*` shaders (load fails otherwise, research.md §4);
      `SourceShader`+`TargetShader` pairs MUST be unique across loaded assets (load-time error, not
      silent pick-one); unresolvable `SourcePropertyName` entries MUST be reported as a load-time
      warning
- [X] T008 [P] [Tests] EditMode tests for the T007 loader invariants (duplicate pair rejected,
      non-`VRChat/Mobile/*` target rejected, unresolved source property warned) in
      `Editor.Tests/ShaderConversionRuleLoaderTests.cs`
- [X] T009 [P] Author default `ShaderConversionRuleSet` assets targeting `VRChat/Mobile/Toon Lit`
      and `VRChat/Mobile/Toon Standard` (research.md §4) under `Data/ShaderConversionRules/`
      (`Standard_To_ToonLit.asset`, `Standard_To_ToonStandard.asset`; PC-side source shader is
      Unity's built-in `Standard`, the practical universal-default baseline — additional
      per-source-shader rules, e.g. for Poiyomi/lilToon, are added the same way without code
      changes per Constitution III)
- [X] T010 [P] Implement the `QuestCompatibilityRules` ScriptableObject schema
      (`FlaggedComponentRule[]`, `PhysBoneThresholds` with a REQUIRED non-empty `SourceCitation`) in
      `Editor/Pipeline/QuestCompatibilityRules.cs` enforcing contracts §2 invariants verbatim
- [X] T011 [P] [Tests] EditMode tests for the T010 loader invariants (unresolved
      `ComponentTypeName` warned, missing `SourceCitation` treated as a rule-authoring error) in
      `Editor.Tests/QuestCompatibilityRulesLoaderTests.cs`
- [X] T012 [P] Author the default `QuestCompatibilityRules` asset with VRChat's published Quest
      PhysBone thresholds (research.md §5 table: 0/4/6/8 components, 0/16/32/64 affected transforms,
      0/4/8/16 colliders, 0/16/32/64 collision checks at Excellent/Good/Medium/Poor) plus the hard
      256-affected-transform-per-component cap, citing the source page, under
      `Data/QuestCompatibilityRules/` (`DefaultQuestCompatibilityRules.asset`; `FlaggedComponents`
      shipped empty — this task only specifies the PhysBone table, and FlaggedComponentRule entries
      are Inspector-editable per-project data per Constitution III, not something to invent
      unsourced defaults for)

**Implementation note (T007/T009): a real Unity ScriptableObject-serialization bug, not a
process/environment issue.** Authoring the two default `ShaderConversionRuleSet` assets initially
failed silently — `AssetDatabase.LoadAssetAtPath` returned `null` for both, with the Editor log
showing `'PropertyMapping' is missing the class attribute 'ExtensionOfNativeClass'!` and the
generated asset YAML showing a broken `m_Script: {fileID: 0}` (no GUID) instead of a real script
reference. Root cause: the original `Editor/Materials/IShaderConversionRule.cs` held multiple
types (`PropertyMapping`, `ShaderConversionRuleSet`, `ShaderConversionRuleLoadResult`,
`ShaderConversionRuleLoader`) and its filename matched **none** of them — Unity's MonoScript↔GUID
reverse lookup for a ScriptableObject asset is unreliable when the declaring file's name doesn't
match its main type. `Editor/Pipeline/QuestCompatibilityRules.cs` (filename matches its main type)
serialized correctly from the start, which is what pointed at the real cause. Fix: split into
`ShaderConversionRuleSet.cs` (schema types) and `ShaderConversionRuleLoader.cs` (loader), each
filename matching its main type — **every future file introducing a
`ScriptableObject`/`MonoBehaviour`-derived type MUST follow this same one-file-one-matching-name
rule**, regardless of what filename an earlier task description suggested. The two default assets
were authored via a small one-off Editor utility
(`Editor/Tools/DefaultRuleAssetAuthoringTool.cs`, run once via
`unity run ... -executeMethod VrcRufu.QuestAvatarConverter.Materials.DefaultRuleAssetAuthoringTool.Run`)
rather than hand-written asset YAML, since letting Unity's own serializer produce the file is what
surfaced (and let us confirm the fix for) this bug — hand-authored YAML would have LOOKED valid
while hiding the same defect. Regression coverage: `Editor.Tests/DefaultRuleAssetsTests.cs` loads
both default assets via `AssetDatabase` and asserts their data round-tripped correctly.
- [X] T013 Implement `AssetResolver` in `Editor/Pipeline/AssetResolver.cs`: traverse only each
      Renderer's static `sharedMaterial`(s) → Material → Texture at conversion time (FR-003);
      Animator/Animation-Clip-driven material/texture swaps MUST NOT be discovered (out of scope
      per spec Assumptions); the VRC Avatar Descriptor reference IS followed
- [X] T014 Implement `AvatarDuplicator` in `Editor/Pipeline/AvatarDuplicator.cs`: produce a fully
      independent Prefab copy, never a Prefab Variant (FR-023), placed at the PC avatar's position
      plus `ConversionSettings.PlacementOffset` (FR-004), under
      `Assets/<QuestConvertedRoot>/<AvatarName>/Avatar/` (FR-002)
- [X] T015 Implement `ExistingOutputDetector` in `Editor/Pipeline/ExistingOutputDetector.cs`:
      detect prior Quest output at the target path and require an explicit user-approved
      confirmation before any overwrite occurs (FR-022) — no silent overwrite path may exist
- [X] T016 Implement `ShaderPropertyMapper` as pure, Editor-independent resolution logic (takes a
      `ShaderConversionRuleSet` + source Material property values, returns target property values)
      in `Editor/Materials/ShaderPropertyMapper.cs` (Constitution Principle VI)
- [X] T017 [P] [Tests] EditMode tests for `ShaderPropertyMapper` resolution logic (mapped
      properties translate correctly; an unmapped-but-used source Property produces a warning per
      the spec's Edge Cases, not a silent drop) in `Editor.Tests/ShaderPropertyMapperTests.cs`
- [X] T018 Implement `MaterialConverter` in `Editor/Materials/MaterialConverter.cs` (FR-006):
      look up the `PCMaterial.Shader` in loaded `ShaderConversionRuleSet`s; if none matches, record
      that Material as an explicit conversion failure (never guess, never silently skip)
- [X] T019 [P] Implement `GpuInstancingApplier` in `Editor/Materials/GpuInstancingApplier.cs`:
      enable GPU Instancing on every generated `QuestMaterial` by default via the mechanism the
      target shader requires (FR-020a)
- [X] T020 [P] Implement `TextureTypeClassifier` in `Editor/Textures/TextureTypeClassifier.cs`:
      classify each Material texture property into Color/Normal/Mask/Emission (FR-008), driven by
      `PropertyMapping.TargetClassification`
- [X] T021 Implement `TextureAtlasGenerator` as pure placement-math logic (inputs: source texture
      dimensions/aspect ratios for one `(Material, TextureClassification)` group; output:
      `AtlasLayout` with aspect-ratio-preserving `AtlasPlacement` rects) in
      `Editor/Textures/TextureAtlasGenerator.cs` (Constitution Principle VI) — MUST NOT combine
      textures of different `TextureClassification`s into one layout (FR-008)
- [X] T022 [P] [Tests] EditMode tests for `TextureAtlasGenerator` placement math (aspect ratio
      preserved for mismatched source sizes, e.g. 2048×2048 + 1024×2048 inputs; single-texture case
      is a no-op layout) in `Editor.Tests/TextureAtlasPlacementTests.cs`
- [X] T023 Implement `TextureResizer` as pure resize-math logic (input: width/height + configured
      max size; output: target width/height) in `Editor/Textures/TextureResizer.cs` — longest edge
      MUST NOT exceed the configured max (default 1024, FR-009), aspect ratio MUST be preserved, and
      a texture already at or below the max MUST NOT be upscaled
- [X] T024 [P] [Tests] EditMode tests for `TextureResizer` (oversized/undersized/already-at-max
      inputs against default 1024 and a custom max) in `Editor.Tests/TextureResizerTests.cs`
- [X] T025 [P] Implement `TextureUvTilingDetector` in `Editor/Textures/TextureUvTilingDetector.cs`:
      detect non-default UV Scale/Offset on a Material being merged and append a
      `ConversionLogEntry` warning (Edge Case) — MUST NOT attempt UV remapping (out of scope for v1)
- [X] T026 Implement `TextureAssetWriter` in `Editor/Textures/TextureAssetWriter.cs`: render
      `TextureAtlasGenerator`/`TextureResizer` output to an actual `Texture2D`, always encoded as
      lossless PNG, preserving an alpha channel whenever any source Texture used one (FR-009),
      written under `Assets/<QuestConvertedRoot>/<AvatarName>/Textures/`
- [X] T027 Implement `RendererMaterialReplacer` in `Editor/Pipeline/RendererMaterialReplacer.cs`:
      point each Quest-side Renderer at its `QuestMaterial` via `ConversionContext.MaterialMap`
      (FR-010), so Renderers sharing a source `PCMaterial` end up sharing one `QuestMaterial`
      (FR-011)
- [X] T028 Implement `AAOIntegrator` in `Editor/Pipeline/AAOIntegrator.cs`: detect whether AAO is
      installed and halt generation with a clear, actionable message before any output is produced
      if absent (FR-013); otherwise add the AAO `Trace And Optimize` Avatar Global Component to the
      Quest avatar root (FR-012) — confirm the exact component class name against the installed AAO
      package before finalizing this task (research.md §3 open verification item)
- [X] T029 Implement `ConversionPipeline` in `Editor/Pipeline/ConversionPipeline.cs`: orchestrate
      T013–T028 in order (AAO presence check → existing-output check → duplicate avatar → resolve
      assets → convert materials/textures → replace renderer materials → add AAO component),
      populating `ConversionContext` throughout and never writing to any PC-namespace asset
      (Constitution Principle I)

**Checkpoint verified (2026-09-23)**: driven directly via a temporary `-executeMethod` tool
(built a fixture PC avatar — Standard-shader Material with 5 textures across 4 target
classifications, one oversized at 2048×2048, two sharing the Mask classification to exercise
merging — then called `ConversionPipeline.Run` and inspected the result), confirming: Quest
Prefab created at the correct path and position offset; AAO `Trace And Optimize` present; the
Material converted to `VRChat/Mobile/Toon Standard` with GPU Instancing on; all 4
`TextureMap` entries present with the oversized Color texture correctly downscaled to
1024×1024, the two Mask-classification sources correctly merged into one 1024×512 atlas
(preserving both sources' aspect ratios), and 15 unmapped-but-used source properties (`_Color`,
`_Metallic`, etc.) each correctly warned rather than silently dropped. Fixture and generated
output were deleted afterward; the verification tool itself was deleted (not shipped). All 33
EditMode tests pass (24 carried over from T005-T012 + 9 new: 4 ShaderPropertyMapperTests, 3
TextureAtlasPlacementTests, 6 TextureResizerTests — some tests cover multiple cases each).

Implementation note (T013/T029): `ConversionPipeline`'s actual stage order runs AssetResolver
(T013) BEFORE AvatarDuplicator (T014) — the reverse of this phase's "duplicate avatar → resolve
assets" summary phrase — since AvatarDuplicator needs the PC avatar's already-resolved Renderer
list to build the matching Quest-side `RendererRef`s. Also, FR-018's four independent step
toggles are only partially wired: `ConversionSettings` (per data-model.md, T005) has no
"Duplicate Avatar" field alongside `MergeTexturesEnabled`/`ResizeTexturesEnabled`/
`AddAaoComponentEnabled` despite T037 naming it as a fourth toggle — a pre-existing spec
inconsistency between data-model.md and T037, left for whoever implements T037 (Phase 4) to
resolve; duplication always runs in the current pipeline.

---

## Phase 3: User Story 1 - Generate a working Quest derivative from a PC avatar (Priority: P1) 🎯 MVP

**Goal**: A user can select a PC avatar and click one "Generate" action to get a working,
placed Quest-compatible derivative, with the PC avatar left untouched.

**Independent Test**: Point the tool at a PC avatar prefab with at least one
SkinnedMeshRenderer/Material/Texture, click Generate, and confirm a new Quest avatar exists, uses
Quest-appropriate Materials, and the original PC assets are byte-for-byte unchanged.

### Implementation for User Story 1

- [X] T030 [US1] Create the `QuestAvatarConverterWindow` `EditorWindow` with a Source Avatar object
      field and a "Generate" button in `Editor/QuestAvatarConverterWindow.cs`
- [X] T031 [US1] Wire the Generate button to `ConversionPipeline` (T029) using default
      `ConversionSettings` (target = `VRChat/Mobile/Toon Standard` per research.md §4's default
      suggestion, `PlacementOffset` = (2, 0, 0) per the source document's example, `MaxTextureSize` =
      1024, all optional steps enabled) in `Editor/QuestAvatarConverterWindow.cs`
- [X] T032 [US1] Surface `AAOIntegrator`'s absent-AAO halt message (FR-013) and
      `ExistingOutputDetector`'s overwrite confirmation (FR-022) as blocking `EditorUtility` dialogs
      from the Generate button handler in `Editor/QuestAvatarConverterWindow.cs`
- [X] T033 [US1] Manual validation: run quickstart.md Scenarios 1, 2, 3, 4, 7, and 8 against a real
      test avatar in a project with AAO installed (and Scenario 4 with AAO temporarily removed)

**T033 note (2026-09-23) — partial, headless-only validation; real interactive validation still
outstanding.** This dev environment has no interactive Unity Editor session (CLI/batch-mode only),
so the GUI itself was never actually clicked — only code-reviewed. What WAS verified, headlessly,
by driving `ConversionPipeline`/the window's own helper methods directly from a temporary
`-executeMethod` tool (same pattern as the Phase 2 checkpoint, deleted after use):
- Scenarios 1-3 (core generation, texture merge, resize): already covered by the Phase 2 checkpoint
  verification above.
- Scenario 8 (unmapped shader): a Material on `Unlit/Color` (no registered rule) correctly produced
  zero `MaterialMap` entries, an explicit FR-006 error log entry, and a null (never the original PC
  Material) Renderer slot on the Quest side.
- Scenario 7 (re-run/overwrite): running Generate twice reused the exact same deterministic output
  path (no versioned duplicate); declining the overwrite confirmation left the prior output
  untouched; accepting it replaced it in place.

**Scenario 4 (AAO absent) could not be exercised even headlessly**, and this is structural, not an
oversight: `AAOIntegrator.cs`/`DomainTypes.cs` reference AAO's `TraceAndOptimize` type at compile
time (this package's own `package.json` hard-depends on it, T004), so removing AAO from this project
would fail this package's own compilation, not exercise a graceful runtime halt. Verified by code
review instead: `AAOIntegrator.IsAaoInstalled()` reflects over loaded assemblies rather than a
direct compiled reference, so it degrades correctly in a project where this package was distributed
precompiled without AAO present (see that file's remarks) — but no environment was available here to
actually prove that path executes.

**Scenarios 5 and 6 are out of scope for T033** — they exercise the Preview/Report panels, which are
Phase 5 (US3, T045/T044) and don't exist yet.

**Still needed before this checkpoint can be called fully done**: an actual interactive Unity Editor
session, opening the `QuestAvatarConverterWindow` and clicking through Scenarios 1-4/7/8 by hand
against a real VRChat test avatar, per quickstart.md's Prerequisites. The residual risk this leaves
unverified is narrow (EditorGUILayout rendering, dialog button wiring, and the `ObjectField`/menu
item registration itself) since the Generate handler delegates directly to the same
ConversionPipeline/AAOIntegrator/ExistingOutputDetector code paths already verified above — but it
is not verified.

**Checkpoint**: User Story 1 is functionally complete and headlessly verified; not yet demoed
interactively (see the T033 note above) — full MVP sign-off is pending that.

---

## Phase 4: User Story 2 - Configure conversion settings before generating (Priority: P2)

**Goal**: The user can change target shader, placement offset, max texture size, and which
optional steps run, and see the generated output reflect each change.

**Independent Test**: Change each setting one at a time and confirm the generated output reflects
it, without re-validating all of US1's correctness in detail.

### Implementation for User Story 2

- [X] T034 [P] [US2] Add a target-shader dropdown to `QuestAvatarConverterWindow`, populated from
      all loaded `ShaderConversionRuleSet` assets (T009), wired to
      `ConversionSettings.TargetShaderRule` (FR-005) in `Editor/QuestAvatarConverterWindow.cs`
- [X] T035 [P] [US2] Add Placement Offset X/Y/Z fields wired to
      `ConversionSettings.PlacementOffset` → `AvatarDuplicator` (FR-004) in
      `Editor/QuestAvatarConverterWindow.cs`
- [X] T036 [P] [US2] Add a Max Texture Size field wired to `ConversionSettings.MaxTextureSize` →
      `TextureResizer` (FR-009) in `Editor/QuestAvatarConverterWindow.cs`
- [X] T037 [US2] Add independent toggles for Merge Textures / Resize Textures / Add AAO Component
      (FR-018) in `Editor/QuestAvatarConverterWindow.cs` — **"Duplicate Avatar" deliberately
      excluded**: confirmed with the project owner (2026-09-23) not to invent semantics for it
      (data-model.md's `ConversionSettings` has no field for it, and disabling it has no safe,
      unambiguous meaning under Constitution I — see this decision recorded in
      `QuestAvatarConverterWindow.cs`'s class remarks and the Phase 2/T029 note above).
      `ConversionPipeline`'s stage selection already threaded the other three through at T029.
- [X] T038 [US2] Manual validation: run quickstart.md Scenario 1 with each setting changed one at a
      time (different target shader, custom offset, custom max size, Merge Textures disabled) —
      verified headlessly the same way as T033 (temporary `-executeMethod` tool driving
      `ConversionPipeline` directly with each setting varied one at a time, deleted after use); all
      four reflected correctly in the output (target shader on the generated Material, avatar
      position, downscaled texture size capped at the custom max, and Merge Textures disabled
      producing zero merged/`Layout`-bearing textures). Same interactive-GUI caveat as T033 applies
      — the window's own controls were not clicked by hand.

**Checkpoint**: User Stories 1 and 2 both work independently; changing any exposed setting visibly
changes the generated output.

---

## Phase 5: User Story 3 - Review results before and after generating (Priority: P3)

**Goal**: The user can preview a Material's merge/resize result before generating, and see a
post-generation report of performance metrics and individually flagged Quest-incompatible objects
(including PhysBone-specific findings), without any automatic destructive action being taken.

**Independent Test**: Open the preview for one Material with no Generate click and confirm no
asset is written to disk; separately, run Generate and confirm the report shows metrics and
individually flagged objects.

### Implementation for User Story 3

- [X] T039 [P] [US3] Implement `PerformanceAnalyzer` in `Editor/Pipeline/PerformanceAnalyzer.cs`:
      compute triangle count, Material count, SkinnedMeshRenderer count, bone count, Texture count,
      and estimated Texture memory for the generated Quest avatar (FR-015). Delegates the actual
      measurement to VRChat SDK's own official `AvatarPerformance.CalculatePerformanceStats` (same
      calculator the SDK Control Panel uses) rather than re-deriving VRChat-specific counting rules
      by hand — verified its real signatures via reflection first (see T041 note), since neither is
      documented in source under `Packages/com.vrchat.base` (both ship precompiled).
- [X] T040 [P] [US3] Implement the `QuestCompatibilityChecker` detection pass in
      `Editor/Pipeline/QuestCompatibilityChecker.cs`: scan the Quest avatar against
      `QuestCompatibilityRules.FlaggedComponents` (T010/T012) and populate one `CompatibilityFinding`
      per flagged object — never aggregate-only (FR-016). Component-type resolution shares
      `Editor/Pipeline/ComponentTypeResolver.cs`, extracted from `QuestCompatibilityRulesLoader`
      (T010) so both use identical resolution semantics.
- [X] T041 [P] [US3] Implement `PhysBoneValidator` in `Editor/Pipeline/PhysBoneValidator.cs`:
      count PhysBone components / PhysBone Colliders / PhysBone-affected transforms and compare
      against `QuestCompatibilityRules.PhysBoneLimits` (research.md §5), reporting the resulting
      Quest Performance Rank tier AND surfacing the hard 256-affected-transform-per-component cap as
      a distinct, higher-severity finding from an ordinary rank downgrade (FR-016a). The four
      avatar-wide aggregate counts reuse VRChat SDK's own `AvatarPerformanceStats.physBone`
      (component/transform/collider/collisionCheck counts — confirmed via a one-off reflection dump
      against the precompiled `VRCSDKBase`/`VRC.SDK3.Dynamics.PhysBone` assemblies, since neither
      type ships as readable source; the dump tool was deleted after confirming the exact
      constructor/field signatures). The one count that calculator does NOT provide — a
      *per-component* max-affected-transforms breakdown, needed for the hard cap, which applies per
      component not per avatar — is approximated by walking each `VRCPhysBone`'s own
      `rootTransform` hierarchy (excluding `ignoreTransforms` subtrees); documented as an
      approximation (doesn't replicate `multiChildType` branching) in the file's remarks.
- [X] T042 [P] [Tests] EditMode tests for `PhysBoneValidator` threshold evaluation (each rank tier
      boundary from research.md §5's table, plus the 256-transform hard-cap case) in
      `Editor.Tests/PhysBoneValidatorTests.cs`. `EvaluateRank`/`ComputeExceedsHardCap` are
      `internal` specifically so these tests can call them directly against hand-built
      `PhysBoneMetrics`/`PhysBoneThresholds` fixtures, without needing real `VRCPhysBone` components
      or a live `AvatarPerformance` call (Constitution VI).
- [X] T043 [US3] Implement `ConversionReport` in `Editor/Pipeline/ConversionReport.cs`: aggregate
      `ConversionContext.Log`, `PerformanceMetrics`, and `CompatibilityFindings` into a single
      displayable report (FR-019). `ConversionReport.Analyze` is the actual orchestration point —
      runs PerformanceAnalyzer/QuestCompatibilityChecker/PhysBoneValidator against the generated
      Quest avatar and writes their results back into `ConversionContext`'s own canonical fields, so
      that data-model.md's ConversionContext.CompatibilityFindings stays the single source of truth
      the Report panel (T044) reads from. A hard-cap violation from PhysBoneValidator is folded in
      as its own `CompatibilityFinding` (`BlockingIfUnaddressed` severity) rather than staying a
      separate report-only number, so FR-016a's "distinct, higher-severity finding" goes through the
      same individually-listed, per-item Remove/Keep review as any other flagged object (FR-021).
- [X] T044 [US3] Add a Report panel to `QuestAvatarConverterWindow` displaying `PerformanceMetrics`
      and each `CompatibilityFinding` individually with per-item Remove/Keep controls — selecting
      neither MUST leave the object untouched (`UserDecision` defaults to `Undecided`, FR-021
      forbids any auto-remove) in `Editor/QuestAvatarConverterWindow.cs`. Remove/Keep are two-step:
      clicking a finding's Remove/Keep button only sets its `UserDecision`; nothing is actually
      destroyed until a separate "Apply Decisions" button runs, which acts *only* on
      `Remove`-decided items (never on `Undecided`/`Keep`) — an explicit, reviewable batch action
      rather than instant deletion on click.
- [X] T045 [US3] Add a Preview panel to `QuestAvatarConverterWindow` that runs
      `TextureAtlasGenerator`/`TextureResizer`/`TextureTypeClassifier` in a dry-run mode for one
      selected Material and displays source textures, merged result, and final resolution without
      calling `TextureAssetWriter` or writing anything to disk (FR-017) in
      `Editor/QuestAvatarConverterWindow.cs`. Shows the actual composited/resized merge result (not
      just its dimensions) via a new `TextureAssetWriter.CompositeAndResize` — the in-memory-only
      half of `Write` (compositing + resizing), extracted so the disk-writing PNG-encode/
      AssetDatabase-import half stays exclusive to the real generation path; the preview's
      in-memory `Texture2D` is destroyed on the next preview run and on window close (`OnDisable`).
- [X] T046 [US3] Manual validation: run quickstart.md Scenarios 5 and 6 — verified headlessly (same
      pattern/caveat as T033/T038, temporary `-executeMethod` tool deleted after use): Scenario 5
      confirmed a 2-source Mask-classification merge preview composites and resizes correctly while
      the project's Texture2D asset count stays unchanged (zero disk writes); Scenario 6 confirmed a
      fixture `AudioSource` was listed as its own `CompatibilityFinding` (defaulting to
      `Undecided`, still present on the generated prefab afterward — no auto-removal) alongside a
      3-Transform PhysBone chain correctly counted and ranked (`Good`, per research.md §5's table).
      Same interactive-GUI caveat as T033/T038 applies.

**Checkpoint**: All three user stories are functionally complete and headlessly verified; the full
spec's Acceptance Scenarios are satisfied except for actually being clicked through in a live,
interactive Unity Editor session (see the T033 note — still outstanding across US1/US2/US3 alike).

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T047 [P] Finalize `package.json` / VPM listing metadata (display name, description, Unity
      2022.3.22f1 minimum, VRChat SDK3 - Avatars 3.10.4 and AAO 1.9.19 dependency ranges) at
      `Packages/com.vrc-rufu.quest-avatar-converter/package.json`. Added `unityRelease: "22f1"`
      (the `unity` field alone only carries major.minor per UPM convention), a fuller description,
      and `keywords`. **Did not** add `com.vrchat.base`/`nadena.dev.ndmf` as `dependencies` entries
      despite the task text's "and AAO 1.9.19 dependency ranges" framing suggesting a fuller list —
      research.md §2b already explains why: they're git-URL/embedded-local project-level
      dependencies in `manifest.json`, not standard-UPM-resolvable, so declaring them in this
      package's own `dependencies` risks a resolution error rather than fixing anything (verified:
      adding them and re-running `unity test` reproduced no error either way in THIS project, since
      they're already present — but the risk is for a fresh project without them pre-resolved,
      which research.md's original reasoning was written for). Kept only
      `com.vrchat.avatars`/`com.anatawa12.avatar-optimizer`/`com.unity.test-framework`, matching T004.
- [X] T048 [P] Confirm AAO's actual `Trace And Optimize` C# class name against the installed AAO
      DLL/source and correct `AAOIntegrator.cs` (T028) if it differs from the assumed name
      (research.md §3). Already correct — `AddComponent<Anatawa12.AvatarOptimizer.TraceAndOptimize>()`
      succeeded across every Phase 2/3/4/5 smoke-test run this session (AAO component confirmed
      present on the generated Quest avatar each time); no correction needed.
- [X] T049 [P] Reconfirmed VRChat SDK3 - Avatars (research.md §2) and AAO (research.md §3) against
      their live sources on 2026-09-23 (4 days after research.md's original 2026-09-19 check): both
      still current — VRChat SDK **3.10.5** remains the latest non-beta release
      (github.com/vrchat/packages/releases), AAO **1.9.19** remains the latest non-prerelease
      release (github.com/anatawa12/AvatarOptimizer/releases). No version bump needed for
      T004/T009. **Not fully done**: the `VRChat/Mobile/Toon Standard` "flagship default shader"
      framing (research.md §4, already flagged there as medium-confidence) was NOT reconfirmed — the
      creators.vrchat.com shaders page returned 404 during this check, and no live VCC project was
      available in this environment to verify against directly. Low practical risk either way since
      both `Toon Lit` and `Toon Standard` are registered as selectable targets (T009), not just the
      default — but this specific sub-item is carried forward, not closed.
- [ ] T050 Run the complete quickstart.md validation pass (all 8 scenarios) end-to-end in a clean
      test project before considering the feature release-ready — **NOT DONE as literally
      specified, and knowingly left incomplete.** What this session actually did, cumulatively, is
      run headless equivalents of Scenarios 1/2/3 (T033's Phase 2 checkpoint), 7/8 (T033), the
      US2 setting-variation equivalent of Scenario 1 (T038), and 5/6 (T046) — all in *this* dev/test
      project via temporary `-executeMethod` tools driving `ConversionPipeline`/`ConversionReport`
      directly, never through the actual `QuestAvatarConverterWindow` GUI, and never in a separate
      clean project. Scenario 4 (AAO absent) could not be run at all — this package compiles
      against AAO directly (T004), so removing AAO from any project containing this package breaks
      the package's own compilation rather than exercising a graceful runtime halt (see the T033
      note's fuller explanation). **Before this feature can be considered release-ready**, a human
      needs to: open a real Unity Editor session, install this package into a project with a real
      VRChat avatar per quickstart.md's Prerequisites, and click through all 8 scenarios by hand —
      including Scenario 4 in a *second* project/copy with AAO actually absent. This is the single
      largest remaining gap in this implementation.
- [X] T051 [P] Updated the root `README.md` (tool usage section now describes the actual GUI
      workflow — menu location, Source Avatar field, Generate, Report/Preview panels — instead of
      "no GUI yet"; status section reflects 46/51 tasks done; added a pointer to the known
      interactive-validation gap from T050's note) and `AI_speckit-main/README.md` (the one stale
      status line, "constitution.md not yet filled in," corrected — it was filled in during Phase
      1/T001-era work, this line just hadn't been updated since).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup. BLOCKS all user stories — none of US1/US2/US3 can
  begin until the full conversion engine (T005–T029) compiles and its tests (T008, T011, T017,
  T022, T024) pass.
- **User Story 1 (Phase 3)**: Depends on Foundational only.
- **User Story 2 (Phase 4)**: Depends on Foundational + US1's `QuestAvatarConverterWindow` skeleton
  (T030) existing, since it adds controls to the same window — but is independently testable once
  added.
- **User Story 3 (Phase 5)**: Depends on Foundational + US1's window skeleton (T030); T039–T043
  (analyzer/checker/report logic) have no dependency on US2 and could be built in parallel with
  Phase 4 by a second contributor.
- **Polish (Phase 6)**: Depends on all desired user stories being complete.

### Parallel Opportunities

- T002–T004 (Setup) run in parallel.
- Within Foundational: T005–T012 (data types + rule schemas + default assets + their tests) are
  mutually independent and run in parallel; T013–T029 (the actual pipeline stages) have real
  sequential dependencies on the T005–T012 types and on each other per the pipeline order, but
  T016/T017, T019, T020, T022, T023/T024, T025 are parallel with each other where marked `[P]`.
- Within US2: T034, T035, T036 are parallel (different UI sections, same file but non-overlapping
  regions — coordinate merge order).
- Within US3: T039, T040, T041 (+ T042 tests) are parallel; T044 and T045 both touch
  `QuestAvatarConverterWindow.cs` so should not be run fully in parallel without coordinating the
  merge.
- T048, T049, T051 (Polish) are parallel.

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 (Setup) → Phase 2 (Foundational — the real engineering effort) → Phase 3 (US1).
2. **STOP and VALIDATE** with quickstart.md Scenarios 1–4, 7, 8.
3. This is a usable MVP: generate a Quest avatar with sensible defaults, non-destructively.

### Incremental Delivery

1. Setup + Foundational → engine ready, no UI.
2. + US1 → MVP: one-click Generate with defaults. Validate → demo.
3. + US2 → configurable Generate. Validate → demo.
4. + US3 → preview + report + compatibility review. Validate → demo.
5. Polish → confirm pinned versions/class names, finalize package metadata, full quickstart pass.
