# UnityProject

Local Unity 2022.3.22f1 dev/test project for the Quest Avatar Converter package
(`Packages/com.vrc-rufu.quest-avatar-converter/`). See
`specs/001-quest-avatar-converter/` in the repo root for the spec/plan/tasks driving this
implementation.

## First-time setup after cloning

VRChat's SDK3 packages (`com.vrchat.base`, `com.vrchat.avatars`) are proprietary
(https://hello.vrchat.com/legal/sdk) and are therefore **not** committed to this repo (see
`.gitignore`). Fetch them locally before opening the project:

```powershell
./Scripts/fetch-vrchat-sdk.ps1
```

This downloads and extracts the pinned version (default `3.10.5`, matching
`specs/001-quest-avatar-converter/research.md` §2) as embedded local packages under `Packages/`.

Avatar Optimizer (`com.anatawa12.avatar-optimizer`) and NDMF (`nadena.dev.ndmf`) are open-source
(MIT) and resolve automatically via their git URL entries in `Packages/manifest.json` the first
time the project opens (or via `unity test` / `unity run`) — no manual step needed for those.

## Running EditMode tests

```powershell
unity test . --editor-version 2022.3.22f1 --mode EditMode --report-format junit --output ./test-results.xml
```
