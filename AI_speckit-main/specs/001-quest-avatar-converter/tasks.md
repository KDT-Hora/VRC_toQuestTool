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
git URL in `Packages/manifest.json`) ships internal `csc.rsp`/`.ruleset` files as **symlinks**. If
Windows Developer Mode is off or `core.symlinks` is `false` (global or repo-local), git checks
these out as plain text files containing the link target instead of real symlinks, which Unity
then fails to parse (`CS2001`/`CS8035` in compile errors). Fix: enable Windows Developer Mode,
`git config --global core.symlinks true` (and repo-local, if overridden), delete the affected
package's folder under `UnityProject/Library/PackageCache/`, and let Unity re-resolve it.

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

- [ ] T005 [P] Define `ConversionContext`, `ConversionSettings`, `ConversionLogEntry` in
      `Editor/Pipeline/ConversionContext.cs` exactly per data-model.md's "Core Run-Scoped Entities"
      table — `MaterialMap` MUST be keyed by `PCMaterial` (one entry per distinct source Material,
      enforcing FR-011 sharing by construction) and `TextureMap` MUST be keyed by
      `(PCMaterial, TextureClassification)` (enforcing FR-007/FR-008 per-Material-per-type scoping)
- [ ] T006 [P] Define the Avatar/Renderer/Material/Texture domain types (`PCAvatar`, `QuestAvatar`,
      `RendererRef`, `PCMaterial`, `QuestMaterial`, `PCTexture`, `QuestTexture`, `MaterialProperty`,
      `TextureClassification` enum, `AtlasLayout`, `AtlasPlacement`) in
      `Editor/Pipeline/DomainTypes.cs` per data-model.md's "Avatar & Renderer Entities" / "Material &
      Shader Entities" / "Texture Entities" tables
- [ ] T007 [P] Implement the `ShaderConversionRuleSet`/`PropertyMapping` ScriptableObject schema in
      `Editor/Materials/IShaderConversionRule.cs` enforcing contracts/extension-data-contracts.md §1
      invariants verbatim: `TargetShader` MUST be one of VRChat's own `VRChat/Mobile/*` shaders (load
      fails otherwise, research.md §4); `SourceShader`+`TargetShader` pairs MUST be unique across
      loaded assets (load-time error, not silent pick-one); unresolvable `SourcePropertyName`
      entries MUST be reported as a load-time warning
- [ ] T008 [P] [Tests] EditMode tests for the T007 loader invariants (duplicate pair rejected,
      non-`VRChat/Mobile/*` target rejected, unresolved source property warned) in
      `Editor.Tests/ShaderConversionRuleLoaderTests.cs`
- [ ] T009 [P] Author default `ShaderConversionRuleSet` assets targeting `VRChat/Mobile/Toon Lit`
      and `VRChat/Mobile/Toon Standard` (research.md §4) under `Data/ShaderConversionRules/`
- [ ] T010 [P] Implement the `QuestCompatibilityRules` ScriptableObject schema
      (`FlaggedComponentRule[]`, `PhysBoneThresholds` with a REQUIRED non-empty `SourceCitation`) in
      `Editor/Pipeline/QuestCompatibilityRules.cs` enforcing contracts §2 invariants verbatim
- [ ] T011 [P] [Tests] EditMode tests for the T010 loader invariants (unresolved
      `ComponentTypeName` warned, missing `SourceCitation` treated as a rule-authoring error) in
      `Editor.Tests/QuestCompatibilityRulesLoaderTests.cs`
- [ ] T012 [P] Author the default `QuestCompatibilityRules` asset with VRChat's published Quest
      PhysBone thresholds (research.md §5 table: 0/4/6/8 components, 0/16/32/64 affected transforms,
      0/4/8/16 colliders, 0/16/32/64 collision checks at Excellent/Good/Medium/Poor) plus the hard
      256-affected-transform-per-component cap, citing the source page, under
      `Data/QuestCompatibilityRules/`
- [ ] T013 Implement `AssetResolver` in `Editor/Pipeline/AssetResolver.cs`: traverse only each
      Renderer's static `sharedMaterial`(s) → Material → Texture at conversion time (FR-003);
      Animator/Animation-Clip-driven material/texture swaps MUST NOT be discovered (out of scope
      per spec Assumptions); the VRC Avatar Descriptor reference IS followed
