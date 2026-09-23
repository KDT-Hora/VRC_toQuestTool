# UnityPackage

`QuestAvatarConverter-0.1.0.unitypackage` — Quest Avatar Converterツール本体（
`com.vrc-rufu.quest-avatar-converter`）を、Unityの `Assets > Import Package > Custom Package...`
からインポートするだけで導入できる配布用パッケージです。

`UnityProject/Packages/com.vrc-rufu.quest-avatar-converter/` から
`AssetDatabase.ExportPackage(..., ExportPackageOptions.Recurse)` でエクスポートしたもので、各Assetの
パス（`Packages/com.vrc-rufu.quest-avatar-converter/...`）をそのまま保持しています。そのため
インポート先プロジェクトでも `Assets/` 直下ではなく `Packages/` 配下に、通常どおりのembedded UPM
package として復元されます。

## 前提条件（このパッケージには含まれません）

VRChatアバター向けツールである以上、インポート先のUnityプロジェクトに以下が**あらかじめ導入済み**で
ある必要があります。このUnityPackageをインポートするだけでは導入されません。

1. **VRChat SDK3 - Base / Avatars**（バージョン3.10.5推奨）
   ライセンス上の理由で本パッケージへの同梱・再配布ができません。[VRChat Creator Companion (VCC)](https://vcc.docs.vrchat.com/)
   等で別途導入してください。
2. **Avatar Optimizer (AAO)**（`com.anatawa12.avatar-optimizer`、バージョン1.9.19推奨）
   MITライセンスですが、VCCやUPM経由で別途導入したバージョンと重複・競合するのを避けるため同梱していません。
   [VCC Community Alliance のリスティング](https://vpm.anatawa12.com/) 等から導入してください。

上記2つが未導入のプロジェクトにこのUnityPackageだけをインポートすると、`Anatawa12.AvatarOptimizer.TraceAndOptimize`
や `VRC.SDK3.Avatars.Components.VRCAvatarDescriptor` 等の型が見つからず**コンパイルエラーになります**。
先に1・2を導入してからこのUnityPackageをインポートしてください。

## インポート後

Unity Editorのメニュー `Tools > VRC Rufu > Quest Avatar Converter` からツールウィンドウを開けます。
使い方はリポジトリルートの [`README.md`](../README.md) を参照してください。

## 再生成方法

このファイルが古くなった場合は、`UnityProject` をUnity Editorで開いた状態で以下を実行すると
再エクスポートできます（`unity-cli` スキル経由、または通常のEditor UIから
`Assets > Export Package...` で `Packages/com.vrc-rufu.quest-avatar-converter` を選択し
"Include dependencies" を有効にしてエクスポートしても同じ結果になります）。

```powershell
unity run <UnityProjectのパス> --editor-version 2022.3.22f1 -- -executeMethod <一時的なExporterクラス>.Run
```
