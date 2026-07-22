


param(
    [string]$PublishDir,
    [string]$OutputPath,
    [string]$Version = "1.0.0",
    [string]$ProductName = "Alias Cockpit",
    [string]$Manufacturer = "HaoXiang Huang",
    [string]$CreatorWebsite = "https://nextweb4.github.io/",
    [string]$CreatorEmail = "Rays688888@Gmail.com",
    [switch]$KeepWork,
    [switch]$NoToolRestore
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = [System.IO.Path]::GetFullPath((Join-Path $scriptDir ".."))
$dotnet = Join-Path $workspace ".tools\dotnet\dotnet.exe"
$wixVersion = "5.0.2"
$wixToolDir = Join-Path $workspace ".tools\wix"
$wix = Join-Path $wixToolDir "wix.exe"

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $workspace "src\AliasCockpit.App\bin\Release\net8.0-windows10.0.26100.0\win-x64\publish"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $workspace "artifacts\AliasCockpit-win-x64.msi"
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

function Normalize-MsiFileLanguages {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DatabasePath
    )

    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.GetType().InvokeMember(
        "OpenDatabase",
        "InvokeMethod",
        $null,
        $installer,
        @($DatabasePath, 1))
    $view = $database.OpenView("UPDATE ``File`` SET ``Language``='0'")
    try {
        $view.Execute()
        $view.Close()
        $database.Commit()
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) | Out-Null
        [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($database) | Out-Null
        [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) | Out-Null
    }
}

function ConvertTo-WixId {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prefix,
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $safe = [System.Text.RegularExpressions.Regex]::Replace($Value, "[^A-Za-z0-9_]", "_")
    if ([string]::IsNullOrWhiteSpace($safe)) {
        $safe = "root"
    }

    if ($safe.Length -gt 40) {
        $safe = $safe.Substring(0, 40)
    }

    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        $hash = [System.BitConverter]::ToString($sha1.ComputeHash($bytes)).Replace("-", "").Substring(0, 12).ToLowerInvariant()
    }
    finally {
        $sha1.Dispose()
    }

    return "$Prefix`_$safe`_$hash"
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

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,
        [Parameter(Mandatory = $true)]
        [string]$FullPath
    )

    $baseNormalized = [System.IO.Path]::GetFullPath($BasePath).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $targetFullPath = [System.IO.Path]::GetFullPath($FullPath)
    $targetNormalized = $targetFullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    if ([string]::Equals($baseNormalized, $targetNormalized, [System.StringComparison]::OrdinalIgnoreCase)) {
        return ""
    }

    $baseFullPath = $baseNormalized + [System.IO.Path]::DirectorySeparatorChar
    $baseUri = [Uri]$baseFullPath
    $targetUri = [Uri]$targetFullPath
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace("/", "\")
}

function Add-Line {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Value
    )

    $Lines.Add($Value) | Out-Null
}

function Add-DirectoryTree {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [Parameter(Mandatory = $true)]
        [string]$DirectoryPath,
        [string]$RelativePath,
        [Parameter(Mandatory = $true)]
        [string]$Indent
    )

    $childDirs = @(Get-ChildItem -LiteralPath $DirectoryPath -Directory | Sort-Object Name)
    foreach ($childDir in $childDirs) {
        $childRelativePath = if ([string]::IsNullOrWhiteSpace($RelativePath)) {
            $childDir.Name
        }
        else {
            Join-Path $RelativePath $childDir.Name
        }

        $directoryId = ConvertTo-WixId -Prefix "dir" -Value $childRelativePath
        Add-Line -Lines $Lines -Value "$Indent<Directory Id=`"$directoryId`" Name=`"$(ConvertTo-XmlAttribute $childDir.Name)`">"
        Add-DirectoryTree -Lines $Lines -DirectoryPath $childDir.FullName -RelativePath $childRelativePath -Indent "$Indent  "
        Add-Line -Lines $Lines -Value "$Indent</Directory>"
    }
}

function Get-DirectoryId {
    param(
        [string]$RelativePath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return "INSTALLFOLDER"
    }

    return ConvertTo-WixId -Prefix "dir" -Value $RelativePath
}

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "Missing local dotnet: $dotnet"
}

if (-not (Test-Path -LiteralPath $wix)) {
    if ($NoToolRestore) {
        throw "Missing WiX CLI: $wix"
    }

    New-Item -ItemType Directory -Path $wixToolDir -Force | Out-Null
    Invoke-Dotnet "tool" "install" "wix" "--version" $wixVersion "--tool-path" $wixToolDir
}

