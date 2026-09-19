# Phase 0 Research: Quest Avatar Converter

All items below resolve the "NEEDS CLARIFICATION" version/technology unknowns from plan.md's
Technical Context. Per the spec's Assumptions and Constitution Principle III, these are concrete,
time-bound values recorded here (not hard-coded into pipeline logic, and not pinned in the
constitution) — they are expected to need periodic re-verification as VRChat/Unity/AAO evolve.

## 1. Target Unity Editor version

- **Decision**: Target **Unity 2022.3.22f1** as the officially supported Editor version (matches
  spec Clarifications: "current latest Unity LTS" per VRChat's own requirement).
- **Rationale**: VRChat's Creation docs state this is the currently required Unity version for
  avatar creation via VRChat Creator Companion (VCC); building against a different version risks
  upload/compatibility failures.
- **Alternatives considered**: Newer 2022.3.x patches (none currently recommended by VRChat);
  Unity 6 / 6000.x (not yet the VRChat-targeted version at time of writing).
- **Source**: VRChat Creation docs, "Current Unity Version" page.
- **Confidence**: High (official first-party source).
- **Re-verification trigger**: Before each release, re-check VRChat's current-Unity-version page —
  this number changes when VRChat migrates SDK support.

## 2. VRChat SDK3 - Avatars version

- **Decision**: Target **SDK3 - Avatars 3.10.5**, depending on **SDK3 - Base 3.10.5** (its own
  declared `vpmDependencies`). Both Unity 2022.3-based.
- **Rationale**: Verified directly against VRChat's live VPM package index
  (`https://vrchat.github.io/packages/index.json`) on 2026-09-19 — 3.10.5 is the latest non-beta
  release (3.10.4 was superseded; earlier research using aggregated search results had a
  now-stale patch number). `com.vrchat.avatars@3.10.5` declares `vpmDependencies:
  {"com.vrchat.base": "3.10.5"}`.
- **Alternatives considered**: Pinning to 3.10.4 (superseded) or beta releases (3.10.4-beta.*,
  3.10.5-beta.*) — rejected, prefer the latest stable.
- **Source**: `https://vrchat.github.io/packages/index.json` (VRChat's own published VPM index),
  fetched and parsed directly.
- **Confidence**: High — directly fetched and parsed, not aggregated-search-derived.
- **Distribution mechanism note**: VRChat does **not** publish these packages' source in a
  git-clonable form (the `vrchat/packages` GitHub repo holds only VPM-listing metadata, not package
  source) — each version is a `.zip` release asset
  (`https://github.com/vrchat/packages/releases/download/<version>/<package>-<version>.zip`).
  Without VCC, the reliable install path is: download that zip and extract it as an **embedded
  local package** under the consuming project's `Packages/<package-name>/` folder (this is what
  `/speckit-implement` did to stand up the local dev/test project — see plan.md).
- **Re-verification trigger**: Same cadence as Unity version above.

## 2b. Newly discovered transitive dependency: NDMF

- **Decision**: Add **`nadena.dev.ndmf` 1.14.8** ("Non-Destructive Modular Framework") as an
  explicit dependency, installed via git URL package reference
  (`https://github.com/bdunderscore/ndmf.git#1.14.8` — its `package.json` is at the repo root, no
  `?path=` needed).
- **Rationale**: AAO's own `package.json` declares `vpmDependencies: {"nadena.dev.ndmf": ">=1.8.0
  <2.0.0", "com.vrchat.avatars": ">=3.7.0 <3.11.0"}`. Standard Unity Package Manager does not read
  the VPM-specific `vpmDependencies` field, so this would silently fail to resolve unless added
  explicitly. `com.vrchat.avatars` 3.10.5 satisfies AAO's avatars range; not previously documented
  in this research file — added now to prevent a missing-dependency compile error.
- **Source**: Directly fetched `package.json` from `anatawa12/AvatarOptimizer` (master) and
  `bdunderscore/ndmf` (tags) on 2026-09-19.
- **Confidence**: High (directly fetched).

## 3. Avatar Optimizer (AAO)

- **Decision**: Target **AAO 1.9.19** (`anatawa12/AvatarOptimizer`, MIT-licensed, distributed via
  VPM — the same distribution mechanism this tool itself will use, see plan.md Project Structure).
- **Rationale**: Latest version per its docs portal at time of writing. Its "Trace And Optimize"
  feature is confirmed to be an Inspector-configurable **Avatar Global Component** added to the
  avatar root — i.e. a MonoBehaviour-style component with serialized settings, consistent with
  spec FR-012 ("add an AAO Trace And Optimize component to the root") and with Constitution
  Principle VI's assumption that the Editor-facing glue around it (`AAOIntegrator.cs`) stays thin.
- **Alternatives considered**: None — AAO is the only actively maintained tool for this role in the
  VRChat ecosystem.
- **Source**: AAO GitHub repository; AAO "Trace And Optimize" reference docs; directly fetched
  `Runtime/TraceAndOptimize.cs` and `Runtime/com.anatawa12.avatar-optimizer.runtime.asmdef` at tag
  `v1.9.19`.
- **Confidence**: High — verified by reading source directly (2026-09-19, during `/speckit-implement`).
  **Verified findings (open item closed):**
  - Class: `Anatawa12.AvatarOptimizer.TraceAndOptimize`, `sealed`, extends `AvatarGlobalComponent`.
  - Its constructor is `internal` — it cannot be `new`'d from outside AAO's assembly, but
    `GameObject.AddComponent<TraceAndOptimize>()` works normally (Unity's component instantiation
    does not go through the public C# constructor path), so `AAOIntegrator.cs` MUST add it via
    `AddComponent<T>()`, never `new T()`.
  - Runtime assembly name (for asmdef `references`): `com.anatawa12.avatar-optimizer.runtime`.
  - AAO's own runtime asmdef declares `precompiledReferences` on `VRC.Dynamics.dll`,
    `VRC.SDK3.Dynamics.PhysBone.dll`, `VRCSDK3A.dll`, `VRCSDKBase.dll` — confirming PhysBone/PhysBone
    Collider types are NOT in a separate VPM package (no `com.vrchat.dynamics` package exists in the
    VPM index); they ship as a precompiled DLL (`VRC.SDK3.Dynamics.PhysBone.dll`, Unity-assembly-name
    `VRC.SDK3.Dynamics.PhysBone`) bundled inside `com.vrchat.base` at
    `Runtime/VRCSDK/Plugins/VRC.SDK3.Dynamics.PhysBone.dll`. `PhysBoneValidator.cs` and its tests must
    reference the `VRC.SDK3.Dynamics.PhysBone` assembly, not `VRC.SDK3A`.

## 4. Quest-compatible shader landscape

- **Decision**: Registered **target** shaders (the Quest-side output of Shader Conversion Rules,
  FR-005/FR-006/FR-020) MUST be drawn only from VRChat's own **`VRChat/Mobile/*`** shader family —
  confirmed members include `VRChat/Mobile/Toon Lit` (long-standing baseline, matches the spec's
  original example) and the newer `VRChat/Mobile/Toon Standard` (introduced VRChat SDK 3.8.1, now
  positioned as VRChat's flagship recommended mobile/Quest shader).
- **Rationale**: VRChat **hard-restricts avatar shaders on Android/Quest to its own bundled
  `VRChat/Mobile/*` shaders** — third-party/custom shaders are rejected for avatars on that
  platform regardless of any "Lite"/"Quest" branding a shader author gives a variant. This is a
  platform-level allowlist for *avatars* specifically (distinct from VRChat *worlds*, where custom
  shaders ARE allowed on Quest). Concretely: **Poiyomi has no Quest avatar path** at all — the
  community-documented workaround is exactly what this tool automates (bake to a VRChat mobile
  shader). **lilToon is likewise not a valid Quest avatar *target*** despite its popularity as a PC
  source shader — no VRChat-sanctioned "lilToon Quest" avatar shader exists; only the official
  `VRChat/Mobile/*` set is permitted as an upload target.
- **Alternatives considered**: Allowing any shader with a "Quest"/"Mobile" label in its name as a
  registerable target — rejected, contradicted by VRChat's platform shader allowlist for avatars.
- **Source**: VRChat Creation docs, "Android Content Limitations" page; VRChat community
  documentation on Poiyomi/Quest incompatibility.
- **Confidence**: High on the restriction itself (consistent across official docs + community).
  Medium on "`Toon Standard` is now the flagship default" framing.
- **Design impact (correction applied)**: `contracts/extension-data-contracts.md` §1 now states the
  `TargetShader` field of `ShaderConversionRuleSet` MUST be one of VRChat's own `VRChat/Mobile/*`
  shaders — a rule asset naming any other shader as its target MUST fail load-time validation. The
  spec's own FR-006 example ("Poiyomi") is correct as written since it names Poiyomi only as an
  example *source* shader, never as a target.
- **Default picker suggestion**: default `ConversionSettings.TargetShaderRule` to
  `VRChat/Mobile/Toon Standard` rather than `Toon Lit`, given VRChat's current flagship-recommended
  status (medium confidence — worth a quick manual reconfirmation before locking the default).

## 5. VRChat Performance Rank — PhysBone thresholds (Quest/mobile-specific)

- **Decision**: Ship the following as the **default** `QuestCompatibilityRules.PhysBoneLimits` data
  asset (per `contracts/extension-data-contracts.md` §2), citing VRChat's Performance Ranking page:

  | Metric | Excellent | Good | Medium | Poor |
  |---|---|---|---|---|
  | PhysBone Components | 0 | 4 | 6 | 8 |
  | PhysBone Affected Transforms | 0 | 16 | 32 | 64 |
  | PhysBone Colliders | 0 | 4 | 8 | 16 |
  | PhysBone Collision Check Count | 0 | 16 | 32 | 64 |

  Additionally: a single PhysBone component has a **hard cap of 256 affected transforms** on Quest
  regardless of rank, and if a mobile avatar exceeds a hard PhysBone/Contact/Constraint cap, VRChat
  strips all components in that category at runtime even when the viewer has "Show Avatar" enabled
  — `PhysBoneValidator` (FR-016a) MUST surface this hard-cap case as a distinct, higher-severity
  finding from a mere rank downgrade, since the practical effect (silent stripping) differs from a
  performance-rank cosmetic label.
- **Rationale**: These are VRChat's own currently published Quest-specific thresholds; the PC
  thresholds are materially more permissive (e.g. 4/16/4/32 at Excellent) and must **not** be reused
  for Quest, confirming FR-016a's requirement for a Quest-specific (not shared) threshold table.
- **Source**: VRChat Creation docs, "Performance Ranks" page and "PhysBones" page (directly
  fetched).
- **Confidence**: High (official first-party source, directly fetched).
- **Re-verification trigger**: VRChat periodically revises its performance-ranking thresholds;
  `QuestCompatibilityRules.PhysBoneLimits.SourceCitation` (per the contract) must be updated
  whenever this table is refreshed, with no change required to `PhysBoneValidator`'s logic itself.

## Open Verification Items Carried Into Tasks (not spec-blocking)

1. ~~Confirm AAO's exact `Trace And Optimize` C# class/type name~~ — **Resolved 2026-09-19**, see §3.
2. ~~Reconfirm the exact VRChat SDK3 - Avatars version number~~ — **Resolved 2026-09-19**, see §2
   (3.10.5, directly fetched from VRChat's own VPM index).
3. Reconfirm `VRChat/Mobile/Toon Standard` as the intended default target shader (§4) before
   locking `ConversionSettings` defaults — still based on aggregated search, not a primary source;
   lower risk since `Toon Lit` remains registered as an alternative either way.
