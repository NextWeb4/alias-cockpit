param(
    [string]$Owner = "NextWeb4",
    [string]$Repo = "alias-cockpit",
    [string]$Tag = "v1.0.0",
    [string]$Branch = "main",
    [string]$ReleaseName = "Alias Cockpit v1.0.0",
    [string]$RemoteName = "origin",
    [string]$TokenScript
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = [System.IO.Path]::GetFullPath((Join-Path $scriptDir ".."))
$releaseNotesPath = Join-Path $workspace "docs\release\v1.0.0.md"
$artifacts = @(
    (Join-Path $workspace "artifacts\AliasCockpit-win-x64-setup.exe"),
    (Join-Path $workspace "artifacts\AliasCockpit-win-x64.msi"),
    (Join-Path $workspace "artifacts\AliasCockpit-win-x64-portable.zip")
)

function Get-GitHubToken {
    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        return $env:GITHUB_TOKEN
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
        return $env:GH_TOKEN
    }

    if ([string]::IsNullOrWhiteSpace($TokenScript)) {
        $candidate = Join-Path $env:USERPROFILE ".cc-switch\skills\github-skill\get-token.ps1"
        if (Test-Path -LiteralPath $candidate) {
            $TokenScript = $candidate
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($TokenScript) -and (Test-Path -LiteralPath $TokenScript)) {
        $token = & $TokenScript 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($token) -and $token -notlike "ERROR*") {
            return $token
        }
    }

    $credentialToken = Get-GitCredentialManagerToken
    if (-not [string]::IsNullOrWhiteSpace($credentialToken)) {
        return $credentialToken
    }

    throw "GitHub token unavailable. Set GITHUB_TOKEN, sign in through Git Credential Manager, or complete GitHub authorization in the app integration panel and retry."
}

function Get-GitCredentialManagerToken {
    try {
        $credential = @"
protocol=https
host=github.com

"@ | git credential fill
    }
    catch {
        return $null
    }

    if ($LASTEXITCODE -ne 0 -or $null -eq $credential) {
        return $null
    }

    $passwordLine = $credential | Where-Object { $_ -like "password=*" } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($passwordLine)) {
        return $null
    }

    $token = $passwordLine.Substring("password=".Length)
    if ([string]::IsNullOrWhiteSpace($token)) {
        return $null
    }

    return $token
}

function Get-GitBasicAuthorizationHeader {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Token
    )

    $pair = "x-access-token:$Token"
    $basic = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($pair))
    return "Authorization: Basic $basic"
}

function Invoke-GitHubJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [string]$Method = "Get",
        [object]$Body,
        [int[]]$AllowedStatusCodes = @(200, 201, 204, 404)
    )

    $uri = if ($Path.StartsWith("https://", [StringComparison]::OrdinalIgnoreCase)) {
        $Path
    }
    else {
        "https://api.github.com/$Path"
    }

    $request = [System.Net.HttpWebRequest]::Create($uri)
    $request.Method = $Method.ToUpperInvariant()
    $request.UserAgent = "AliasCockpitReleasePublisher"
    $request.Accept = "application/vnd.github+json"
    $request.Headers.Add("Authorization", "Bearer $script:GitHubToken")
    $request.Headers.Add("X-GitHub-Api-Version", "2022-11-28")

    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 20
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
        $request.ContentType = "application/json"
        $request.ContentLength = $bytes.Length
        $stream = $request.GetRequestStream()
        try {
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally {
            $stream.Dispose()
        }
    }

    try {
        $response = $request.GetResponse()
        $statusCode = [int]$response.StatusCode
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        try {
            $text = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
            $response.Dispose()
        }
    }
    catch [System.Net.WebException] {
        if ($null -eq $_.Exception.Response) {
            throw
        }

        $response = [System.Net.HttpWebResponse]$_.Exception.Response
        $statusCode = [int]$response.StatusCode
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        try {
            $text = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
            $response.Dispose()
        }
    }

    if ($AllowedStatusCodes -notcontains $statusCode) {
        throw "GitHub API $Method $Path failed with HTTP ${statusCode}: $text"
    }

    if ([string]::IsNullOrWhiteSpace($text)) {
        return [pscustomobject]@{ StatusCode = $statusCode }
    }

    $value = $text | ConvertFrom-Json
    if ($null -eq $value) {
        return [pscustomobject]@{ StatusCode = $statusCode }
    }

    $value | Add-Member -NotePropertyName StatusCode -NotePropertyValue $statusCode -Force
    return $value
}