$installedWixVersion = (& $wix "--version").Trim()
if (-not $installedWixVersion.StartsWith($wixVersion, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Unexpected WiX version '$installedWixVersion'. Expected $wixVersion."
}

$publish = (Resolve-Path -LiteralPath $PublishDir).Path
$publish = Assert-UnderWorkspace -Path $publish -Label "Publish path"
$output = Assert-UnderWorkspace -Path $OutputPath -Label "MSI output path"
$outputDirectory = Split-Path -Parent $output
$workDirectory = Assert-UnderWorkspace -Path (Join-Path $workspace "artifacts\msi-work") -Label "MSI work path"
$wxs = Join-Path $workDirectory "Package.wxs"
$intermediate = Join-Path $workDirectory "intermediate"
$icon = Join-Path $workspace "src\AliasCockpit.App\Assets\AppIcon.ico"

if (-not (Test-Path -LiteralPath (Join-Path $publish "AliasCockpit.App.exe"))) {
    throw "Publish directory does not contain AliasCockpit.App.exe: $publish"
}

if (-not (Test-Path -LiteralPath $icon)) {
    throw "Missing application icon: $icon"
}

$files = @(Get-ChildItem -LiteralPath $publish -Recurse -File | Sort-Object FullName)
if ($files.Count -eq 0) {
    throw "Publish directory is empty: $publish"
}

if (Test-Path -LiteralPath $workDirectory) {
    Remove-Item -LiteralPath $workDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $workDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $intermediate -Force | Out-Null
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Force
}

$componentIds = New-Object System.Collections.Generic.List[string]
$lines = New-Object System.Collections.Generic.List[string]
$upgradeCode = "A0843697-29B1-44AD-837B-B08A53B8C9E8"

Add-Line -Lines $lines -Value "<?xml version=`"1.0`" encoding=`"utf-8`"?>"
Add-Line -Lines $lines -Value "<Wix xmlns=`"http://wixtoolset.org/schemas/v4/wxs`">"
Add-Line -Lines $lines -Value "  <Package Name=`"$(ConvertTo-XmlAttribute $ProductName)`" Manufacturer=`"$(ConvertTo-XmlAttribute $Manufacturer)`" Version=`"$(ConvertTo-XmlAttribute $Version)`" UpgradeCode=`"$upgradeCode`" Scope=`"perMachine`">"
Add-Line -Lines $lines -Value "    <Icon Id=`"AppIcon`" SourceFile=`"$(ConvertTo-XmlAttribute $icon)`" />"
Add-Line -Lines $lines -Value "    <Property Id=`"ARPPRODUCTICON`" Value=`"AppIcon`" />"
Add-Line -Lines $lines -Value "    <Property Id=`"ARPCONTACT`" Value=`"$(ConvertTo-XmlAttribute $CreatorEmail)`" />"
Add-Line -Lines $lines -Value "    <Property Id=`"ARPURLINFOABOUT`" Value=`"$(ConvertTo-XmlAttribute $CreatorWebsite)`" />"
Add-Line -Lines $lines -Value "    <MajorUpgrade DowngradeErrorMessage=`"A newer version of [ProductName] is already installed.`" />"
Add-Line -Lines $lines -Value "    <MediaTemplate EmbedCab=`"yes`" />"
Add-Line -Lines $lines -Value "    <Feature Id=`"MainFeature`" Title=`"$(ConvertTo-XmlAttribute $ProductName)`" Level=`"1`">"
Add-Line -Lines $lines -Value "      <ComponentGroupRef Id=`"PublishedFiles`" />"
Add-Line -Lines $lines -Value "      <ComponentRef Id=`"ApplicationShortcut`" />"
Add-Line -Lines $lines -Value "    </Feature>"
Add-Line -Lines $lines -Value "  </Package>"
Add-Line -Lines $lines -Value ""
Add-Line -Lines $lines -Value "  <Fragment>"
Add-Line -Lines $lines -Value "    <StandardDirectory Id=`"ProgramFiles64Folder`">"
Add-Line -Lines $lines -Value "      <Directory Id=`"INSTALLFOLDER`" Name=`"AliasCockpit`">"
Add-DirectoryTree -Lines $lines -DirectoryPath $publish -RelativePath "" -Indent "        "
Add-Line -Lines $lines -Value "      </Directory>"
Add-Line -Lines $lines -Value "    </StandardDirectory>"
Add-Line -Lines $lines -Value "    <StandardDirectory Id=`"ProgramMenuFolder`">"
Add-Line -Lines $lines -Value "      <Directory Id=`"ApplicationProgramsFolder`" Name=`"$(ConvertTo-XmlAttribute $ProductName)`" />"
Add-Line -Lines $lines -Value "    </StandardDirectory>"
Add-Line -Lines $lines -Value "  </Fragment>"
Add-Line -Lines $lines -Value ""

$directories = @((Get-Item -LiteralPath $publish)) + @(Get-ChildItem -LiteralPath $publish -Recurse -Directory | Sort-Object FullName)
foreach ($directory in $directories) {
    $relativeDirectoryPath = Get-RelativePath -BasePath $publish -FullPath $directory.FullName
    if ($relativeDirectoryPath -eq ".\") {
        $relativeDirectoryPath = ""
    }

    $directoryId = Get-DirectoryId -RelativePath $relativeDirectoryPath
    $directoryFiles = @(Get-ChildItem -LiteralPath $directory.FullName -File | Sort-Object Name)
    if ($directoryFiles.Count -eq 0) {
        continue
    }

    Add-Line -Lines $lines -Value "  <Fragment>"
    Add-Line -Lines $lines -Value "    <DirectoryRef Id=`"$directoryId`">"

    foreach ($file in $directoryFiles) {
        $relativeFilePath = Get-RelativePath -BasePath $publish -FullPath $file.FullName
        $componentId = ConvertTo-WixId -Prefix "cmp" -Value $relativeFilePath
        $fileId = ConvertTo-WixId -Prefix "fil" -Value $relativeFilePath
        $componentIds.Add($componentId) | Out-Null

        Add-Line -Lines $lines -Value "      <Component Id=`"$componentId`" Guid=`"*`">"
        Add-Line -Lines $lines -Value "        <File Id=`"$fileId`" Source=`"$(ConvertTo-XmlAttribute $file.FullName)`" KeyPath=`"yes`" />"
        Add-Line -Lines $lines -Value "      </Component>"
    }

    Add-Line -Lines $lines -Value "    </DirectoryRef>"
    Add-Line -Lines $lines -Value "  </Fragment>"
    Add-Line -Lines $lines -Value ""
}

