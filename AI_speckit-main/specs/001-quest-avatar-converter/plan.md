# Implementation Plan: Quest Avatar Converter

**Branch**: `001-quest-avatar-converter` | **Date**: 2026-09-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-quest-avatar-converter/spec.md`

## Summary

A Unity Editor tool that generates a Quest/Android-compatible derivative of a PC-platform VRChat
avatar without modifying the PC avatar or any asset it references. The primary technical approach:
a pipeline of independently testable, data-driven pipeline stages (avatar duplication, static
reference-graph asset resolution, per-shader-pair Material/Property conversion, per-Material
per-texture-type Texture Atlas generation, aspect-ratio-preserving resize to a configurable max
size, AAO `Trace And Optimize` integration, Quest/PhysBone compatibility checking against
externalized rule data, and a conversion report) orchestrated by an Editor Window UI, packaged as
a UPM package so it can be distributed the same way the project's AAO dependency is (via VRChat
Creator Companion / a VPM listing).

## Technical Context

**Language/Version**: C# targeting Unity 2022.3 LTS's supported Roslyn/language version (Unity
2022.3.22f1 — research.md §1)

**Primary Dependencies**: Unity Editor 2022.3.5f1 (locally available; pinned target remains
2022.3.22f1, see research.md §1) — `UnityEditor`/`UnityEngine` APIs, `AssetDatabase`. VRChat SDK3 -
Avatars 3.10.5 + SDK3 - Base 3.10.5 (assemblies `VRC.SDK3A`, `VRC.SDKBase`; PhysBone/PhysBone
Collider types are in the precompiled `VRC.SDK3.Dynamics.PhysBone` assembly bundled inside
SDK3-Base — research.md §2). `nadena.dev.ndmf` 1.14.8, a transitive dependency of AAO discovered
during implementation (research.md §2b). Avatar Optimizer ("AAO") 1.9.19
(`anatawa12/AvatarOptimizer`; assembly `com.anatawa12.avatar-optimizer.runtime`; the
`Trace And Optimize` component is `Anatawa12.AvatarOptimizer.TraceAndOptimize`, added via
`AddComponent<T>()` since its constructor is `internal` — research.md §3, verified from source).
Target (Quest-side) shaders are restricted to VRChat's own `VRChat/Mobile/*` family (`Toon Lit`,
`Toon Standard`, etc.) — research.md §4.

**Storage**: N/A — all state is Unity `Asset` files on disk (Prefabs, Materials, Textures) plus an
in-memory `ConversionContext` for the duration of a single generation run; no external database.

**Testing**: Unity Test Framework (`com.unity.test-framework`), EditMode test assembly, per
Constitution Principle VI — pure algorithmic logic (shader property mapping resolution, texture
atlas placement math, resize math, PhysBone threshold evaluation) is implemented as plain C#
classes taking explicit inputs so it is testable without a live Editor/AssetDatabase context;
Editor/AssetDatabase-dependent glue code stays thin around it.

**Target Platform**: Unity Editor tool (Windows/Mac/Linux host), producing avatars whose *output*
targets the VRChat Quest/Android runtime platform.

**Project Type**: Single Unity Editor tool, packaged as a UPM (Unity Package Manager) package —
this matches how the tool's own AAO dependency is distributed and keeps the tool installable via
VRChat Creator Companion into any VRChat avatar project.

**Performance Goals**: No throughput/latency SLA is defined in the spec (this is an interactive
Editor operation, not a hot-path service); the operative goals are the spec's Success Criteria
(SC-001–SC-006), which are correctness/UX outcomes, not timing targets. Generation is expected to
run synchronously within an Editor session for a single avatar at a time (see Assumptions in
spec.md — batch conversion is out of scope).

**Constraints**: MUST NOT write to any PC-namespace asset (Constitution I / FR-001). Quest Texture
longest edge MUST NOT exceed the configured max (default 1024px, FR-009). Shader property mappings
and Quest/PhysBone compatibility thresholds MUST be externalized data, not hard-coded (Constitution
III, FR-016a, FR-020). Tool MUST halt with a clear message rather than proceed when AAO is absent
(FR-013).

**Scale/Scope**: One PC avatar processed per run; typical avatar complexity is on the order of a
handful to a few dozen Materials/Textures. VRChat's current published Quest Performance Rank
thresholds (research.md §5) top out at 8 PhysBone components / 64 affected transforms / 16
Colliders at the "Poor" tier, with a hard per-component cap of 256 affected transforms — these are
the reference numbers `PhysBoneValidator` (FR-016a) evaluates against.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Gate | Status |
|---|-----------|------|--------|
| I | Source Immutability | All pipeline stages that touch PC-namespace assets (`AssetResolver`, `AvatarDuplicator`) are read-only against them; every write targets a path under the Quest output root only. No stage holds a reference that permits writing to a PC asset. | PASS — design keeps PC-asset access read-only by construction (separate `SourceAssetReader` vs `QuestAssetWriter` responsibilities). |
| II | Referenced-Asset-Only Processing | `AssetResolver` traverses only the static `sharedMaterial` reference graph (FR-003); no project-wide folder copy exists anywhere in the design. | PASS |
| III | Rule-Based, Externalized Conversion Logic | Shader Property Mappings (`ShaderPropertyMapper` + `IShaderConversionRule` data assets) and Quest/PhysBone compatibility thresholds (`QuestCompatibilityRules` data assets) are ScriptableObject-based data, not inline constants. | PASS |
| IV | Separation of Concerns | Project Structure below assigns one class/module per pipeline responsibility (`AvatarDuplicator`, `AssetResolver`, `MaterialConverter`, `ShaderPropertyMapper`, `TextureAnalyzer`, `TextureAtlasGenerator`, `TextureResizer`, `RendererMaterialReplacer`, `AAOIntegrator`, `QuestCompatibilityChecker`, `PhysBoneValidator`, `PerformanceAnalyzer`, `ConversionReport`), communicating only through `ConversionContext`. | PASS |
| V | Idempotent, Traceable Regeneration | `ConversionContext` holds `MaterialMap`/`TextureMap` for the run; FR-022's overwrite-with-confirmation is a dedicated pre-flight step (`ExistingOutputDetector`) before any generation stage runs. | PASS |
| VI | Testable Core Logic, Isolated from Editor | `TextureAtlasGenerator`'s placement math, `TextureResizer`'s resize math, `ShaderPropertyMapper`'s resolution logic, and `PhysBoneValidator`'s threshold evaluation are designed as pure functions/classes over plain data (rects, sizes, mapping tables), with a thin `AssetDatabase`-facing wrapper layer calling them. An `Editor.Tests` EditMode assembly is part of the package from the start. | PASS |

No violations — Complexity Tracking is not needed.

**Post-Phase-1 re-check**: data-model.md, contracts/extension-data-contracts.md, quickstart.md, and
research.md were reviewed against the table above after Phase 1 design. The one design refinement
made during Phase 1 (constraining `ShaderConversionRuleSet.TargetShader` to VRChat's own
`VRChat/Mobile/*` family, research.md §4) strengthens Principle III rather than weakening it — it
is itself an externalized, citable rule (research.md), not a hard-coded pipeline branch. All six
principles remain PASS; no new Complexity Tracking entries required.

## Project Structure

### Documentation (this feature)

```text
specs/001-quest-avatar-converter/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
Packages/com.vrc-rufu.quest-avatar-converter/     # UPM package (name adjustable; see research.md)
├── package.json
├── Editor/
│   ├── QuestAvatarConverterWindow.cs              # EditorWindow UI (US1/US2/US3 controls)
│   ├── Pipeline/
│   │   ├── ConversionContext.cs                   # MaterialMap/TextureMap/Settings/Log (Principle V)
│   │   ├── ConversionPipeline.cs                  # Orchestrates stages in the FR-defined order
│   │   ├── AssetResolver.cs                        # Static reference-graph traversal (FR-003)
│   │   ├── AvatarDuplicator.cs                     # Independent Prefab copy (FR-023)
│   │   ├── ExistingOutputDetector.cs                # Overwrite-with-confirmation gate (FR-022)
│   │   ├── RendererMaterialReplacer.cs              # FR-010/FR-011
│   │   ├── AAOIntegrator.cs                         # AAO presence check + Trace And Optimize add (FR-012/013)
│   │   ├── QuestCompatibilityChecker.cs             # Generic flagged-component check (FR-016/FR-021)
│   │   ├── PhysBoneValidator.cs                     # PhysBone-specific quantitative check (FR-016a)
│   │   ├── PerformanceAnalyzer.cs                   # Metrics (FR-015)
│   │   └── ConversionReport.cs                      # Run report (FR-019)
│   ├── Materials/
│   │   ├── MaterialConverter.cs                     # FR-006
│   │   ├── IShaderConversionRule.cs                 # Constitution III contract
│   │   ├── ShaderPropertyMapper.cs                  # Pure mapping-resolution logic (Constitution VI)
│   │   └── GpuInstancingApplier.cs                  # FR-020a
│   └── Textures/
│       ├── TextureTypeClassifier.cs                 # Color/Normal/Mask/Emission (FR-008)
│       ├── TextureAtlasGenerator.cs                  # Per-Material, per-type atlas (FR-007/FR-008)
│       ├── TextureResizer.cs                         # Aspect-preserving max-size resize (FR-009)
│       └── TextureUvTilingDetector.cs                 # Warn-only tiling detection (Edge Case)
├── Editor.Tests/                                     # Unity Test Framework EditMode assembly (Constitution VI)
│   ├── ShaderPropertyMapperTests.cs
│   ├── TextureAtlasPlacementTests.cs
│   ├── TextureResizerTests.cs
│   └── PhysBoneValidatorTests.cs
└── Data/
    ├── ShaderConversionRules/                        # ScriptableObject assets (Constitution III)
    └── QuestCompatibilityRules/                       # Component + PhysBone threshold data (Constitution III)
```

**Structure Decision**: Single Unity Editor tool delivered as one UPM package (no separate
frontend/backend split — this is a local Editor tool, not a client/server application). Pipeline
logic lives under `Editor/Pipeline`, `Editor/Materials`, and `Editor/Textures`, split by
responsibility per Constitution Principle IV; externalized rule data lives under `Data/` as
ScriptableObject assets per Constitution Principle III; a dedicated `Editor.Tests` assembly holds
EditMode tests for the Editor-independent core logic per Constitution Principle VI. Package ID:
`com.vrc-rufu.quest-avatar-converter` (confirmed with the project owner 2026-09-19). A local
Unity 2022.3.5f1 dev/test project hosting this package (plus VRChat SDK3-Base/Avatars, NDMF, and
AAO as embedded/git-URL packages so the package can actually compile and run its EditMode tests)
lives at `UnityProject/` under the repository root — the `Packages/...` path below is therefore
`UnityProject/Packages/com.vrc-rufu.quest-avatar-converter/` on disk.

## Complexity Tracking

*No Constitution Check violations — this section is intentionally empty.*
