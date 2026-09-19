# Feature Specification: Quest Avatar Converter

**Feature Branch**: `001-quest-avatar-converter`

**Created**: 2026-09-19

**Status**: Draft

**Input**: User description: "VRChat_Quest対応アバター改変ツール_仕様整理.txt の内容に基づく機能仕様。PC platform 用に作られた VRChat アバターから、PC 版を一切変更せずに Quest/Android 向け派生アバターを Unity Editor 上で自動生成するツール。Shader変換・Texture統合(Material単位)・Texture最適化・AAO(Avatar Optimizer) Trace And Optimize追加を一連のワークフローとして自動化する。"

## Clarifications

### Session 2026-09-19

- Q: このツールがサポートすべきUnityエディター・VRChat SDK・AAOのバージョン範囲はどのように決めますか？ → A: 現行最新LTSのみを公式サポート対象とする（他バージョンは非保証）。
- Q: v1ではTexture統合（Atlas生成）をColor系のみ対応とし、Normal/Mask/Emissionは単一画像のリサイズのみにすべきか、すべての種類で統合をサポートすべきか？ → A: Color/Normal/Mask/Emissionの全種類で、同一Material内に複数Textureがあればその種類ごとに統合する。
- Q: MaterialのTextureにUV Scale/Offset（タイリング）が設定されている場合、v1ではUVを正確に再マッピングすべきか、標準UV(0-1)を仮定して警告のみに留めスコープ外とすべきか？ → A: タイリングを検出した場合は警告のみ表示し、正確なUV再マッピングはv1のスコープ外とする。
- Q: 「実際に参照されているAsset」の範囲に、Animator/Animation Clipがランタイムに切り替えるMaterial/Textureもv1で追跡対象に含めるべきか、変換時点のRenderer.sharedMaterial（静的状態）のみを対象とすべきか？ → A: 静的参照（変換時点のRenderer.sharedMaterial）のみを対象とし、Animation経由のMaterial/Texture切り替えの追跡はv1スコープ外とする。
- Q: Shader Property Mapping（変換元/変換先ShaderのProperty対応付け）はv1ではどのように提供すべきか？ → A: 代表的なShaderペアのマッピングを内蔵データとして同梱し、ユーザーがマッピングを作成・編集できるUIはv1スコープ外とする。未対応のShaderペアは未対応として明示的にエラー表示する。
- Q: PhysBoneやContactなどVRChat固有のComponentについて、Quest Compatibility CheckではFR-016の汎用検出だけで充分か、v1からPhysBone固有の定量的な制限検証（Component数・Collider数など）が必要か？ → A: v1からPhysBone専用の定量検証（Component数・Collider数などをVRChatのQuest Performance Rank基準と照らして検証）を実装する。
- Q: 生成されるQuest Textureの出力形式・アルファチャンネルはどうすべきか？ → A: 常にPNGで出力し、元Textureが使用するアルファチャンネルは保持する（ロスレス圧縮PNGに統一。ファイルサイズ縮小はUnity Import設定側のCompressionに委ねる）。
- Q: FR-022の「既存Quest出力を上書き」を実行する際、ユーザーの明示的な確認を必須とすべきか、確認なしで自動的に上書きしてよいか？ → A: 既存のQuest出力が検出された場合は常に確認ダイアログを表示し、ユーザーが明示的に承認するまで上書きを実行しない。
- Q: Quest版の出力先フォルダはどこで決まるか？ → A: Assetフォルダ直下に固定の「Quest対応化」フォルダを用意し、その中に変換対象アバターごとのサブフォルダを自動作成する（ユーザーが任意パスを毎回指定する方式ではない）。
- Q: GPU InstancingはQuest用Materialに対してデフォルトで自動有効化すべきか、ユーザーが明示的に有効化するまで無効とすべきか？ → A: 生成されるQuest Materialに対してデフォルトでGPU Instancingを自動的に有効化する（Shaderごとの設定方法の違いに対応する形で）。

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate a working Quest derivative from a PC avatar (Priority: P1)

An avatar creator has a PC-platform-ready VRChat avatar in their Unity project. They select that
avatar in the tool, pick a Quest-compatible target shader, and run a single "Generate" action.
The tool produces a placed, working Quest-compatible copy of the avatar — with its own materials
and textures — while leaving the original PC avatar and its assets completely untouched.

**Why this priority**: This is the entire reason the tool exists. Without this flow working
end-to-end, nothing else in the tool has value. Everything else (preview, compatibility checks,
performance reporting) supports or refines this core outcome.