- [ ] T014 Implement `AvatarDuplicator` in `Editor/Pipeline/AvatarDuplicator.cs`: produce a fully
      independent Prefab copy, never a Prefab Variant (FR-023), placed at the PC avatar's position
      plus `ConversionSettings.PlacementOffset` (FR-004), under
      `Assets/<QuestConvertedRoot>/<AvatarName>/Avatar/` (FR-002)
- [ ] T015 Implement `ExistingOutputDetector` in `Editor/Pipeline/ExistingOutputDetector.cs`:
      detect prior Quest output at the target path and require an explicit user-approved
      confirmation before any overwrite occurs (FR-022) — no silent overwrite path may exist
- [ ] T016 Implement `ShaderPropertyMapper` as pure, Editor-independent resolution logic (takes a
      `ShaderConversionRuleSet` + source Material property values, returns target property values)
      in `Editor/Materials/ShaderPropertyMapper.cs` (Constitution Principle VI)
- [ ] T017 [P] [Tests] EditMode tests for `ShaderPropertyMapper` resolution logic (mapped
      properties translate correctly; an unmapped-but-used source Property produces a warning per
      the spec's Edge Cases, not a silent drop) in `Editor.Tests/ShaderPropertyMapperTests.cs`
- [ ] T018 Implement `MaterialConverter` in `Editor/Materials/MaterialConverter.cs` (FR-006):
      look up the `PCMaterial.Shader` in loaded `ShaderConversionRuleSet`s; if none matches, record
      that Material as an explicit conversion failure (never guess, never silently skip)
- [ ] T019 [P] Implement `GpuInstancingApplier` in `Editor/Materials/GpuInstancingApplier.cs`:
      enable GPU Instancing on every generated `QuestMaterial` by default via the mechanism the
      target shader requires (FR-020a)
- [ ] T020 [P] Implement `TextureTypeClassifier` in `Editor/Textures/TextureTypeClassifier.cs`:
      classify each Material texture property into Color/Normal/Mask/Emission (FR-008), driven by
      `PropertyMapping.TargetClassification`
- [ ] T021 Implement `TextureAtlasGenerator` as pure placement-math logic (inputs: source texture
      dimensions/aspect ratios for one `(Material, TextureClassification)` group; output:
      `AtlasLayout` with aspect-ratio-preserving `AtlasPlacement` rects) in
      `Editor/Textures/TextureAtlasGenerator.cs` (Constitution Principle VI) — MUST NOT combine
      textures of different `TextureClassification`s into one layout (FR-008)
- [ ] T022 [P] [Tests] EditMode tests for `TextureAtlasGenerator` placement math (aspect ratio
      preserved for mismatched source sizes, e.g. 2048×2048 + 1024×2048 inputs; single-texture case
      is a no-op layout) in `Editor.Tests/TextureAtlasPlacementTests.cs`
- [ ] T023 Implement `TextureResizer` as pure resize-math logic (input: width/height + configured
      max size; output: target width/height) in `Editor/Textures/TextureResizer.cs` — longest edge
      MUST NOT exceed the configured max (default 1024, FR-009), aspect ratio MUST be preserved, and
      a texture already at or below the max MUST NOT be upscaled
- [ ] T024 [P] [Tests] EditMode tests for `TextureResizer` (oversized/undersized/already-at-max
      inputs against default 1024 and a custom max) in `Editor.Tests/TextureResizerTests.cs`
- [ ] T025 [P] Implement `TextureUvTilingDetector` in `Editor/Textures/TextureUvTilingDetector.cs`:
      detect non-default UV Scale/Offset on a Material being merged and append a
      `ConversionLogEntry` warning (Edge Case) — MUST NOT attempt UV remapping (out of scope for v1)
- [ ] T026 Implement `TextureAssetWriter` in `Editor/Textures/TextureAssetWriter.cs`: render
      `TextureAtlasGenerator`/`TextureResizer` output to an actual `Texture2D`, always encoded as
      lossless PNG, preserving an alpha channel whenever any source Texture used one (FR-009),
      written under `Assets/<QuestConvertedRoot>/<AvatarName>/Textures/`
- [ ] T027 Implement `RendererMaterialReplacer` in `Editor/Pipeline/RendererMaterialReplacer.cs`:
      point each Quest-side Renderer at its `QuestMaterial` via `ConversionContext.MaterialMap`
      (FR-010), so Renderers sharing a source `PCMaterial` end up sharing one `QuestMaterial`
      (FR-011)
- [ ] T028 Implement `AAOIntegrator` in `Editor/Pipeline/AAOIntegrator.cs`: detect whether AAO is
      installed and halt generation with a clear, actionable message before any output is produced
      if absent (FR-013); otherwise add the AAO `Trace And Optimize` Avatar Global Component to the
      Quest avatar root (FR-012) — confirm the exact component class name against the installed AAO
      package before finalizing this task (research.md §3 open verification item)
- [ ] T029 Implement `ConversionPipeline` in `Editor/Pipeline/ConversionPipeline.cs`: orchestrate
      T013–T028 in order (AAO presence check → existing-output check → duplicate avatar → resolve
      assets → convert materials/textures → replace renderer materials → add AAO component),
      populating `ConversionContext` throughout and never writing to any PC-namespace asset
      (Constitution Principle I)

**Checkpoint**: The full conversion engine runs correctly end-to-end when driven directly (e.g.
from a temporary test menu item), producing a correct Quest avatar from default settings — no
end-user-facing UI exists yet. All Phase 2 EditMode tests pass.

---

## Phase 3: User Story 1 - Generate a working Quest derivative from a PC avatar (Priority: P1) 🎯 MVP

**Goal**: A user can select a PC avatar and click one "Generate" action to get a working,
placed Quest-compatible derivative, with the PC avatar left untouched.

**Independent Test**: Point the tool at a PC avatar prefab with at least one
SkinnedMeshRenderer/Material/Texture, click Generate, and confirm a new Quest avatar exists, uses
Quest-appropriate Materials, and the original PC assets are byte-for-byte unchanged.

### Implementation for User Story 1

- [ ] T030 [US1] Create the `QuestAvatarConverterWindow` `EditorWindow` with a Source Avatar object
      field and a "Generate" button in `Editor/QuestAvatarConverterWindow.cs`
- [ ] T031 [US1] Wire the Generate button to `ConversionPipeline` (T029) using default
      `ConversionSettings` (target = `VRChat/Mobile/Toon Standard` per research.md §4's default
      suggestion, `PlacementOffset` = (2, 0, 0) per the source document's example, `MaxTextureSize` =
      1024, all optional steps enabled) in `Editor/QuestAvatarConverterWindow.cs`
- [ ] T032 [US1] Surface `AAOIntegrator`'s absent-AAO halt message (FR-013) and
      `ExistingOutputDetector`'s overwrite confirmation (FR-022) as blocking `EditorUtility` dialogs
      from the Generate button handler in `Editor/QuestAvatarConverterWindow.cs`
- [ ] T033 [US1] Manual validation: run quickstart.md Scenarios 1, 2, 3, 4, 7, and 8 against a real
      test avatar in a project with AAO installed (and Scenario 4 with AAO temporarily removed)

**Checkpoint**: User Story 1 is fully functional and independently testable/demoable (MVP).

---

## Phase 4: User Story 2 - Configure conversion settings before generating (Priority: P2)

**Goal**: The user can change target shader, placement offset, max texture size, and which
optional steps run, and see the generated output reflect each change.

**Independent Test**: Change each setting one at a time and confirm the generated output reflects
it, without re-validating all of US1's correctness in detail.

### Implementation for User Story 2

- [ ] T034 [P] [US2] Add a target-shader dropdown to `QuestAvatarConverterWindow`, populated from
      all loaded `ShaderConversionRuleSet` assets (T009), wired to
      `ConversionSettings.TargetShaderRule` (FR-005) in `Editor/QuestAvatarConverterWindow.cs`
- [ ] T035 [P] [US2] Add Placement Offset X/Y/Z fields wired to
      `ConversionSettings.PlacementOffset` → `AvatarDuplicator` (FR-004) in
      `Editor/QuestAvatarConverterWindow.cs`
- [ ] T036 [P] [US2] Add a Max Texture Size field wired to `ConversionSettings.MaxTextureSize` →
      `TextureResizer` (FR-009) in `Editor/QuestAvatarConverterWindow.cs`
- [ ] T037 [US2] Add independent toggles for Duplicate Avatar / Merge Textures / Resize Textures /
      Add AAO Component (FR-018), threading each through `ConversionPipeline`'s stage selection in
      `Editor/QuestAvatarConverterWindow.cs` and `Editor/Pipeline/ConversionPipeline.cs`
- [ ] T038 [US2] Manual validation: run quickstart.md Scenario 1 with each setting changed one at a
      time (different target shader, custom offset, custom max size, Merge Textures disabled)

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

- [ ] T039 [P] [US3] Implement `PerformanceAnalyzer` in `Editor/Pipeline/PerformanceAnalyzer.cs`:
      compute triangle count, Material count, SkinnedMeshRenderer count, bone count, Texture count,
      and estimated Texture memory for the generated Quest avatar (FR-015)
- [ ] T040 [P] [US3] Implement the `QuestCompatibilityChecker` detection pass in
      `Editor/Pipeline/QuestCompatibilityChecker.cs`: scan the Quest avatar against
      `QuestCompatibilityRules.FlaggedComponents` (T010/T012) and populate one `CompatibilityFinding`
      per flagged object — never aggregate-only (FR-016)
- [ ] T041 [P] [US3] Implement `PhysBoneValidator` in `Editor/Pipeline/PhysBoneValidator.cs`:
      count PhysBone components / PhysBone Colliders / PhysBone-affected transforms and compare
      against `QuestCompatibilityRules.PhysBoneLimits` (research.md §5), reporting the resulting
      Quest Performance Rank tier AND surfacing the hard 256-affected-transform-per-component cap as
      a distinct, higher-severity finding from an ordinary rank downgrade (FR-016a)
- [ ] T042 [P] [Tests] EditMode tests for `PhysBoneValidator` threshold evaluation (each rank tier
      boundary from research.md §5's table, plus the 256-transform hard-cap case) in
      `Editor.Tests/PhysBoneValidatorTests.cs`
- [ ] T043 [US3] Implement `ConversionReport` in `Editor/Pipeline/ConversionReport.cs`: aggregate
      `ConversionContext.Log`, `PerformanceMetrics`, and `CompatibilityFindings` into a single
      displayable report (FR-019)
- [ ] T044 [US3] Add a Report panel to `QuestAvatarConverterWindow` displaying `PerformanceMetrics`
      and each `CompatibilityFinding` individually with per-item Remove/Keep controls — selecting
      neither MUST leave the object untouched (`UserDecision` defaults to `Undecided`, FR-021
      forbids any auto-remove) in `Editor/QuestAvatarConverterWindow.cs`
- [ ] T045 [US3] Add a Preview panel to `QuestAvatarConverterWindow` that runs
      `TextureAtlasGenerator`/`TextureResizer`/`TextureTypeClassifier` in a dry-run mode for one
      selected Material and displays source textures, merged result, and final resolution without
      calling `TextureAssetWriter` or writing anything to disk (FR-017) in
      `Editor/QuestAvatarConverterWindow.cs`
- [ ] T046 [US3] Manual validation: run quickstart.md Scenarios 5 and 6

**Checkpoint**: All three user stories are independently functional; the full spec's Acceptance
Scenarios are satisfied.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T047 [P] Finalize `package.json` / VPM listing metadata (display name, description, Unity
      2022.3.22f1 minimum, VRChat SDK3 - Avatars 3.10.4 and AAO 1.9.19 dependency ranges) at
      `Packages/com.vrc-rufu.quest-avatar-converter/package.json`
- [ ] T048 [P] Confirm AAO's actual `Trace And Optimize` C# class name against the installed AAO
      DLL/source and correct `AAOIntegrator.cs` (T028) if it differs from the assumed name
      (research.md §3)
- [ ] T049 [P] Reconfirm the exact VRChat SDK3 - Avatars version (research.md §2) and the
      `VRChat/Mobile/Toon Standard` default-target-shader choice (research.md §4) against a live VCC
      project, adjusting T004/T009/T031 if either has changed
- [ ] T050 Run the complete quickstart.md validation pass (all 8 scenarios) end-to-end in a clean
      test project before considering the feature release-ready
- [ ] T051 [P] Update the root `README.md` and `AI_speckit-main/README.md` status sections to
      reflect implementation completion

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
