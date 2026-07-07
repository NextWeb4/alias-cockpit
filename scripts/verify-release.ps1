param(
    [switch]$SkipBenchmark,
    [switch]$SkipUiSmoke,
    [switch]$SkipMsi,
    [switch]$SkipSetupExe
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = [System.IO.Path]::GetFullPath((Join-Path $scriptDir ".."))
$dotnet = Join-Path $workspace ".tools\dotnet\dotnet.exe"
$wix = Join-Path $workspace ".tools\wix\wix.exe"
$uiSmokeScript = Join-Path $workspace "scripts\verify-ui-smoke.ps1"
$prunePublishScript = Join-Path $workspace "scripts\prune-publish.ps1"
$msiPackageScript = Join-Path $workspace "scripts\package-msi.ps1"
$setupExePackageScript = Join-Path $workspace "scripts\package-setup-exe.ps1"
$solution = Join-Path $workspace "AliasCockpit.slnx"
$appProject = Join-Path $workspace "src\AliasCockpit.App\AliasCockpit.App.csproj"
$benchmarkProject = Join-Path $workspace "benchmarks\AliasCockpit.Benchmarks\AliasCockpit.Benchmarks.csproj"
$publish = Join-Path $workspace "src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish"
$artifact = Join-Path $workspace "artifacts\AliasCockpit-win-x64-portable"
$zip = Join-Path $workspace "artifacts\AliasCockpit-win-x64-portable.zip"
$msi = Join-Path $workspace "artifacts\AliasCockpit-win-x64.msi"
$setupExe = Join-Path $workspace "artifacts\AliasCockpit-win-x64-setup.exe"
$setupExtract = Join-Path $workspace "artifacts\setup-extract-check"

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

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Name"
    & $Action
}

function Invoke-Dotnet {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed: $($Arguments -join ' ')"
    }
}

function Test-AppLaunch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExePath
    )

    $resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
    $process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -WindowStyle Hidden -PassThru
    try {
        Start-Sleep -Seconds 5
        $process.Refresh()
        if ($process.HasExited) {
            throw "Application exited early with code $($process.ExitCode): $resolvedExe"
        }

        Write-Host "Started successfully: PID $($process.Id)"
    }
    finally {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
    }
}

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "Missing local dotnet: $dotnet"
}

$artifact = Assert-UnderWorkspace -Path $artifact -Label "Artifact path"
$zip = Assert-UnderWorkspace -Path $zip -Label "Zip path"
$msi = Assert-UnderWorkspace -Path $msi -Label "MSI path"
$setupExe = Assert-UnderWorkspace -Path $setupExe -Label "Setup EXE path"
$setupExtract = Assert-UnderWorkspace -Path $setupExtract -Label "Setup EXE extract path"
$publish = Assert-UnderWorkspace -Path $publish -Label "Publish path"

Invoke-Step "dotnet runtimes" {
    Invoke-Dotnet "--list-runtimes"
}

Invoke-Step "build" {
    Invoke-Dotnet "build" $solution "-v" "minimal"
}

Invoke-Step "test" {
    Invoke-Dotnet "test" $solution "-v" "minimal"
}

if (-not $SkipBenchmark) {
    Invoke-Step "benchmark" {
        Invoke-Dotnet "run" "--project" $benchmarkProject "-c" "Release"
    }
}

Invoke-Step "format verify" {
    Invoke-Dotnet "format" $solution "--verify-no-changes" "--verbosity" "minimal"
}

Invoke-Step "publish win-x64" {
    Invoke-Dotnet "publish" $appProject "-c" "Release" "-r" "win-x64" "--self-contained" "true" "-p:PublishSingleFile=false" "--disable-build-servers" "-v" "minimal"
}

Invoke-Step "prune publish" {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $prunePublishScript -PublishDir $publish
    if ($LASTEXITCODE -ne 0) {
        throw "Publish pruning failed."
    }
}

Invoke-Step "publish smoke" {
    Test-AppLaunch -ExePath (Join-Path $publish "AliasCockpit.App.exe")
}

Invoke-Step "portable artifact" {
    if (Test-Path -LiteralPath $artifact) {
        Remove-Item -LiteralPath $artifact -Recurse -Force
    }
    if (Test-Path -LiteralPath $zip) {
        Remove-Item -LiteralPath $zip -Force
    }

    New-Item -ItemType Directory -Path $artifact | Out-Null
    Copy-Item -Path (Join-Path $publish "*") -Destination $artifact -Recurse -Force
    Compress-Archive -Path (Join-Path $artifact "*") -DestinationPath $zip -Force
}

Invoke-Step "zip check" {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $hasRootExe = [bool]($archive.Entries | Where-Object { $_.FullName -eq "AliasCockpit.App.exe" } | Select-Object -First 1)
        if (-not $hasRootExe) {
            throw "Zip root does not contain AliasCockpit.App.exe"
        }

        $zipBytes = (Get-Item -LiteralPath $zip).Length
        Write-Host "Zip entries: $($archive.Entries.Count)"
        Write-Host "Zip bytes: $zipBytes"
    }
    finally {
        $archive.Dispose()
    }
}

Invoke-Step "artifact smoke" {
    Test-AppLaunch -ExePath (Join-Path $artifact "AliasCockpit.App.exe")
}

if (-not $SkipMsi) {
    Invoke-Step "msi artifact" {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $msiPackageScript -PublishDir $publish -OutputPath $msi
        if ($LASTEXITCODE -ne 0) {
            throw "MSI packaging failed."
        }
    }

    Invoke-Step "msi validate" {
        & $wix "msi" "validate" $msi
        if ($LASTEXITCODE -ne 0) {
            throw "MSI validation failed."
        }
    }

    if (-not $SkipSetupExe) {
        Invoke-Step "setup exe artifact" {
            & powershell -NoProfile -ExecutionPolicy Bypass -File $setupExePackageScript -MsiPath $msi -OutputPath $setupExe
            if ($LASTEXITCODE -ne 0) {
                throw "Setup EXE packaging failed."
            }
        }

        Invoke-Step "setup exe extract check" {
            if (Test-Path -LiteralPath $setupExtract) {
                Remove-Item -LiteralPath $setupExtract -Recurse -Force
            }

            & $wix "burn" "extract" $setupExe "-o" $setupExtract
            if ($LASTEXITCODE -ne 0) {
                throw "Setup EXE extraction failed."
            }

            $msiBytes = (Get-Item -LiteralPath $msi).Length
            $matchingPayload = Get-ChildItem -LiteralPath $setupExtract -Recurse -File |
                Where-Object { $_.Length -eq $msiBytes } |
                Select-Object -First 1
            if ($null -eq $matchingPayload) {
                throw "Setup EXE does not contain a payload matching the MSI size $msiBytes."
            }

            Write-Host "Setup EXE payload matched MSI bytes: $msiBytes"
            Remove-Item -LiteralPath $setupExtract -Recurse -Force
        }
    }
}

if (-not $SkipUiSmoke) {
    Invoke-Step "ui smoke" {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $uiSmokeScript -ExePath (Join-Path $artifact "AliasCockpit.App.exe")
        if ($LASTEXITCODE -ne 0) {
            throw "UI smoke failed."
        }
    }
}

Write-Host ""
Write-Host "Release verification passed."
