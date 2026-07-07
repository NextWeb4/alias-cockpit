# Alias Cockpit

[English](README.md) | [中文](README.zh-CN.md)

## 简介

Alias Cockpit 是一个 Windows 本地邮箱别名管理工具，用于生成、管理、同步、导入和导出 Email Alias。当前版本的主界面是本地 Gmail / Outlook 邮箱别名扩展器：输入一个邮箱地址和标签后，可以生成 Gmail 点号别名与 `+tag` 别名，并在本机保存常用输入邮箱、站点用途标记、颜色标记和已标记 / 未标记筛选状态。

当前版本已经包含 WinUI 3 / .NET 桌面应用骨架、核心别名生成逻辑、本地 SQLite 持久化、Windows Credential Manager 凭据存储边界、SimpleLogin / addy.io Provider adapter 基础、MSI / EXE 安装包构建、便携版 zip、单元测试、集成测试和发布脚本。加密同步、高级 Provider 同步和完整 UI 自动化仍在后续范围内。

作者信息固定写在产品代码、安装器元数据和发布文档中，不从配置文件或环境变量读取：

- 作者：HaoXiang Hwang
- 网站：https://nextweb4.github.io/
- 邮箱：didadida1688@gmail.com

应用图标源文件位于 `src\AliasCockpit.App\Assets\AppIcon.ico`。它会通过 `ApplicationIcon` 写入应用 exe，通过 `AppWindow.SetIcon` 用于 WinUI 窗口，并通过 `scripts\package-msi.ps1` 写入 MSI 开始菜单快捷方式和“添加/删除程序”条目。

## 常用命令

```powershell
.\.tools\dotnet\dotnet.exe build AliasCockpit.slnx -v minimal
.\.tools\dotnet\dotnet.exe test AliasCockpit.slnx -v minimal
.\.tools\dotnet\dotnet.exe run --project benchmarks\AliasCockpit.Benchmarks\AliasCockpit.Benchmarks.csproj -c Release
.\.tools\dotnet\dotnet.exe format AliasCockpit.slnx --verify-no-changes --verbosity minimal
.\.tools\dotnet\dotnet.exe publish src\AliasCockpit.App\AliasCockpit.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -v minimal
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\prune-publish.ps1 -PublishDir src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\clean-build-cache.ps1 -Artifacts
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-msi.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-setup-exe.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-github-release.ps1
```

完整发布验证会执行发布目录裁剪、便携版 zip 重建、MSI 重建与校验、setup EXE 重建与抽取检查、zip 内容检查、进程启动冒烟测试，以及基础 UI 冒烟测试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-release.ps1
```

桌面应用运行候选命令：

```powershell
.\.tools\dotnet\dotnet.exe run --project src\AliasCockpit.App\AliasCockpit.App.csproj
```

## 发布产物

发布后的 x64 应用 exe 位于：

```text
src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish\AliasCockpit.App.exe
```

这个 exe 是文件夹发布目录里的应用程序本体，不是安装包，也不是单文件包。运行它时必须保留同目录的 WinUI / .NET 运行时文件。

便携版：

```text
artifacts\AliasCockpit-win-x64-portable.zip
```

MSI 安装包：

```text
artifacts\AliasCockpit-win-x64.msi
```

EXE 安装包：

```text
artifacts\AliasCockpit-win-x64-setup.exe
```

这个 EXE 是 WiX Burn 安装器，内部嵌入 MSI。对外分发时应使用这个 setup EXE，不要把发布目录里的 `AliasCockpit.App.exe` 或快捷方式当成安装包。

GitHub Release 发布目标：

```text
https://github.com/NextWeb4/alias-cockpit
```

`scripts\publish-github-release.ps1` 会创建 / 使用该仓库，推送 `main`，创建或更新 `v1.0.0` Release，并上传 setup EXE、MSI 和便携版 zip。Token 只在运行时通过 `GITHUB_TOKEN`、`GH_TOKEN`、Git Credential Manager 或 Codex GitHub 集成 helper 临时读取，不写入仓库。

## 项目结构

- `src/AliasCockpit.App`：WinUI 3 Windows 桌面应用壳，当前主界面是本地 Email Alias Expander。
- `src/AliasCockpit.Core`：不依赖 UI 的领域逻辑、邮箱别名生成、审计模型和 Provider 抽象。
- `src/AliasCockpit.Infrastructure`：SQLite 持久化与基础设施适配器。
- `tests/AliasCockpit.App.Tests`：App / ViewModel 单元测试。
- `tests/AliasCockpit.Core.Tests`：核心逻辑单元测试和压力测试。
- `tests/AliasCockpit.Infrastructure.Tests`：SQLite / Credential Manager / Provider adapter 集成测试。
- `benchmarks/AliasCockpit.Benchmarks`：基础性能测量入口。
- `docs`：调研、架构、安全和发布决策文档。

## 本地数据

当前 Email Alias Expander 只在本地生成结果，并读写本机 SQLite 元数据：

- 保存过的输入邮箱地址；
- 生成 alias 的站点、用途、颜色等标记。

SQLite 数据库路径：

```text
%LocalAppData%\AliasCockpit\aliases.sqlite
```

该开发库目前尚未加密。Provider token 和 secret 不存入 SQLite；不要把 API token、密码、恢复密钥或其他敏感信息写入站点、用途、标签等标记字段。

Provider token 应通过 `WindowsCredentialManagerSecretStore` 使用 Windows Credential Manager 保存；SQLite 只保存后续的 `secret_ref`。

## 当前门禁

- Build：通过。
- 单元 / 压力 / 集成测试：通过。
- Benchmark：通过。
- Format check：通过，可能有非阻塞 workspace load warning。
- win-x64 Publish：通过，trimming 关闭以降低 WinUI 发布风险。
- 启动冒烟测试：通过。
- Portable artifact：通过。
- MSI artifact：通过。
- Setup EXE artifact：通过。
- UI smoke：通过。
- Release verification script：`scripts\verify-release.ps1`。
