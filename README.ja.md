[English](README.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja.md)

# Alias Cockpit

メールエイリアスの生成、マーキング、保存、インポート、エクスポート、および必要に応じた同期を行う、ローカルファーストの Windows デスクトップアプリです。

![最終コミット](https://img.shields.io/github/last-commit/NextWeb4/alias-cockpit?style=flat-square)
![リポジトリサイズ](https://img.shields.io/github/repo-size/NextWeb4/alias-cockpit?style=flat-square)
![GitHub Stars](https://img.shields.io/github/stars/NextWeb4/alias-cockpit?style=flat-square)
![C# と .NET 8](https://img.shields.io/badge/C%23-.NET%208-512BD4?style=flat-square&logo=dotnet&logoColor=white)

## 現在の範囲

Alias Cockpit は開発中の WinUI 3/.NET アプリです。現在のメイン画面はオフラインの Gmail/Outlook エイリアス展開ツールで、入力履歴、サイト・用途・色のマーカー、マーク済み/未マークの絞り込み、コピー操作を備えています。リポジトリには次の実装も含まれます。

- エイリアス生成、エントロピー推定、CSV インポート/エクスポートの dry-run、監査イベント、tombstone、Provider capability モデル
- エイリアス、保存済みアドレス、Provider アカウント、監査データ用のローカル SQLite リポジトリ
- Windows Credential Manager によるシークレット保存
- SimpleLogin/addy.io の mock adapter と HTTP adapter の基盤
- xUnit による単体、ストレス、ViewModel、インフラストラクチャテスト
- 生成、CSV、SQLite の再現可能なベンチマーク
- folder publish、portable ZIP、MSI、setup EXE、GitHub Release 用ツール

暗号化同期、高度な Provider 同期、完全な UI 自動化は未完成です。通常の起動時に実 Provider API を呼び出すことはありません。

## 動作要件

- Windows 10 version 2004（`10.0.19041.0`）以降
- Git 管理対象外のローカル `.tools\dotnet` ツールチェーン。既存ドキュメントでは、`.slnx` の編成に .NET 10 SDK、製品プロジェクトに .NET 8 Runtime を使用しています。
- UI smoke test にはデスクトップセッションが必要です。

## 実行

```powershell
.\.tools\dotnet\dotnet.exe run --project src\AliasCockpit.App\AliasCockpit.App.csproj
```

ローカルメタデータの保存先は次のとおりです。

```text
%LocalAppData%\AliasCockpit\aliases.sqlite
```

この開発用データベースは暗号化されていません。エイリアスメタデータ、入力履歴、マーカー、監査データ、Provider の `secret_ref` は保存できますが、Provider token や API secret を保存してはいけません。

## ビルド、テスト、フォーマット

```powershell
.\.tools\dotnet\dotnet.exe build AliasCockpit.slnx -v minimal
.\.tools\dotnet\dotnet.exe test AliasCockpit.slnx -v minimal
.\.tools\dotnet\dotnet.exe run --project benchmarks\AliasCockpit.Benchmarks\AliasCockpit.Benchmarks.csproj -c Release
.\.tools\dotnet\dotnet.exe format AliasCockpit.slnx --verify-no-changes --verbosity minimal
```

既存ドキュメントによると、`dotnet format` は非ブロッキングの workspace-load warning を出す場合があります。終了コードと build/test の結果を合わせて判定してください。

## リリースパッケージ

配布物を作る際は、完全なリリース検証を実行します。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-release.ps1
```

このスクリプトは build、test、benchmark、format、publish を実行し、監査済みの未使用 WinAppSDK ファイルを削減します。その後 portable ZIP、MSI、setup EXE を再構築して内容を検査し、publish 版と portable 版の起動 smoke test、および基本 UI smoke test を行います。

個別のコマンドも利用できます。

```powershell
.\.tools\dotnet\dotnet.exe publish src\AliasCockpit.App\AliasCockpit.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -v minimal
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\prune-publish.ps1 -PublishDir src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-msi.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-setup-exe.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-github-release.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\clean-build-cache.ps1 -Artifacts
```

想定される成果物：

| 成果物 | 用途 |
| --- | --- |
| `src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish\AliasCockpit.App.exe` | 完全な folder publish 内の実行ファイル。インストーラーや単一ファイル bundle ではありません |
| `artifacts\AliasCockpit-win-x64-portable.zip` | publish ディレクトリ一式を含む portable パッケージ |
| `artifacts\AliasCockpit-win-x64.msi` | WiX によるマシン単位の MSI。インストール時に昇格が必要な場合があります |
| `artifacts\AliasCockpit-win-x64-setup.exe` | MSI を内包する WiX Burn setup |

publish された EXE は、隣接する WinUI/.NET Runtime ファイルと一緒に保持してください。WiX はビルド時だけ使用します。`scripts\publish-github-release.ps1` は `NextWeb4/alias-cockpit` と `v1.0.0` を対象とし、`GITHUB_TOKEN`、`GH_TOKEN`、Git Credential Manager、または Codex integration helper から実行時だけ認証情報を取得します。

## プロジェクト構成

| パス | 役割 |
| --- | --- |
| `src/AliasCockpit.App/` | WinUI 3 shell、メイン画面、ViewModel、クリップボード、デスクトップ連携 |
| `src/AliasCockpit.Core/` | UI 非依存のエイリアス、生成、CSV、監査、Provider、secret、安全、展開ツールの契約 |
| `src/AliasCockpit.Infrastructure/` | SQLite、Windows Credential Manager、Provider adapter |
| `tests/AliasCockpit.App.Tests/` | WinUI ウィンドウを起動しない ViewModel テスト |
| `tests/AliasCockpit.Core.Tests/` | ドメイン動作の単体・ストレステスト |
| `tests/AliasCockpit.Infrastructure.Tests/` | SQLite、credential store、Provider adapter の統合テスト |
| `benchmarks/AliasCockpit.Benchmarks/` | 生成、CSV dry-run、SQLite のベースライン |
| `docs/` | 調査、アーキテクチャ判断、セキュリティモデル、リリースノート |
| `scripts/` | branding、publish、package、cleanup、release、smoke test の自動化 |

## データとセキュリティの境界

- Gmail のドットエイリアスは Gmail/Googlemail アドレスだけが対象です。Google Workspace の独自ドメインには自動適用しません。
- `+tag` エイリアスを受け付けない外部フォームもあります。
- Provider token は `WindowsCredentialManagerSecretStore` に保存し、SQLite には secret-key モデルが生成した参照だけを保存します。
- サイト、用途、タグ、マーカー、保存アドレスの各フィールドに、パスワード、token、復旧コードなどの機密情報を書かないでください。
- Provider alias の一括 disable/delete は実行前に plan を作成し、delete には明示的な確認と監査記録が必要です。
- HTTP adapter は明示的に呼び出すと key 検証と対応操作を行えますが、mock adapter や fake HTTP handler は実アカウントでの E2E 動作を保証しません。

## 作者

- HaoXiang Huang
- [didadida1688@gmail.com](mailto:didadida1688@gmail.com)
- <https://nextweb4.github.io/>

アイコンの原本は `src/AliasCockpit.App/Assets/AppIcon.ico` です。アプリ EXE、WinUI ウィンドウ、スタートメニューのショートカット、アプリ一覧、setup bundle で同じアイコンを使用します。

## ライセンス

監査対象のリポジトリには `LICENSE` ファイルがありませんでした。所有者が別途利用条件を示すまでは、オープンソースライセンスが付与されているものとして扱わないでください。
