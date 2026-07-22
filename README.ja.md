<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/English-0969da?style=flat-square" alt="English"></a>
  <a href="README.zh-CN.md"><img src="https://img.shields.io/badge/%E7%AE%80%E4%BD%93%E4%B8%AD%E6%96%87-c8102e?style=flat-square" alt="简体中文"></a>
  <a href="README.ja.md"><img src="https://img.shields.io/badge/%E6%97%A5%E6%9C%AC%E8%AA%9E-8250df?style=flat-square" alt="日本語"></a>
</p>

# Alias Cockpit

メールエイリアスの生成、マーキング、保存、インポート、エクスポート、および必要に応じた同期を行う、ローカルファーストの Windows デスクトップアプリです。

![最終コミット](https://img.shields.io/github/last-commit/NextWeb4/alias-cockpit?style=flat-square)
![リポジトリサイズ](https://img.shields.io/github/repo-size/NextWeb4/alias-cockpit?style=flat-square)
![GitHub スター](https://img.shields.io/github/stars/NextWeb4/alias-cockpit?style=flat-square)
![C# と .NET 8](https://img.shields.io/badge/C%23-.NET%208-512BD4?style=flat-square&logo=dotnet&logoColor=white)

## 現在の範囲

Alias Cockpit は開発中の WinUI 3/.NET アプリです。現在のメイン画面はオフラインの Gmail/Outlook エイリアス展開ツールで、入力履歴、サイト・用途・色のマーカー、マーク済み/未マークの絞り込み、コピー操作を備えています。リポジトリには次の実装も含まれます。

- エイリアス生成、エントロピー推定、CSV インポート/エクスポートのドライラン、監査イベント、削除記録（tombstone）、プロバイダー機能モデル
- エイリアス、保存済みアドレス、プロバイダーアカウント、監査データ用のローカル SQLite リポジトリ
- Windows Credential Manager によるシークレット保存
- SimpleLogin/addy.io のモックアダプターと HTTP アダプターの基盤
- xUnit による単体、ストレス、ViewModel、インフラストラクチャテスト
- 生成、CSV、SQLite の再現可能なベンチマーク
- フォルダー公開、ポータブル ZIP、MSI、セットアップ EXE、GitHub Release 用ツール

暗号化同期、高度なプロバイダー同期、完全な UI 自動化は未完成です。通常の起動時に実プロバイダー API を呼び出すことはありません。

## 動作要件

- Windows 10 バージョン 2004（`10.0.19041.0`）以降
- Git 管理対象外のローカル `.tools\dotnet` ツールチェーン。既存ドキュメントでは、`.slnx` の編成に .NET 10 SDK、製品プロジェクトに .NET 8 ランタイムを使用しています。
- UI スモークテストにはデスクトップセッションが必要です。

`.tools\dotnet` ディレクトリは新規クローンには含まれません。リポジトリのコマンドを使う前に互換性のある SDK/ランタイムを用意してください。このツールチェーン用の自動セットアップスクリプトは監査対象にありません。

## 実行

```powershell
.\.tools\dotnet\dotnet.exe run --project src\AliasCockpit.App\AliasCockpit.App.csproj
```

ローカルメタデータの保存先は次のとおりです。

```text
%LocalAppData%\AliasCockpit\aliases.sqlite
```

この開発用データベースは暗号化されていません。エイリアスメタデータ、入力履歴、マーカー、監査データ、プロバイダーの `secret_ref` は保存できますが、プロバイダートークンや API シークレットを保存してはいけません。

## 基本的なワークフロー

1. Gmail または Outlook のアドレスを入力し、タグを 1 行ずつ追加して、生成する候補数を指定します。
2. アドレスと利用先のフォームが対応している場合だけ、Gmail のドットエイリアスや `+tag` エイリアスを有効にします。対応しないドメインではドットの項目が無効になります。
3. 結果の概要を確認し、「すべて」「ドット」「プラス」「マーク済み」「未マーク」のフィルターで一覧を絞り込みます。
4. エイリアスを選び、利用先、用途、色のマーカーを保存します。繰り返し使うアドレスはローカル履歴にも保存できます。
5. 選択したエイリアス、または絞り込んだ結果をコピーします。プロバイダー操作は明示的な後続操作であり、ローカルでの生成だけではプロバイダーを呼び出しません。

## ビルド、テスト、フォーマット

```powershell
.\.tools\dotnet\dotnet.exe build AliasCockpit.slnx -v minimal
.\.tools\dotnet\dotnet.exe test AliasCockpit.slnx -v minimal
.\.tools\dotnet\dotnet.exe run --project benchmarks\AliasCockpit.Benchmarks\AliasCockpit.Benchmarks.csproj -c Release
.\.tools\dotnet\dotnet.exe format AliasCockpit.slnx --verify-no-changes --verbosity minimal
```

既存ドキュメントによると、`dotnet format` は処理を妨げないワークスペース読み込み警告を出す場合があります。終了コードとビルド/テストの結果を合わせて判定してください。

## リリースパッケージ

配布物を作る際は、完全なリリース検証を実行します。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-release.ps1
```

このスクリプトはビルド、テスト、ベンチマーク、フォーマット確認、公開処理を実行し、監査済みの未使用 WinAppSDK ファイルを削減します。その後ポータブル ZIP、MSI、セットアップ EXE を再構築して内容を検査し、公開版とポータブル版の起動スモークテスト、および基本 UI スモークテストを行います。

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
| `src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish\AliasCockpit.App.exe` | 完全なフォルダー公開内の実行ファイル。インストーラーや単一ファイルバンドルではありません |
| `artifacts\AliasCockpit-win-x64-portable.zip` | 公開ディレクトリ一式を含むポータブルパッケージ |
| `artifacts\AliasCockpit-win-x64.msi` | WiX によるマシン単位の MSI。インストール時に昇格が必要な場合があります |
| `artifacts\AliasCockpit-win-x64-setup.exe` | MSI を内包する WiX Burn セットアップ |

公開された EXE は、隣接する WinUI/.NET ランタイムファイルと一緒に保持してください。WiX はビルド時だけ使用します。`scripts\publish-github-release.ps1` は `NextWeb4/alias-cockpit` と `v1.0.0` を対象とし、`GITHUB_TOKEN`、`GH_TOKEN`、Git Credential Manager、または Codex 連携ヘルパーから実行時だけ認証情報を取得します。

## プロジェクト構成

| パス | 役割 |
| --- | --- |
| `src/AliasCockpit.App/` | WinUI 3 アプリシェル、メイン画面、ViewModel、クリップボード、デスクトップ連携 |
| `src/AliasCockpit.Core/` | UI 非依存のエイリアス、生成、CSV、監査、プロバイダー、シークレット、安全、展開ツールの契約 |
| `src/AliasCockpit.Infrastructure/` | SQLite、Windows Credential Manager、プロバイダーアダプター |
| `tests/AliasCockpit.App.Tests/` | WinUI ウィンドウを起動しない ViewModel テスト |
| `tests/AliasCockpit.Core.Tests/` | ドメイン動作の単体・ストレステスト |
| `tests/AliasCockpit.Infrastructure.Tests/` | SQLite、資格情報ストア、プロバイダーアダプターの統合テスト |
| `benchmarks/AliasCockpit.Benchmarks/` | 生成、CSV ドライラン、SQLite のベースライン |
| `docs/` | 調査、アーキテクチャ判断、セキュリティモデル、リリースノート |
| `scripts/` | ブランド素材、公開、パッケージ、整理、リリース、スモークテストの自動化 |

## データとセキュリティの境界

- Gmail のドットエイリアスは Gmail/Googlemail アドレスだけが対象です。Google Workspace の独自ドメインには自動適用しません。
- `+tag` エイリアスを受け付けない外部フォームもあります。
- プロバイダートークンは `WindowsCredentialManagerSecretStore` に保存し、SQLite にはシークレットキーモデルが生成した参照だけを保存します。
- サイト、用途、タグ、マーカー、保存アドレスの各フィールドに、パスワード、トークン、復旧コードなどの機密情報を書かないでください。
- プロバイダーエイリアスの一括無効化/削除は実行前に計画を作成し、削除には明示的な確認と監査記録が必要です。
- HTTP アダプターは明示的に呼び出すとキー検証と対応操作を行えますが、モックアダプターや偽の HTTP ハンドラーは実アカウントでの E2E 動作を保証しません。

## 作者

- HaoXiang Huang
- [Rays688888@Gmail.com](mailto:Rays688888@Gmail.com)
- <https://nextweb4.github.io/>

アイコンの原本は `src/AliasCockpit.App/Assets/AppIcon.ico` です。アプリ EXE、WinUI ウィンドウ、スタートメニューのショートカット、アプリ一覧、セットアップバンドルで同じアイコンを使用します。

## 保守とコントリビューション

- ドメイン処理は `src/AliasCockpit.Core/`、OS とプロバイダーの連携は `src/AliasCockpit.Infrastructure/`、画面表示は `src/AliasCockpit.App/` に置き、対応するテストプロジェクトへ検証を追加してください。
- 永続化、資格情報、プロバイダー操作、リリース境界を変更する前に、[ネイティブ UI のアーキテクチャ決定記録](docs/architecture/adr/0001-windows-native-ui-stack.md)、[テスト戦略](docs/architecture/test-strategy.md)、[脅威モデル](docs/security/threat-model.md)を確認してください。
- 通常の変更では上記のビルド、テスト、ベンチマーク、フォーマット確認を実行します。パッケージ変更では `scripts\verify-release.ps1` を最後まで実行し、すべての成果物を確認してください。
- 動作、コマンド、成果物名、セキュリティ上の制約、ライセンス情報を変更した場合は、3 言語の README を同時に更新してください。

## ライセンス

監査対象のリポジトリには `LICENSE` ファイルがありませんでした。所有者が別途利用条件を示すまでは、オープンソースライセンスが付与されているものとして扱わないでください。