function Upload-ReleaseAsset {
    param(
        [Parameter(Mandatory = $true)]
        [long]$ReleaseId,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $file = Get-Item -LiteralPath $Path
    $encodedName = [System.Uri]::EscapeDataString($file.Name)
    $uri = "https://uploads.github.com/repos/$Owner/$Repo/releases/$ReleaseId/assets?name=$encodedName"
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)

    $request = [System.Net.HttpWebRequest]::Create($uri)
    $request.Method = "POST"
    $request.UserAgent = "AliasCockpitReleasePublisher"
    $request.Accept = "application/vnd.github+json"
    $request.ContentType = if ($file.Extension -eq ".zip") { "application/zip" } else { "application/octet-stream" }
    $request.Headers.Add("Authorization", "Bearer $script:GitHubToken")
    $request.Headers.Add("X-GitHub-Api-Version", "2022-11-28")
    $request.ContentLength = $bytes.Length

    $stream = $request.GetRequestStream()
    try {
        $stream.Write($bytes, 0, $bytes.Length)
    }
    finally {
        $stream.Dispose()
    }

    try {
        $response = $request.GetResponse()
        $statusCode = [int]$response.StatusCode
        $response.Dispose()
    }
    catch [System.Net.WebException] {
        if ($null -eq $_.Exception.Response) {
            throw
        }

        $response = [System.Net.HttpWebResponse]$_.Exception.Response
        $statusCode = [int]$response.StatusCode
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        try {
            $text = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
            $response.Dispose()
        }

        throw "Release asset upload failed for $($file.Name) with HTTP ${statusCode}: $text"
    }

    if ($statusCode -ne 201) {
        throw "Release asset upload failed for $($file.Name) with HTTP $statusCode."
    }
}

$script:GitHubToken = Get-GitHubToken

foreach ($artifact in $artifacts) {
    if (-not (Test-Path -LiteralPath $artifact)) {
        throw "Missing release artifact: $artifact. Run scripts\verify-release.ps1 first."
    }
}

$user = Invoke-GitHubJson -Path "user" -AllowedStatusCodes @(200)
$repoResult = Invoke-GitHubJson -Path "repos/$Owner/$Repo" -AllowedStatusCodes @(200, 404)
if ($repoResult.StatusCode -eq 404) {
    $body = @{
        name = $Repo
        description = "Windows local email alias cockpit with hard-coded creator metadata and packaged releases."
        private = $false
        auto_init = $false
    }

    if ($user.login -eq $Owner) {
        $repoResult = Invoke-GitHubJson -Path "user/repos" -Method "Post" -Body $body -AllowedStatusCodes @(201)
    }
    else {
        $repoResult = Invoke-GitHubJson -Path "orgs/$Owner/repos" -Method "Post" -Body $body -AllowedStatusCodes @(201)
    }
}

$remoteUrl = "https://github.com/$Owner/$Repo.git"
$existingRemote = (& git remote get-url $RemoteName 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($existingRemote)) {
    & git remote add $RemoteName $remoteUrl
}
elseif ($existingRemote -ne $remoteUrl) {
    & git remote set-url $RemoteName $remoteUrl
}

$gitAuthHeader = Get-GitBasicAuthorizationHeader -Token $script:GitHubToken
& git -c "http.https://github.com/.extraHeader=$gitAuthHeader" push -u $RemoteName $Branch
if ($LASTEXITCODE -ne 0) {
    throw "git push failed."
}

$releaseBody = [System.IO.File]::ReadAllText($releaseNotesPath)
$release = Invoke-GitHubJson -Path "repos/$Owner/$Repo/releases/tags/$Tag" -AllowedStatusCodes @(200, 404)
if ($release.StatusCode -eq 404) {
    $release = Invoke-GitHubJson -Path "repos/$Owner/$Repo/releases" -Method "Post" -Body @{
        tag_name = $Tag
        target_commitish = $Branch
        name = $ReleaseName
        body = $releaseBody
        draft = $false
        prerelease = $false
    } -AllowedStatusCodes @(201)
}
else {
    $release = Invoke-GitHubJson -Path "repos/$Owner/$Repo/releases/$($release.id)" -Method "Patch" -Body @{
        name = $ReleaseName
        body = $releaseBody
        draft = $false
        prerelease = $false
    } -AllowedStatusCodes @(200)
}

$existingAssets = @(Invoke-GitHubJson -Path "repos/$Owner/$Repo/releases/$($release.id)/assets?per_page=100" -AllowedStatusCodes @(200))
foreach ($artifact in $artifacts) {
    $name = Split-Path -Leaf $artifact
    if ($existingAssets | Where-Object { $_.name -eq $name } | Select-Object -First 1) {
        Write-Host "Release asset already exists, skipping: $name"
        continue
    }

    Upload-ReleaseAsset -ReleaseId $release.id -Path $artifact
    Write-Host "Uploaded release asset: $name"
}

Write-Host "Published repository: https://github.com/$Owner/$Repo"
Write-Host "Published release: https://github.com/$Owner/$Repo/releases/tag/$Tag"
