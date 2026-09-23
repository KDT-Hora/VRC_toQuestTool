# AI_speckit

[GitHub Spec Kit](https://github.com/github/spec-kit) を Claude Code 向けにセットアップし、コンテキスト消費を抑えるよう調整した Spec-Driven Development 環境です。

## これは何か

Spec Kit は「仕様 → 計画 → タスク → 実装」を段階的に進める開発フレームワークです。本リポジトリは `specify init --integration claude --script ps` で初期化済みで、各フェーズのコマンドは Claude Code の Skills として `.claude/skills/speckit-*/` に配置されています。

## ディレクトリ構成

```
.claude/skills/speckit-*/SKILL.md   Claude Code から呼び出す各フェーズのSkill定義
.specify/memory/constitution.md     プロジェクトの原則(v1.0.0で確立済み)
.specify/templates/                 spec / plan / tasks / checklist 生成用テンプレート
.specify/scripts/powershell/        各フェーズの前処理スクリプト(PowerShell版)
.specify/workflows/                 spec-kit 標準ワークフロー定義
```

## 使い方(Claude Code 内で)

1. `/speckit-constitution` — プロジェクトの原則を確立
2. `/speckit-specify` — 仕様書のベースラインを作成
3. `/speckit-clarify`(任意) — 計画前に曖昧な点を解消
4. `/speckit-plan` — 実装計画を作成
5. `/speckit-tasks` — 実行可能なタスク一覧を生成
6. `/speckit-analyze`(任意) — spec / plan / tasks 間の整合性チェック
7. `/speckit-checklist`(任意) — 要求の完全性・明確性チェックリストを生成
8. `/speckit-implement` — タスクを実行
9. `/speckit-converge` — 未実装分を検出してタスクに追記

## このセットアップで行ったコンテキスト最適化

標準の spec-kit テンプレートは、各 Skill の実行前後に拡張フック(`.specify/extensions.yml`)の存在確認ロジックがほぼ同一のまま約35行×2箇所ずつ重複していました。多くのプロジェクトでは拡張フックを使わないため、Skill を呼び出すたびに無駄なコンテキストを消費します。

- 全10個の `SKILL.md` で、フック確認ロジックの挙動(必須/任意フックの区別、`condition`付きフックのスキップ、パースエラー時の告知など)を一切変えずに1段落へ圧縮し、合計行数を 3120 → 約1845行(約41%削減)
- `/speckit-implement` に、独立した `[P]` タスクやユーザーストーリー単位のフェーズをサブエージェント(Claude Code の Task tool)へ委譲する指示を追加し、大規模な実装でもメインの会話コンテキストが肥大化しにくいようにした

## 必要なもの

- [Claude Code](https://claude.com/claude-code)
- spec-kit CLI 自体の再インストール/更新には [uv](https://github.com/astral-sh/uv)
  ```sh
  uvx --from git+https://github.com/github/spec-kit.git specify init --here --integration claude --script ps
  ```