Add-Line -Lines $lines -Value "  <Fragment>"
Add-Line -Lines $lines -Value "    <DirectoryRef Id=`"ApplicationProgramsFolder`">"
Add-Line -Lines $lines -Value "      <Component Id=`"ApplicationShortcut`" Guid=`"*`">"
Add-Line -Lines $lines -Value "        <Shortcut Id=`"StartMenuShortcut`" Name=`"$(ConvertTo-XmlAttribute $ProductName)`" Description=`"Local email alias cockpit`" Target=`"[INSTALLFOLDER]AliasCockpit.App.exe`" WorkingDirectory=`"INSTALLFOLDER`" Icon=`"AppIcon`" IconIndex=`"0`" />"
Add-Line -Lines $lines -Value "        <RemoveFolder Id=`"RemoveApplicationProgramsFolder`" On=`"uninstall`" />"
Add-Line -Lines $lines -Value "        <RegistryValue Root=`"HKCU`" Key=`"Software\AliasCockpit`" Name=`"InstallDir`" Type=`"string`" Value=`"[INSTALLFOLDER]`" KeyPath=`"yes`" />"
Add-Line -Lines $lines -Value "      </Component>"
Add-Line -Lines $lines -Value "    </DirectoryRef>"
Add-Line -Lines $lines -Value "  </Fragment>"
Add-Line -Lines $lines -Value ""
Add-Line -Lines $lines -Value "  <Fragment>"
Add-Line -Lines $lines -Value "    <ComponentGroup Id=`"PublishedFiles`">"

foreach ($componentId in $componentIds) {
    Add-Line -Lines $lines -Value "      <ComponentRef Id=`"$componentId`" />"
}

Add-Line -Lines $lines -Value "    </ComponentGroup>"
Add-Line -Lines $lines -Value "  </Fragment>"
Add-Line -Lines $lines -Value "</Wix>"

[System.IO.File]::WriteAllLines($wxs, $lines, [System.Text.UTF8Encoding]::new($false))

& $wix "build" $wxs "-arch" "x64" "-dcl" "high" "-pdbtype" "none" "-intermediatefolder" $intermediate "-o" $output
if ($LASTEXITCODE -ne 0) {
    throw "WiX MSI build failed."
}

Normalize-MsiFileLanguages -DatabasePath $output

$msiBytes = (Get-Item -LiteralPath $output).Length
if ($msiBytes -le 0) {
    throw "MSI output is empty: $output"
}

Write-Host "MSI created: $output"
Write-Host "MSI bytes: $msiBytes"
Write-Host "Packaged files: $($files.Count)"

if (-not $KeepWork -and (Test-Path -LiteralPath $workDirectory)) {
    Remove-Item -LiteralPath $workDirectory -Recurse -Force
}