**Independent Test**: Can be fully tested by pointing the tool at any PC avatar prefab that has at
least one SkinnedMeshRenderer with a Material and Texture, running Generate, and confirming (a) a
new Quest avatar exists in the scene/output folder, (b) it renders using Quest-appropriate
materials, and (c) the original PC avatar's assets are unchanged on disk.

**Acceptance Scenarios**:

1. **Given** a PC avatar with one Material that references one Texture, **When** the user selects
   a target shader and runs Generate, **Then** a new Quest avatar prefab is created in the Quest
   output folder, its renderer references a newly generated Quest Material using the selected
   shader, and the original PC Material/Texture assets are byte-for-byte unchanged.
2. **Given** a PC avatar whose Material references multiple source Textures, **When** the user
   runs Generate, **Then** the tool produces a single merged Quest Texture for that Material that
   preserves each source image's aspect ratio, and the Quest Material's texture property points at
   that merged Texture.
3. **Given** a PC avatar with a Texture whose longest edge exceeds the configured maximum size,
   **When** the user runs Generate, **Then** the generated Quest Texture's longest edge is resized
   down to the configured maximum while preserving aspect ratio, and the source PC Texture file is
   unchanged.
4. **Given** AAO (Avatar Optimizer) is installed in the project, **When** generation completes,
   **Then** the Quest avatar's root object has an AAO "Trace And Optimize" component attached.
5. **Given** AAO is NOT installed in the project, **When** the user attempts to run Generate,
   **Then** the tool stops before producing a partial/broken result and shows a clear message
   explaining that AAO is required and how to obtain it.

---

### User Story 2 - Configure conversion settings before generating (Priority: P2)

Before generating, the avatar creator wants control over key conversion choices: which Quest
shader to target, where the Quest avatar is placed relative to the PC avatar, the maximum texture
size, and which optional steps (texture merging, resizing, AAO component) are applied.

**Why this priority**: Different avatars and different target shaders need different settings;
without configurability the tool only serves the single default case, but this is not required
for the very first successful conversion (P1 can run on defaults).

**Independent Test**: Can be tested independently by changing each setting (target shader, offset
position, max texture size, toggles) one at a time and confirming the generated output reflects
the changed setting, without needing to validate the full P1 flow's correctness in detail.

**Acceptance Scenarios**:

1. **Given** the tool's settings panel, **When** the user selects a different registered
   Quest-compatible shader than the default, **Then** all generated Quest Materials use the newly
   selected shader and its corresponding property mapping.
2. **Given** the tool's settings panel, **When** the user sets a custom placement offset (X/Y/Z),
   **Then** the generated Quest avatar is placed in the scene at the PC avatar's position plus that
   offset.
3. **Given** the tool's settings panel, **When** the user changes the maximum texture size, **Then**
   subsequently generated Quest Textures are constrained to that new maximum on their longest edge.
4. **Given** the user disables the "Merge Textures" option, **When** Generate runs, **Then** the
   tool does not perform texture merging (behavior for the resulting single-texture-per-property
   case follows the same resize/optimization rules as merged output).

---

### User Story 3 - Review results before and after generating (Priority: P3)

Before committing to a full generation, the avatar creator wants to preview how a Material's
textures will be merged and resized. After generation, they want a report of the Quest avatar's
performance-relevant metrics and any Quest-incompatible components found, so they can judge
whether further manual adjustment is needed.

**Why this priority**: This improves trust and iteration speed but is not required for the tool to
deliver its core value the first time; a user can inspect generated assets manually in its absence.

**Independent Test**: Can be tested independently by opening the preview for a single Material
before generating (no Generate action needed) and, separately, by running Generate and confirming
a report/checklist of metrics and flagged components is shown.

**Acceptance Scenarios**:

1. **Given** a PC avatar is selected, **When** the user opens the preview for one of its Materials,
   **Then** the tool shows the source textures, the resulting merged texture, and the final
   resolution, without creating any assets on disk.
2. **Given** generation has completed, **When** the user views the result, **Then** the tool
   displays avatar performance metrics (at minimum triangle count, material count, skinned mesh
   renderer count, bone count, texture count, estimated texture memory).
3. **Given** generation has completed and the avatar contains a component known to be restricted or
   unsupported on Quest, **When** the user views the compatibility check results, **Then** that
   specific object is listed individually (not only as part of a summary count).

---

### Edge Cases

