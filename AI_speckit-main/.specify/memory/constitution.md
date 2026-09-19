<!--
Sync Impact Report
- Version change: (none) → 1.0.0
- Modified principles: N/A (initial ratification)
- Added sections: Core Principles (I-VI), Technology & Environment Constraints,
  Development Workflow, Governance
- Removed sections: none
- Templates requiring updates:
  - .specify/templates/plan-template.md ⚠ pending manual check (Constitution Check gate should
    reference principles I-VI)
  - .specify/templates/spec-template.md ✅ no changes required (principle-agnostic)
  - .specify/templates/tasks-template.md ✅ no changes required (principle-agnostic)
- Follow-up TODOs:
  - TODO(RATIFICATION_DATE): set to the date this constitution is first approved by the project
    owner; defaulted to the drafting date because no prior constitution or explicit ratification
    date was supplied.
-->

# Quest Avatar Converter Constitution

## Core Principles

### I. Source Immutability (NON-NEGOTIABLE)
The PC-platform avatar and every asset it references (Prefab, Material, Texture, Animation,
Animator Controller, VRC components) MUST NOT be modified, moved, or overwritten by any
conversion operation. All Quest-facing output MUST be written as new assets under a dedicated
Quest output location. A run of the tool MUST be safely re-runnable without ever mutating the
original PC assets, even if the Quest output already exists and is being regenerated or
overwritten.
Rationale: The tool's entire value proposition is generating a Quest-compatible derivative
without risking the artist's PC-quality source content; any code path that writes to a
PC-namespace asset is a defect, not an edge case.

### II. Referenced-Asset-Only Processing
The tool MUST derive its working set by traversing actual reference graphs from the avatar root
(Renderer → Material → Texture, plus Animator/Animation/VRC component asset references) rather
than copying entire project folders. Assets not reachable from the avatar's reference graph MUST
NOT be copied into the Quest output. The definition of "referenced" MUST be documented and kept
current as new reference sources are discovered (e.g., animation-driven material/texture swaps).
Rationale: Unconditional folder copies waste build time, bloat the project, and make it
impossible to reason about what the Quest avatar actually depends on.

### III. Rule-Based, Externalized Conversion Logic
Shader property mappings, Quest/mobile capability limits, VRChat performance-rank thresholds, and
similar fast-changing external facts MUST be expressed as data or pluggable rule objects (e.g.
per-shader `IShaderConversionRule` implementations, mapping tables/ScriptableObjects) rather than
hard-coded inline in conversion logic. Adding support for a new source or target shader, or
updating a performance threshold, MUST be possible without modifying the core conversion pipeline.
Rationale: VRChat SDK limits, AAO APIs, and recommended Quest shaders change over time; the
architecture must absorb that churn without rewrites, per the source document's explicit warning
against baking these values into code.

### IV. Separation of Concerns Across the Pipeline
The pipeline MUST be decomposed into independently responsible, independently testable units
(at minimum: avatar duplication, asset resolution, material conversion, shader property mapping,
texture analysis, texture atlas generation, texture resizing, renderer/material replacement, AAO
integration, Quest-compatibility checking, performance analysis, and conversion reporting). A
unit MUST NOT reach into another unit's internal state; units communicate through an explicit
conversion context/data structure (source↔generated asset maps, settings, log). Texture-atlas
logic in particular MUST remain reusable independent of which shader is targeted.
Rationale: Mirrors the architecture already worked out in the requirements analysis and is what
makes future shader/texture-format additions tractable instead of a rewrite.

### V. Idempotent, Traceable Regeneration
Every conversion run MUST record a source-to-generated mapping (at minimum Material→Material and
Texture→Texture) and MUST consult it before creating new assets, so that re-converting the same
PC avatar does not silently accumulate duplicate or orphaned Quest assets. The tool MUST make its
overwrite/update/fresh-regenerate behavior explicit to the user before destructive changes to
previously generated Quest output occur. Every run MUST produce a human-readable conversion
report (what was generated, skipped, warned, or failed) sufficient to explain the result without
reading source code.
Rationale: Conversion will be run repeatedly during avatar iteration; unpredictable duplication or
silent overwrites of the user's prior Quest output erodes trust in the tool.

### VI. Testable Core Logic, Isolated from the Unity Editor
Algorithmic logic that does not require live Unity Editor/Asset Database state — shader property
mapping resolution, aspect-ratio-preserving atlas placement, texture-size optimization math,
performance-metric aggregation — MUST be implemented so it can be unit-tested without an Editor
context (plain C# classes/methods taking explicit inputs and returning explicit outputs). Editor-
and AssetDatabase-dependent glue code MUST stay thin and separable from that logic. Automated
tests for this core logic MUST be added alongside the feature that introduces it, not deferred.
Rationale: A Unity Editor tool cannot practically run in a full TDD red-green loop against the
Editor itself, but the parts of this tool most prone to subtle bugs (packing math, property
mapping, resize math) are exactly the parts that CAN and MUST be tested in isolation.

## Technology & Environment Constraints

- Target runtime: Unity Editor tooling (C#), producing avatars for the VRChat Quest/Android
  (mobile) platform target.
- Hard external dependency: AAO (Avatar Optimizer). The tool MUST detect whether AAO is present
  and fail with a clear, actionable message (not a silent no-op or an unhandled exception) when it
  is absent, since Trace And Optimize application is a required pipeline step per the agreed
  workflow.
- Exact supported Unity version, VRChat SDK version, and AAO version ranges are open
  clarification items and MUST be resolved and recorded in the feature spec before `/speckit-plan`
  is finalized for the initial release; this constitution does not itself pin those versions so
  that it does not need amendment every time a supported-version range changes.
- Quest hardware/platform capability limits (unsupported component types, texture/triangle/bone
  budgets, performance-rank thresholds) MUST be sourced from the externalized rule data described
  in Principle III, not duplicated as inline magic numbers.

## Development Workflow

- This project follows Spec-Driven Development via Spec Kit: every feature proceeds through
  `/speckit-specify` → (optional) `/speckit-clarify` → `/speckit-plan` → `/speckit-tasks` →
  (optional) `/speckit-analyze` / `/speckit-checklist` → `/speckit-implement`.
- Ambiguities identified in the source requirements analysis (shader/texture conversion
  algorithms, avatar-duplication scope, non-Quest-compatible component handling, regeneration
  semantics — see the project's requirements analysis document) MUST be resolved via
  `/speckit-clarify` and recorded in the spec before `/speckit-plan` proceeds on the affected
  area, rather than left as implicit assumptions in code.
- Each `/speckit-plan` MUST include an explicit Constitution Check against Principles I-VI before
  design work is considered complete.

## Governance

This constitution supersedes ad hoc engineering conventions for this project. Amendments require:
(1) a documented rationale for the change, (2) a version bump following the semantic versioning
policy below, and (3) review of dependent templates (plan/spec/tasks) for needed updates as
tracked in the Sync Impact Report at the top of this file.

Versioning policy:
- MAJOR: Backward-incompatible governance changes, or removal/redefinition of a principle.
- MINOR: A new principle or materially expanded section is added.
- PATCH: Clarifications, wording fixes, or non-semantic refinements.

All `/speckit-plan` runs MUST verify compliance with this constitution's Constitution Check
section. Any deviation MUST be explicitly justified in the plan's Complexity Tracking section or
the deviation MUST be removed.

**Version**: 1.0.0 | **Ratified**: TODO(RATIFICATION_DATE): confirm with project owner | **Last Amended**: 2026-09-19
