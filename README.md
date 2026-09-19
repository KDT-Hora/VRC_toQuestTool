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
   └─ checklists/requirements.md                仕様品質チェックリスト
```

## 開発の進め方（Spec-Driven Development）

本プロジェクトは [GitHub Spec Kit](https://github.com/github/spec-kit) ベースの Spec-Driven Development で進めます。

1. `/speckit-constitution` — プロジェクトの原則を確立 ✅ 完了 (v1.0.0)
2. `/speckit-specify` — 仕様書のベースラインを作成 ✅ 完了
3. `/speckit-clarify` — 計画前に曖昧な点を解消 ✅ 完了（計13件の論点を解消）
4. `/speckit-plan` — 実装計画を作成 ← 次のステップ
5. `/speckit-tasks` — 実行可能なタスク一覧を生成
6. `/speckit-analyze` / `/speckit-checklist`（任意）— 整合性・完全性チェック
7. `/speckit-implement` — タスクを実行
8. `/speckit-converge` — 未実装分を検出してタスクに追記

詳細な使い方は [`AI_speckit-main/README.md`](AI_speckit-main/README.md) を参照してください。

## 現在の状態

- 現在の機能仕様は [`AI_speckit-main/specs/001-quest-avatar-converter/spec.md`](AI_speckit-main/specs/001-quest-avatar-converter/spec.md) を参照。
- プロジェクトの原則は [`AI_speckit-main/.specify/memory/constitution.md`](AI_speckit-main/.specify/memory/constitution.md) を参照。