- What happens when a PC Material has no Texture assigned to any of its properties (a flat-color
  Material)? The tool must still generate a Quest Material with the mapped color properties, and
  must not fail merely because there was nothing to merge.
- What happens when two different PC Materials reference the exact same source Texture? Each
  Material's merge output is still computed per-Material (per the "merge is scoped per Material,
  not globally" rule), so the same source Texture may legitimately appear in more than one
  generated Quest Texture.
- What happens when the same PC Material is referenced by more than one Renderer on the avatar?
  All Quest-side renderers that referenced that shared PC Material must reference the same single
  generated Quest Material, not one copy per renderer (see FR-011).
- What happens when a source Texture's UV is scaled/offset (tiled) on the Material rather than
  using the default 0-1 range? For v1, the system assumes a standard 0-1 UV range for merge/atlas
  placement; when it detects a non-default UV Scale/Offset on a Material being merged, it MUST
  surface a warning in the conversion report that the merged result may not be visually accurate,
  rather than silently producing an incorrect result or failing. Accurate UV remapping for tiled
  textures is out of scope for v1 (see Assumptions).
- What happens when the selected target shader has no mapping rule registered for a Property that
  the source Material uses? The tool must surface this as a warning in the conversion report
  rather than silently dropping the value.
- What happens when the user runs Generate again for an avatar that was already converted in a
  prior run? Per FR-022, the tool overwrites the prior Quest output in place at the same output
  location.
- What happens when a source Texture is already smaller than the configured maximum size? The tool
  must leave its resolution unchanged rather than upscaling it.
- What happens when the PC avatar itself already lives under a folder named `Quest/` or otherwise
  collides with the intended output path? The tool must not silently overwrite unrelated assets;
  it must detect the collision and inform the user.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST NOT modify, move, rename, or overwrite any PC-platform asset (source
  avatar prefab/hierarchy, Materials, Textures, Animations, Animator Controllers, VRChat avatar
  components) as part of any conversion operation, including re-runs.
- **FR-002**: The system MUST generate the Quest avatar and its supporting assets (Materials,
  Textures) as new assets under a dedicated Quest output location, structurally separated from the
  PC-platform assets (e.g. distinct Avatar / Materials / Textures locations). The output location
  MUST be a single fixed top-level convention folder directly under the project's `Assets` folder
  (a "Quest-converted" root), with the system automatically creating a dedicated subfolder per
  source avatar underneath it — the user does not need to browse to or type an arbitrary output
  path for each run.
- **FR-003**: The system MUST determine which assets to duplicate by traversing the PC avatar's
  static reference graph at conversion time (each Renderer's currently assigned `sharedMaterial`(s)
  → Material → Texture) rather than copying an entire project folder. Material/Texture references
  that only exist as runtime swaps driven by an Animator Controller or Animation Clip (e.g. outfit
  or blend-shape-driven material changes) are explicitly out of scope for discovery in v1 (see
  Assumptions); VRChat avatar component references needed for the avatar to function (e.g. the VRC
  Avatar Descriptor itself) are still followed.
- **FR-004**: The system MUST allow the user to choose where the generated Quest avatar is placed
  in the scene, including a configurable positional offset from the PC avatar.
- **FR-005**: The system MUST let the user select the target Quest-compatible shader, from a
  registered list of supported shaders, before generating.
- **FR-006**: The system MUST convert each referenced PC Material into a corresponding Quest
  Material using the selected target shader, mapping each source shader Property (texture, color,
  and scalar values) to the corresponding target shader Property according to a defined mapping for
  that shader pair. For v1, these mappings ship as built-in data covering a representative set of
  common source shaders (e.g. Standard-family, Poiyomi) paired with the supported Quest target
  shaders (e.g. VRChat/Mobile/Toon Lit); a user-facing UI to author or edit mapping rules is out of
  scope for v1 (see Assumptions). When a Material's source shader has no registered mapping to the
  selected target shader, the system MUST report that Material as explicitly unsupported/failed for
  conversion rather than guessing a mapping or silently skipping it.
- **FR-007**: When a PC Material references more than one source Texture, the system MUST merge
  those textures into a single combined Texture scoped to that Material (not merged globally across
  the whole avatar), preserving each source image's aspect ratio in the resulting layout.
- **FR-008**: The system MUST classify source textures by type (at minimum: Color/Albedo, Normal
  Map, Mask [Metallic/Roughness/Occlusion/etc.], Emission) and MUST NOT combine textures of
  different types into one merged image via plain alpha compositing; each texture type consumed by
  the target shader MUST be merged into its own separate output image. This merging (per FR-007)
  MUST be supported for all four classified types, not only Color/Albedo — a Material with, e.g.,
  multiple Normal Maps or multiple Mask textures MUST have those merged into their own atlas the
  same way multiple Color textures are.
