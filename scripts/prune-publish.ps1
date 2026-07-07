param(
    [string]$PublishDir,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = [System.IO.Path]::GetFullPath((Join-Path $scriptDir ".."))

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $workspace "src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish"
}

function Assert-UnderWorkspace {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($workspace, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label escaped workspace: $fullPath"
    }

    return $fullPath
}

$publish = (Resolve-Path -LiteralPath $PublishDir).Path
$publish = Assert-UnderWorkspace -Path $publish -Label "Publish path"

if (-not (Test-Path -LiteralPath (Join-Path $publish "AliasCockpit.App.exe"))) {
    throw "Publish directory does not contain AliasCockpit.App.exe: $publish"
}

$patterns = @(
    "*.pdb",
    "createdump.exe",
    "Microsoft.DiaSymReader.Native.amd64.dll",
    "mscordaccore*.dll",
    "mscordbi.dll",
    "onnxruntime.dll",
    "DirectML.dll",
    "Microsoft.ML.OnnxRuntime.dll",
    "Microsoft.Windows.AI*",
    "Microsoft.Windows.Internal.AI*",
    "Microsoft.Windows.*Vision*",
    "Microsoft.Windows.*SemanticSearch*",
    "Microsoft.Graphics.Imaging*",
    "Microsoft.Graphics.Internal.Imaging*",
    "Microsoft.Windows.ImageCreationInternal.winmd",
    "Microsoft.Windows.Internal.ImageCreation.winmd",
    "Microsoft.Windows.Private.Workloads.SessionManager.winmd",
    "Microsoft.Windows.Workloads*",
    "NPUDetect.dll",
    "SessionHandleIPCProxyStub.dll",
    "workloads*.json",
    "Microsoft.Windows.Widgets*",
    "Microsoft.Web.WebView2*",
    "WebView2Loader.dll",
    "Microsoft.Security.Authentication.OAuth*",
    "Microsoft.Windows.ApplicationModel.Background*",
    "Microsoft.Windows.AppNotifications*",
    "Microsoft.Windows.BadgeNotifications*",
    "PushNotificationsLongRunningTask.ProxyStub.dll",
    "RestartAgent.exe"
)

$filesByPath = @{}
foreach ($pattern in $patterns) {
    $matches = @(Get-ChildItem -LiteralPath $publish -File -Filter $pattern -ErrorAction SilentlyContinue)
    foreach ($match in $matches) {
        $filesByPath[$match.FullName] = $match
    }
}

$files = @($filesByPath.Values | Sort-Object FullName)
$bytes = ($files | Measure-Object Length -Sum).Sum
if ($null -eq $bytes) {
    $bytes = 0
}

if ($DryRun) {
    foreach ($file in $files) {
        Write-Host "Would remove $($file.FullName)"
    }

    Write-Host "Publish prune dry run: $($files.Count) files, $bytes bytes."
    return
}

foreach ($file in $files) {
    Remove-Item -LiteralPath $file.FullName -Force
}

Write-Host "Publish pruned: $($files.Count) files, $bytes bytes removed."
