# Data Model: Quest Avatar Converter

Derived from spec.md's Key Entities section, expanded with the fields needed by the pipeline
stages described in plan.md. This is a Unity Editor tool — "entities" here are in-memory C#
types and Unity `Asset`/`ScriptableObject` references for the duration of one generation run, not
rows in a persistent database (see Constitution: Storage = N/A).

## Core Run-Scoped Entities

### ConversionContext
The single object all pipeline stages read from and write to (Constitution Principle IV: stages
communicate only through this, never through each other's internals).

| Field | Type | Notes |
|---|---|---|
| SourceAvatar | reference to PCAvatar | Read-only for the whole run (Constitution I) |
| QuestAvatar | reference to QuestAvatar | Populated once `AvatarDuplicator` runs |
| OutputRoot | asset path | `Assets/<QuestConvertedRoot>/<AvatarName>/` (FR-002) |
| Settings | ConversionSettings | See below |
| MaterialMap | dict\<PCMaterial, QuestMaterial\> | FR-011/FR-014: one entry per distinct source Material, so sharing is enforced by construction |
| TextureMap | dict\<(PCMaterial, TextureClassification), QuestTexture\> | FR-007/FR-008: keyed per-Material *and* per classified type, since atlases are scoped per Material per type |
| Log | list\<ConversionLogEntry\> | FR-019; append-only during a run |
| CompatibilityFindings | list\<CompatibilityFinding\> | FR-016/FR-016a/FR-021 |
| PerformanceMetrics | PerformanceMetrics | FR-015 |

### ConversionSettings
User-configured options from the Editor Window (US2).

| Field | Type | Notes |
|---|---|---|
| TargetShaderRule | reference to ShaderConversionRuleSet | FR-005 |
| PlacementOffset | Vector3 | FR-004 |
| MaxTextureSize | int | Default 1024 (FR-009) |
| MergeTexturesEnabled | bool | FR-018 |
| ResizeTexturesEnabled | bool | FR-018 |
| AddAaoComponentEnabled | bool | FR-018 |

### ConversionLogEntry
| Field | Type | Notes |
|---|---|---|
| Level | enum {Info, Warning, Error} | e.g. UV-tiling warning, unmapped-Property warning (Edge Cases) |
| Message | string | Human-readable |
| RelatedAsset | asset reference (nullable) | For "jump to object" style UX (source doc §13) |

## Avatar & Renderer Entities

### PCAvatar
| Field | Type | Notes |
|---|---|---|
| RootPrefab | Prefab reference | Never written to (Constitution I) |
| Renderers | list\<RendererRef\> | SkinnedMeshRenderer/MeshRenderer discovered via static traversal (FR-003) |
| VrcAvatarDescriptor | component reference | Followed for avatar-level config, not treated as "runtime asset swap" (FR-003) |

### QuestAvatar
| Field | Type | Notes |
|---|---|---|
| RootPrefab | Prefab reference | Independent copy, not a Variant (FR-023) |
| Renderers | list\<RendererRef\> | Mirrors PCAvatar.Renderers 1:1; each renderer's material list is replaced in place (FR-010) |
| AaoTraceAndOptimize | component reference | Added to root (FR-012) |

### RendererRef
| Field | Type | Notes |
|---|---|---|
| Transform path | string | Used to match a PC renderer to its Quest counterpart after duplication |
| SourceMaterials | list\<PCMaterial\> | Renderer's `sharedMaterials` at conversion time (FR-003) |

## Material & Shader Entities

### PCMaterial
| Field | Type | Notes |
|---|---|---|
| Asset reference | Material | Never written to |
| Shader | Shader reference | Used to look up a ShaderConversionRule |
| Properties | list\<MaterialProperty\> | Texture/Color/Float properties actually set on the Material |

### QuestMaterial
| Field | Type | Notes |
|---|---|---|
| Asset reference | Material | Newly created under OutputRoot/Materials |
| Shader | Shader reference | = ConversionSettings.TargetShaderRule's target shader |
| SourcePCMaterial | reference to PCMaterial | For MaterialMap traceability (FR-014) |
| GpuInstancingEnabled | bool | Always true post-generation (FR-020a) |

### MaterialProperty
| Field | Type | Notes |
|---|---|---|
| Name | string | e.g. `_MainTex`, `_BumpMap` |
| Classification | TextureClassification \| Color \| Scalar | Drives FR-008 grouping |
| TextureValue | Texture reference (nullable) | |
| UvScale / UvOffset | Vector2 / Vector2 | Used only for tiling *detection* (Edge Case), not remapping, in v1 |

### TextureClassification (enum)
`Color`, `Normal`, `Mask`, `Emission` — FR-008's four required categories.

### ShaderConversionRuleSet (ScriptableObject, Constitution III / FR-020)
| Field | Type | Notes |
|---|---|---|
| SourceShader | Shader reference | |
| TargetShader | Shader reference | e.g. a Quest-compatible shader from research.md |
| PropertyMappings | list\<PropertyMapping\> | |

### PropertyMapping
| Field | Type | Notes |
|---|---|---|
| SourcePropertyName | string | e.g. `_MainTex` |
| TargetPropertyName | string | e.g. `_BaseMap` |
| TargetClassification | TextureClassification \| Color \| Scalar | Used to route into the right atlas (FR-008) |

## Texture Entities

### PCTexture
| Field | Type | Notes |
|---|---|---|
| Asset reference | Texture2D | Never written to |
| Width / Height | int | Source resolution, aspect ratio preserved downstream |
| HasAlpha | bool | Drives FR-009's alpha-preservation rule |

### QuestTexture
| Field | Type | Notes |
|---|---|---|
| Asset reference | Texture2D (PNG) | Newly created under OutputRoot/Textures (FR-009) |
| SourcePCTextures | list\<PCTexture\> | 1 entry if not merged, N entries if this is an atlas (FR-007) |
| Classification | TextureClassification | One QuestTexture per (Material, Classification) pair (FR-008) |
| Layout | AtlasLayout (nullable) | Present only when SourcePCTextures.Count > 1 |
| FinalSize | (int width, int height) | ≤ ConversionSettings.MaxTextureSize on the longest edge (FR-009) |

### AtlasLayout
| Field | Type | Notes |
|---|---|---|
| Placements | list\<AtlasPlacement\> | One per source texture |

### AtlasPlacement
| Field | Type | Notes |
|---|---|---|
| SourceTexture | reference to PCTexture | |
| DestRect | (x, y, width, height) | Aspect-ratio-preserving placement within the merged image (FR-007) |

## Compatibility & Performance Entities

### CompatibilityFinding
| Field | Type | Notes |
|---|---|---|
| TargetObject | GameObject/Component reference | FR-016: listed individually, not only aggregated |
| Reason | string | e.g. "Camera component unsupported on Quest" |
| Severity | enum {Warning, Blocking-if-unaddressed} | |
| UserDecision | enum {Undecided, Remove, Keep} | Defaults to Undecided; FR-021 forbids auto-remove |

### PhysBoneMetrics (specialization feeding into CompatibilityFinding, FR-016a)
| Field | Type | Notes |
|---|---|---|
| PhysBoneComponentCount | int | |
| PhysBoneColliderCount | int | |
| PhysBoneAffectedTransformCount | int | |
| PhysBoneCollisionCheckCount | int | 4th metric in research.md §5's tier table |
| MaxAffectedTransformsOnAnySingleComponent | int | Compared against the 256 hard cap (research.md §5), independently of tier |
| ThresholdsSource | reference to QuestCompatibilityRules asset | Externalized data (Constitution III), not hard-coded; see contracts §2's `PhysBoneThresholds` (4 tiers + hard cap) |
| ResultingRank | enum {Excellent, Good, Medium, Poor} | The best tier whose thresholds are NOT exceeded by any of the 4 counts above |
| ExceedsHardCap | bool | True if `MaxAffectedTransformsOnAnySingleComponent` > the hard cap — reported as a distinct, higher-severity finding than a rank downgrade (FR-016a) |

### PerformanceMetrics (FR-015)
| Field | Type | Notes |
|---|---|---|
| TriangleCount | int | |
| MaterialCount | int | |
| SkinnedMeshRendererCount | int | |
| BoneCount | int | |
| TextureCount | int | |
| EstimatedTextureMemoryBytes | long | |

## Relationships Summary

- One `PCAvatar` → exactly one `QuestAvatar` per run (overwritten in place on re-run, FR-022).
- One `PCMaterial` → at most one `QuestMaterial` (shared across all referencing Renderers, FR-011).
- One `(PCMaterial, TextureClassification)` pair → at most one `QuestTexture`, which may aggregate
  multiple `PCTexture`s via an `AtlasLayout` (FR-007/FR-008).
- `ShaderConversionRuleSet` is looked up by `PCMaterial.Shader` → if absent, that Material is
  reported as a failed/unsupported conversion (FR-006), not silently skipped.