- **FR-009**: The system MUST resize each generated Quest Texture so its longest edge does not
  exceed a configurable maximum size (default: 1024px), preserving aspect ratio, and MUST leave the
  source PC Texture unmodified. A source texture already at or below the maximum MUST NOT be
  upscaled. Every generated Quest Texture (merged or single-source) MUST be written in a lossless
  PNG format, preserving an alpha channel whenever the corresponding source Texture(s) used one;
  further file-size reduction is expected to happen via the generated Texture's own Unity Import
  Settings (compression), not by choosing a lossy output format.
- **FR-010**: The system MUST update Renderer components on the generated Quest avatar to reference
  only the newly generated Quest Materials, never the original PC Materials.
- **FR-011**: If the same PC Material is referenced by multiple Renderers on the avatar, the system
  MUST generate a single shared Quest Material for it and reference that same generated Material
  from every corresponding Quest-side Renderer, rather than generating a separate copy per
  Renderer.
- **FR-012**: The system MUST add an AAO "Trace And Optimize" component to the root of the
  generated Quest avatar as a standard part of generation.
- **FR-013**: The system MUST detect whether AAO is installed in the project before generation
  proceeds, and MUST halt with a clear, actionable message (not a silent no-op and not an unhandled
  error) when AAO is not present.
- **FR-014**: The system MUST maintain a mapping between each source PC asset (Material, Texture)
  and its corresponding generated Quest asset for a given Quest output, so that the relationship
  between source and generated assets can be inspected and reused across the pipeline (e.g. shared
  Material reuse per FR-011) rather than only during a single generation pass.
- **FR-015**: The system MUST report avatar performance-relevant metrics for the generated Quest
  avatar after generation, at minimum: triangle count, Material count, Skinned Mesh Renderer count,
  bone count, Texture count, and estimated Texture memory.
- **FR-016**: The system MUST check the generated Quest avatar for components/objects known to be
  unsupported or restricted on the Quest/mobile VRChat platform (e.g. certain Camera, AudioSource,
  ParticleSystem, Constraint configurations) and MUST list each flagged object individually, not
  only as an aggregate count.
- **FR-016a**: For VRChat PhysBone and PhysBone Collider components specifically, the compatibility
  check MUST go beyond generic presence-flagging: it MUST count PhysBone components, PhysBone
  Collider components, and PhysBone-affected transforms on the Quest avatar and compare them
  against VRChat's published Quest Performance Rank thresholds for those quantities, flagging the
  avatar's resulting rank/violations individually rather than only a pass/fail summary. These
  threshold values MUST be sourced from the externalized rule data described in Constitution
  Principle III, not hard-coded, since VRChat's published limits change over time.
- **FR-017**: The system MUST let the user preview, for at least one Material at a time, the
  resulting merged Texture(s) and final resolution before running full generation, without writing
  any asset to disk as part of that preview.
- **FR-018**: The system MUST let the user independently enable or disable each major optional
  generation step (duplicate avatar, merge textures, resize textures, add AAO component) before
  generating.
- **FR-019**: The system MUST produce, for each generation run, a report/log of what was created,
  skipped, or flagged (warnings/errors), sufficient for the user to understand the outcome without
  manually inspecting each generated asset.
- **FR-020**: Quest-compatible shaders and their per-shader Property mapping rules MUST be defined
  so that a new source/target shader pair can be registered without modifying the core conversion
  pipeline logic.
- **FR-020a**: The system MUST enable GPU Instancing on every generated Quest Material by default,
  using whichever mechanism the target shader requires to do so, without requiring the user to
  enable it manually per Material.
- **FR-021**: When the compatibility check (FR-016) flags an unsupported/restricted component, the
  system MUST leave the component in place by default and MUST let the user decide, per flagged
  item, whether to remove it, keep it, or leave it for later manual review — the tool MUST NOT
  remove any flagged component without an explicit per-item user decision.
- **FR-022**: When the user runs generation for a PC avatar that already has previously generated
  Quest output, the system MUST overwrite that prior Quest output in place (replacing the
  previously generated Avatar/Materials/Textures at the same output location) rather than creating
  a separate versioned copy or silently leaving stale assets behind. Every time prior Quest output
  is detected at the target location, the system MUST show an explicit confirmation dialog and
  MUST NOT perform the overwrite until the user explicitly approves it in that dialog.
