param(
    [switch]$Artifacts
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = [System.IO.Path]::GetFullPath((Join-Path $scriptDir ".."))

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

function Remove-CheckedPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [string[]]$AllowedLeafNames
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $fullPath = Assert-UnderWorkspace -Path $Path -Label $Label
    if ($AllowedLeafNames.Count -gt 0) {
        $leaf = Split-Path -Leaf $fullPath
        if ($AllowedLeafNames -notcontains $leaf) {
            throw "$Label has unexpected leaf '$leaf': $fullPath"
        }
    }

    Write-Host "Removing $fullPath"
    Remove-Item -LiteralPath $fullPath -Recurse -Force
}

$projectRoots = @(
    (Join-Path $workspace "src"),
    (Join-Path $workspace "tests"),
    (Join-Path $workspace "benchmarks")
)

foreach ($root in $projectRoots) {
    if (-not (Test-Path -LiteralPath $root)) {
        continue
    }

    $buildDirectories = Get-ChildItem -LiteralPath $root -Directory -Recurse |
        Where-Object { $_.Name -in @("bin", "obj") } |
        Sort-Object FullName -Descending

    foreach ($buildDirectory in $buildDirectories) {
        Remove-CheckedPath -Path $buildDirectory.FullName -Label "Build cache path" -AllowedLeafNames @("bin", "obj")
    }
}

if ($Artifacts) {
    $artifactPaths = @(
        (Join-Path $workspace "artifacts\AliasCockpit-win-x64-portable"),
        (Join-Path $workspace "artifacts\AliasCockpit-win-x64-portable.zip"),
        (Join-Path $workspace "artifacts\AliasCockpit-win-x64.msi"),
        (Join-Path $workspace "artifacts\AliasCockpit-win-x64-setup.exe"),
        (Join-Path $workspace "artifacts\AliasCockpit-win-x64.wixpdb"),
        (Join-Path $workspace "artifacts\msi-work"),
        (Join-Path $workspace "artifacts\setup-work"),
        (Join-Path $workspace "artifacts\setup-extract-check")
    )

    foreach ($artifactPath in $artifactPaths) {
        Remove-CheckedPath -Path $artifactPath -Label "Artifact path" -AllowedLeafNames @(
            "AliasCockpit-win-x64-portable",
            "AliasCockpit-win-x64-portable.zip",
            "AliasCockpit-win-x64.msi",
            "AliasCockpit-win-x64-setup.exe",
            "AliasCockpit-win-x64.wixpdb",
            "msi-work",
            "setup-work",
            "setup-extract-check"
        )
    }

    $debugImages = @(Get-ChildItem -LiteralPath (Join-Path $workspace "artifacts") -File -Filter "ui-smoke-*.png" -ErrorAction SilentlyContinue)
    foreach ($debugImage in $debugImages) {
        Remove-CheckedPath -Path $debugImage.FullName -Label "UI smoke debug artifact" -AllowedLeafNames @($debugImage.Name)
    }
}

Write-Host "Build cache clean complete."
