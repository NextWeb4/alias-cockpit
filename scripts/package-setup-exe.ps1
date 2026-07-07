param(
    [string]$MsiPath,
    [string]$OutputPath,
    [string]$Version = "1.0.0",
    [string]$ProductName = "Alias Cockpit",
    [string]$Manufacturer = "HaoXiang Hwang",
    [string]$CreatorWebsite = "https://nextweb4.github.io/",
    [switch]$KeepWork
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = [System.IO.Path]::GetFullPath((Join-Path $scriptDir ".."))
$wixVersion = "5.0.2"
$wix = Join-Path $workspace ".tools\wix\wix.exe"
$balExtensionId = "WixToolset.Bal.wixext"
$balExtensionDllName = "WixToolset.BootstrapperApplications.wixext.dll"

if ([string]::IsNullOrWhiteSpace($MsiPath)) {
    $MsiPath = Join-Path $workspace "artifacts\AliasCockpit-win-x64.msi"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $workspace "artifacts\AliasCockpit-win-x64-setup.exe"
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

function ConvertTo-XmlAttribute {
    param(
        [AllowNull()]
        [string]$Value
    )

    if ($null -eq $Value) {
        return ""
    }

    return [System.Security.SecurityElement]::Escape($Value)
}

function Add-Line {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Value
    )

    $Lines.Add($Value) | Out-Null
}

function Get-BalExtensionPath {
    $extensionPath = Join-Path $env:USERPROFILE ".wix\extensions\$balExtensionId\$wixVersion\wixext5\$balExtensionDllName"
    if (-not (Test-Path -LiteralPath $extensionPath)) {
        & $wix "extension" "add" "$balExtensionId/$wixVersion"
        if ($LASTEXITCODE -ne 0 -and -not (Test-Path -LiteralPath $extensionPath)) {
            throw "Failed to restore WiX BAL extension $balExtensionId/$wixVersion."
        }
    }

    if (-not (Test-Path -LiteralPath $extensionPath)) {
        throw "Missing WiX BAL extension DLL: $extensionPath"
    }

    return $extensionPath
}

if (-not (Test-Path -LiteralPath $wix)) {
    throw "Missing WiX CLI: $wix"
}

$installedWixVersion = (& $wix "--version").Trim()
if (-not $installedWixVersion.StartsWith($wixVersion, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Unexpected WiX version '$installedWixVersion'. Expected $wixVersion."
}

$msi = (Resolve-Path -LiteralPath $MsiPath).Path
$msi = Assert-UnderWorkspace -Path $msi -Label "MSI path"
$output = Assert-UnderWorkspace -Path $OutputPath -Label "Setup EXE output path"
$outputDirectory = Split-Path -Parent $output
$workDirectory = Assert-UnderWorkspace -Path (Join-Path $workspace "artifacts\setup-work") -Label "Setup EXE work path"
$wxs = Join-Path $workDirectory "Bundle.wxs"
$intermediate = Join-Path $workDirectory "intermediate"
$icon = Join-Path $workspace "src\AliasCockpit.App\Assets\AppIcon.ico"
$balExtension = Get-BalExtensionPath
$bundleUpgradeCode = "0D3832DC-1E5C-4D21-A7EF-75B5AC4254E9"

if (Test-Path -LiteralPath $workDirectory) {
    Remove-Item -LiteralPath $workDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $workDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $intermediate -Force | Out-Null
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Force
}

$lines = New-Object System.Collections.Generic.List[string]
Add-Line -Lines $lines -Value "<?xml version=`"1.0`" encoding=`"utf-8`"?>"
Add-Line -Lines $lines -Value "<Wix xmlns=`"http://wixtoolset.org/schemas/v4/wxs`" xmlns:bal=`"http://wixtoolset.org/schemas/v4/wxs/bal`">"
Add-Line -Lines $lines -Value "  <Bundle Name=`"$(ConvertTo-XmlAttribute $ProductName)`" Manufacturer=`"$(ConvertTo-XmlAttribute $Manufacturer)`" Version=`"$(ConvertTo-XmlAttribute $Version)`" UpgradeCode=`"$bundleUpgradeCode`" IconSourceFile=`"$(ConvertTo-XmlAttribute $icon)`">"
Add-Line -Lines $lines -Value "    <BootstrapperApplication>"
Add-Line -Lines $lines -Value "      <bal:WixStandardBootstrapperApplication Theme=`"hyperlinkLicense`" LicenseUrl=`"$(ConvertTo-XmlAttribute $CreatorWebsite)`" />"
Add-Line -Lines $lines -Value "    </BootstrapperApplication>"
Add-Line -Lines $lines -Value "    <Chain>"
Add-Line -Lines $lines -Value "      <MsiPackage Id=`"AliasCockpitMsi`" SourceFile=`"$(ConvertTo-XmlAttribute $msi)`" Compressed=`"yes`" Vital=`"yes`" />"
Add-Line -Lines $lines -Value "    </Chain>"
Add-Line -Lines $lines -Value "  </Bundle>"
Add-Line -Lines $lines -Value "</Wix>"

[System.IO.File]::WriteAllLines($wxs, $lines, [System.Text.UTF8Encoding]::new($false))

& $wix "build" $wxs "-ext" $balExtension "-pdbtype" "none" "-intermediatefolder" $intermediate "-o" $output
if ($LASTEXITCODE -ne 0) {
    throw "WiX setup EXE build failed."
}

$setupBytes = (Get-Item -LiteralPath $output).Length
if ($setupBytes -le 0) {
    throw "Setup EXE output is empty: $output"
}

Write-Host "Setup EXE created: $output"
Write-Host "Setup EXE bytes: $setupBytes"
Write-Host "Embedded MSI: $msi"

if (-not $KeepWork -and (Test-Path -LiteralPath $workDirectory)) {
    Remove-Item -LiteralPath $workDirectory -Recurse -Force
}
