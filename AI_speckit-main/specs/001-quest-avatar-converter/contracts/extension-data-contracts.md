# Extension Data Contracts

This tool's only "external interface" (per Constitution Principle III / FR-020) is the data schema
that lets a new Shader pair or a new Quest-compatibility rule be registered **without modifying
core pipeline code**. This document is the contract for those two ScriptableObject asset types.
Anyone (including future maintainers) authoring a new rule asset must conform to this contract; the
pipeline code must never read fields not defined here without a corresponding contract update.

## 1. ShaderConversionRuleSet (`Data/ShaderConversionRules/*.asset`)

One asset instance = one Source Shader → Target Shader conversion rule, consumed by
`MaterialConverter`/`ShaderPropertyMapper` (FR-006, FR-020).

```csharp
// Contract shape — see data-model.md for the authoritative field list.
class ShaderConversionRuleSet : ScriptableObject
{
    Shader SourceShader;              // REQUIRED. Exact shader asset this rule applies to.
    Shader TargetShader;              // REQUIRED. Must be one of the shaders offered in FR-005's picker.
    PropertyMapping[] PropertyMappings;
}

struct PropertyMapping
{
    string SourcePropertyName;        // REQUIRED. Must exist on SourceShader.
    string TargetPropertyName;        // REQUIRED. Must exist on TargetShader.
    TextureClassification TargetClassification; // REQUIRED for texture properties (FR-008 routing).
}
```

**Invariants (validated at load time, not silently ignored):**

- `TargetShader` MUST be one of VRChat's own `VRChat/Mobile/*` shaders (e.g. `Toon Lit`,
  `Toon Standard`). VRChat hard-restricts avatar shaders on Quest/Android to this bundled family —
  a rule asset naming any other shader as its target (including a shader merely branded
  "Quest-compatible" by a third-party author, e.g. a lilToon or Poiyomi variant) MUST fail load-time
  validation rather than being silently accepted (research.md §4). `SourceShader` has no such
  restriction — any PC-side shader may be a valid source.
- `SourceShader` + `TargetShader` pair MUST be unique across all loaded rule assets. On duplicate,
  the loader MUST raise a load-time error (surfaced in the Conversion Report) rather than silently
  picking one.
- Every `SourcePropertyName` MUST correspond to a property the loader can find via
  `Shader.GetPropertyCount`/`GetPropertyName` on `SourceShader`; unresolvable entries MUST be
  reported as a rule-authoring warning at load time, not at conversion time.
- A `PCMaterial` whose `Shader` has no matching `ShaderConversionRuleSet.SourceShader` is a
  conversion failure for that Material (FR-006) — this is the pipeline's contract-consumption
  behavior, not something a rule author configures.

**Adding a new supported source/target shader pair = adding one new asset of this type.** No
change to `MaterialConverter`, `ShaderPropertyMapper`, or `ConversionPipeline` is permitted for
this purpose (Constitution III).

## 2. QuestCompatibilityRules (`Data/QuestCompatibilityRules/*.asset`)

Consumed by `QuestCompatibilityChecker` (FR-016) and `PhysBoneValidator` (FR-016a). Two rule kinds
share one contract file so both stay swappable as VRChat's published limits change.

```csharp
class QuestCompatibilityRules : ScriptableObject
{
    FlaggedComponentRule[] FlaggedComponents;   // FR-016: generic unsupported/restricted components
    PhysBoneThresholds PhysBoneLimits;          // FR-016a: quantitative PhysBone limits
}

struct FlaggedComponentRule
{
    string ComponentTypeName;         // REQUIRED. Fully-qualified type name, matched via reflection.
    string ReasonMessage;             // REQUIRED. Shown to the user per flagged object (FR-016).
}

struct PhysBoneThresholds
{
    int MaxPhysBoneComponents;        // Per VRChat's published Quest Performance Rank reference.
    int MaxPhysBoneColliders;
    int MaxPhysBoneAffectedTransforms;
    string SourceCitation;            // REQUIRED. Where these numbers came from (doc/version), for traceability.
}
```

**Invariants:**

- `ComponentTypeName` entries that fail to resolve to a real `Component`-derived type at load time
  MUST be reported as a rule-authoring warning, not silently skipped.
- `PhysBoneThresholds.SourceCitation` MUST be non-empty — a threshold with no citation is treated
  as a rule-authoring error, since Constitution III requires these values be traceable to an
  external, updatable source rather than an opaque magic number.
- Updating VRChat's published Quest Performance Rank numbers = editing this one asset's values (and
  citation). No change to `PhysBoneValidator`'s evaluation logic is required or permitted for a
  pure threshold-number update.

## Non-Goals

- Neither contract includes a version/schema-migration field in v1; the spec's Assumptions
  explicitly defer a user-facing rule-authoring UI, so these are plain Inspector-edited
  ScriptableObject assets, not a versioned wire format consumed by external tooling.
- There is no HTTP/CLI/IPC contract in this feature — the entire tool runs in-process inside the
  Unity Editor (see plan.md Technical Context: Target Platform).
