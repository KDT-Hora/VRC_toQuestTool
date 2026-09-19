# Quickstart: Validating the Quest Avatar Converter

This is a manual validation guide proving the feature works end-to-end against spec.md's
Acceptance Scenarios. It does not contain implementation code — see tasks.md for build steps and
data-model.md/contracts/ for the underlying schemas.

## Prerequisites

- A Unity project (pinned Editor version: see research.md) with VRChat SDK3 - Avatars and Avatar
  Optimizer (AAO) installed via VRChat Creator Companion.
- The Quest Avatar Converter package installed into that project (see plan.md Project Structure).
- A test PC avatar Prefab in the project with:
  - At least one `SkinnedMeshRenderer` whose Material references at least one Texture.
  - At least one Material referencing **more than one** source Texture (to validate merging).
  - At least one Texture whose longest edge exceeds 1024px (to validate resizing).
  - At least one PhysBone component (to validate FR-016a).
  - At least one component from the "flagged" set, e.g. an `AudioSource` or `Camera` (to validate
    FR-016/FR-021).

## Scenario 1 — Core generation (User Story 1, P1)

1. Open the Quest Avatar Converter window and select the test PC avatar.
2. Leave all settings at default; click **Generate**.
3. **Expect**: a new Prefab appears under `Assets/<QuestConvertedRoot>/<AvatarName>/Avatar/`; its
   Renderer(s) reference newly generated Materials under `.../Materials/`; the AAO
   `Trace And Optimize` component is present on the Quest avatar root.
4. Diff the original PC Prefab/Materials/Textures against a pre-run snapshot (e.g. git status /
   asset timestamps). **Expect**: zero changes (SC-002 / Constitution I).

## Scenario 2 — Texture merge with aspect ratio (User Story 1, Acceptance Scenario 2)

1. Using the Material that references multiple source Textures, run Generate (or use Preview —
   see Scenario 5).
2. **Expect**: a single merged Quest Texture is produced for that Material's Color classification
   (and separately for any other classified type it uses — Normal/Mask/Emission, per FR-008); each
   source image's proportions are visibly preserved in the merged layout (SC-003).

## Scenario 3 — Resize without modifying source (User Story 1, Acceptance Scenario 3)

1. Using the oversized test Texture, run Generate.
2. **Expect**: the generated Quest Texture's longest edge is ≤ the configured max (default 1024,
   SC-004); the original PC Texture asset is byte-for-byte unchanged.

## Scenario 4 — AAO absent (User Story 1, Acceptance Scenario 5)

1. Temporarily remove/disable the AAO package from the test project.
2. Attempt Generate.
3. **Expect**: generation halts before creating a partial/broken output; a clear message explains
   AAO is required (FR-013). Re-enable AAO afterward.

## Scenario 5 — Preview before generating (User Story 3, Acceptance Scenario 1)

1. Select the multi-texture Material and open its Preview (no Generate click).
2. **Expect**: source textures, merged result, and final resolution are shown; no new asset exists
   on disk as a result of opening the preview.

## Scenario 6 — Compatibility check & PhysBone validation (User Story 3, Acceptance Scenario 3; FR-016a)

1. Run Generate on the avatar containing the flagged component (e.g. `AudioSource`) and PhysBone
   components.
2. **Expect**: the compatibility results list the flagged `AudioSource` object individually (not
   just a count), with no automatic removal (FR-021 — object still present unless you explicitly
   choose to remove it). PhysBone/Collider/affected-transform counts are shown against the
   externalized threshold data (contracts/extension-data-contracts.md §2), with a resulting
   Quest Performance Rank indication.

## Scenario 7 — Re-generation / overwrite confirmation (FR-022)

1. Run Generate again for the same PC avatar (already converted in Scenario 1).
2. **Expect**: a confirmation dialog appears before anything is overwritten; declining leaves the
   prior Quest output untouched; accepting replaces it in place at the same output path (no
   duplicate/orphaned folder is created).

## Scenario 8 — Unmapped shader (FR-006 failure path)

1. Point the tool at a Material using a Shader with no registered `ShaderConversionRuleSet`
   (contracts/extension-data-contracts.md §1).
2. **Expect**: that Material is reported as an explicit conversion failure in the Conversion Report
   (FR-019), not silently skipped and not guessed at.

## Success Criteria Traceability

| Scenario | Success Criteria covered |
|---|---|
| 1 | SC-001, SC-002 |
| 2 | SC-003 |
| 3 | SC-004 |
| 5 | (US3 preview requirement, FR-017) |
| 6 | SC-005 |
| 7 | SC-006 |
