# VRC_toQuestTool

VRChatアバターをPC版を一切変更せずにQuest/Android向け派生アバターへ自動変換するUnity Editorツール。

## これは何か

Unity Editor上で動作するアバター改変支援ツールです。PCプラットフォーム用に作られたVRChatアバターを入力すると、
Shader変換・Texture統合（Material単位）・Textureサイズ最適化・AAO (Avatar Optimizer) Trace And Optimize追加までを
一連のワークフローとして自動化し、Quest対応の派生アバターを別Assetとして生成します。元のPC版アバターとその
参照Assetは一切変更しません。

## リポジトリ構成

```
VRChat_Quest対応アバター改変ツール_仕様整理.txt   要件整理の原本メモ（企画意図・未決定事項の記録）

AI_speckit-main/                                Spec-Driven Development 環境 (Spec Kit for Claude Code)
├─ .specify/memory/constitution.md              プロジェクトの原則（憲法）
├─ .claude/skills/speckit-*/                    各開発フェーズのSkill定義
└─ specs/001-quest-avatar-converter/
   ├─ spec.md                                   機能仕様書（要件・受け入れ基準・クラリファイ結果）
   ├─ plan.md                                   実装計画（技術スタック・Constitution Check・構成）
   ├─ research.md                               技術調査（対応バージョン・Shader事情・PhysBone基準等）
   ├─ data-model.md                             データモデル定義
   ├─ contracts/extension-data-contracts.md     拡張用データスキーマ契約
   ├─ quickstart.md                             手動検証シナリオ
   ├─ tasks.md                                  実装タスク一覧（進捗は [X] でマーク）
   └─ checklists/requirements.md                仕様品質チェックリスト

UnityProject/                                   ツール本体を開発・動作確認するためのUnityプロジェクト
├─ Packages/com.vrc-rufu.quest-avatar-converter/  ツール本体（UPMパッケージ）
├─ Scripts/fetch-vrchat-sdk.ps1                 VRChat SDK3導入スクリプト（初回セットアップ用）
└─ README.md                                    Unityプロジェクトのセットアップ・テスト実行手順

UnityPackage/                                   ツール本体の配布用.unitypackage
├─ QuestAvatarConverter-0.1.0.unitypackage      インポートするだけで導入できるツール本体（VRChat SDK3/AAOは同梱せず、別途導入が前提）
└─ README.md                                    インポート手順・前提条件
```

## 開発の進め方（Spec-Driven Development）

本プロジェクトは [GitHub Spec Kit](https://github.com/github/spec-kit) ベースの Spec-Driven Development で進めます。

1. `/speckit-constitution` — プロジェクトの原則を確立 ✅ 完了 (v1.0.0)
2. `/speckit-specify` — 仕様書のベースラインを作成 ✅ 完了
3. `/speckit-clarify` — 計画前に曖昧な点を解消 ✅ 完了（計13件の論点を解消）
4. `/speckit-plan` — 実装計画を作成 ✅ 完了
5. `/speckit-tasks` — 実行可能なタスク一覧を生成 ✅ 完了（51タスク）
6. `/speckit-analyze` / `/speckit-checklist`（任意）— 整合性・完全性チェック（未実施）
7. `/speckit-implement` — タスクを実行 🔄 ほぼ完了（詳細は下記「現在の状態」）
8. `/speckit-converge` — 未実装分を検出してタスクに追記（未実施）

詳細な使い方は [`AI_speckit-main/README.md`](AI_speckit-main/README.md) を参照してください。

## ツール本体のセットアップ・使い方

Generateボタンを含むGUI（`QuestAvatarConverterWindow`）まで実装済みです。

**すでにVRChat SDK3・AAO導入済みのプロジェクトにツールだけ入れたい場合**は、
[`UnityPackage/QuestAvatarConverter-0.1.0.unitypackage`](UnityPackage/QuestAvatarConverter-0.1.0.unitypackage)
をインポートするだけで導入できます（詳細は [`UnityPackage/README.md`](UnityPackage/README.md)）。
以下は開発・動作確認用にリポジトリ全体をセットアップする手順です。

1. リポジトリをクローン
2. `UnityProject/Scripts/fetch-vrchat-sdk.ps1` をPowerShellで実行し、VRChat SDK3 (Base / Avatars) を
   ローカル導入（ライセンスの都合上リポジトリには同梱していません）
3. Unity Hub / Unity CLI で `UnityProject/`（Unity 2022.3.22f1推奨。詳細は
   [`AI_speckit-main/specs/001-quest-avatar-converter/research.md`](AI_speckit-main/specs/001-quest-avatar-converter/research.md) 参照）を開く
   - AAO (`com.anatawa12.avatar-optimizer`) と NDMF (`nadena.dev.ndmf`) はGit URL経由で自動解決されます
   - 解決に失敗する場合（`.csc.rsp.nullsafe`や`AvatarOptimizer.ruleset`関連のコンパイルエラー）は
     [`UnityProject/README.md`](UnityProject/README.md) のトラブルシューティング項目を参照
4. EditModeテストの実行方法・詳細は [`UnityProject/README.md`](UnityProject/README.md) を参照
5. Unity Editorのメニュー `Tools > VRC Rufu > Quest Avatar Converter` からツールウィンドウを開く
   - Source AvatarにPC版アバターのPrefabを指定し、必要に応じて設定（Target Shader / Placement Offset /
     Max Texture Size / Merge・Resize Textures / Add AAO Component）を調整して **Generate** を押すと、
     `Assets/QuestConverted/<アバター名>/` 以下にQuest対応アバターが生成されます
   - Generate後はウィンドウ内のReportパネルでパフォーマンス指標・PhysBone互換性・フラグ付きコンポーネント
     （個別にRemove/Keepを選択可能、選択しない限り何も削除されません）を確認できます
   - Previewパネルで、Generateを押さずに特定Materialの結合結果とTexture解像度を事前確認できます

⚠️ **既知の制約**：この実装は対話的なUnity Editorセッションで実際にGUIをクリックして検証されたことは
まだありません（ヘッドレスなCLI環境で、パイプライン本体を直接呼び出す形での動作確認のみ実施）。詳細は
[`tasks.md`](AI_speckit-main/specs/001-quest-avatar-converter/tasks.md) のT033/T038/T046の記録を参照してください。

## 現在の状態

- 機能仕様: [`spec.md`](AI_speckit-main/specs/001-quest-avatar-converter/spec.md)
- 実装計画: [`plan.md`](AI_speckit-main/specs/001-quest-avatar-converter/plan.md)
- タスク一覧・進捗: [`tasks.md`](AI_speckit-main/specs/001-quest-avatar-converter/tasks.md)
  （51タスク中50タスク完了。残るT050「クリーンなプロジェクトでの対話的な全シナリオ確認」のみ未実施。
  詳細は上記の既知の制約を参照）
- プロジェクトの原則: [`constitution.md`](AI_speckit-main/.specify/memory/constitution.md)