- **FR-023**: The system MUST duplicate the PC avatar into the Quest avatar as a fully independent
  Prefab copy (not a Prefab Variant of the PC prefab), so that subsequent edits to the PC prefab do
  not propagate to the Quest avatar and the Quest prefab can be freely modified and uploaded to
  VRChat independently of the PC prefab.

### Key Entities

- **PC Avatar**: The user-selected source avatar (a Prefab/Hierarchy instance) built for the PC
  platform, with Renderers referencing PC Materials. Never modified by this feature.
- **Quest Avatar**: The generated derivative avatar placed in the scene/output, structurally
  mirroring the PC Avatar but referencing only generated Quest Materials/Textures.
- **PC Material / Quest Material**: A shader plus its Property values (textures, colors, scalars).
  Each processed PC Material has at most one corresponding generated Quest Material, shared across
  all Renderers that reference the same source Material.
- **PC Texture / Quest Texture**: An image asset. A Quest Texture may be a direct resized copy of
  one PC Texture, or a merged image combining multiple PC Textures of the same classified type that
  belong to one Material.
- **Shader Conversion Rule**: A registered definition pairing a source shader and a target Quest
  shader with the Property mapping between them, used by Material conversion.
- **Conversion Context / Report**: The run-scoped record of the source→generated asset mapping,
  the settings used, and the resulting log (created/skipped/warned/failed items), plus the
  performance metrics and compatibility findings produced for that run.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can go from selecting a PC avatar to having a placed, working Quest-compatible
  derivative avatar in the scene using a single guided "Generate" action, without manually creating
  any Material, Texture, or output folder by hand.
- **SC-002**: After any generation run, every one of the original PC avatar's Materials, Textures,
  and Prefab assets remains unchanged, verifiable by comparing asset content before and after the
  run.
- **SC-003**: For a Material with multiple source Textures, the merged Quest Texture visibly
  preserves each source image's proportions (no unintended stretching), confirmed by visual
  comparison against the source images.
- **SC-004**: Every generated Quest Texture's longest edge is at or under the currently configured
  maximum size, with no exceptions.
- **SC-005**: From the generation report alone, a user can identify the Quest avatar's triangle,
  Material, and Texture counts, and every individually flagged Quest-incompatible object, without
  opening any generated asset directly.
- **SC-006**: Re-running generation for the same PC avatar never leaves the user needing to
  manually locate and delete duplicate or orphaned previously-generated assets to get back to a
  clean, correct Quest output.

## Assumptions

- The tool is a Unity Editor-only workflow (no runtime/build-step component); all generation
  happens through in-Editor user action.
- One PC avatar is processed per generation run (batch conversion of multiple avatars at once is
  out of scope for this feature).
- One target shader is selected and applied to all Materials generated in a given run (mixed
  target shaders within a single run are out of scope for this feature).
- Default maximum Quest Texture size is 1024px on the longest edge, matching the source document's
  stated default; this is user-configurable per FR-009/US2.
- Default texture-merge scope is per-Material ("Material Based"), matching the source document;
  merging textures across multiple Materials into one shared atlas is out of scope for this
  feature.
- Texture merging assumes source Textures use the standard 0-1 UV range; accurately remapping UVs
  for Textures with non-default UV Scale/Offset (tiling) is out of scope for v1 — the tool warns
  instead (see Edge Cases).
- Asset discovery is limited to each Renderer's static `sharedMaterial` reference at conversion
  time; Material/Texture swaps driven at runtime by an Animator Controller or Animation Clip are
  not tracked or converted in v1.
- Shader Property Mapping rules (FR-006/FR-020) ship as built-in data covering a representative set
  of common shader pairs; a user-facing UI for authoring or editing custom mapping rules is out of
  scope for v1.
- The Quest output root is a single fixed convention folder under `Assets` (not a per-run,
  user-browsed path); each converted avatar gets its own automatically created subfolder under it.
- Full Unity Undo-stack integration for the generate action, and an in-tool AAO-installation
  action (vs. pointing the user to install it themselves), are treated as enhancements beyond this
  feature's initial scope; FR-013 only requires clear detection and messaging when AAO is absent.
- Officially supported versions are the current latest Unity LTS, the current latest VRChat SDK3,
  and the current latest AAO release only (see Clarifications). Older versions are not guaranteed
  to work; exact version numbers are tracked in the implementation plan since they change over
  time and are not a product-scope decision.
