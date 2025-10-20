#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Prepares and submits jaybird to the Windows Package Manager (winget)

.DESCRIPTION
    This script helps prepare winget manifests for jaybird and optionally submits them.
    It downloads the Windows release asset, extracts the SHA256 hash, and creates
    the winget manifest using wingetcreate.

.PARAMETER Version
    The version to prepare (e.g., 1.0.0 - without v prefix)

.PARAMETER Submit
    If specified, submits the manifest to winget-pkgs repository

.PARAMETER Token
    GitHub personal access token for submission (only used with -Submit)

.EXAMPLE
    .\scripts\prepare-winget.ps1 -Version 1.0.0

.EXAMPLE
    .\scripts\prepare-winget.ps1 -Version 1.0.0 -Submit -Token "ghp_..."
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$false)]
    [switch]$Submit,

    [Parameter(Mandatory=$false)]
    [string]$Token
)

# Error handling
$ErrorActionPreference = "Stop"

Write-Host "🚀 Preparing jaybird v$Version for winget submission..." -ForegroundColor Green

# Validate version format (should be semantic version without 'v' prefix)
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "❌ Invalid version format. Use semantic versioning without 'v' prefix (e.g., 1.0.0)" -ForegroundColor Red
    exit 1
}

# Check if wingetcreate is installed
try {
    $wingetcreateVersion = wingetcreate --version 2>$null
    Write-Host "✅ Found wingetcreate version: $wingetcreateVersion" -ForegroundColor Green
} catch {
    Write-Host "⚠️  wingetcreate not found. Installing..." -ForegroundColor Yellow
    try {
        winget install wingetcreate --accept-source-agreements --accept-package-agreements
        Write-Host "✅ wingetcreate installed successfully" -ForegroundColor Green
    } catch {
        Write-Host "❌ Failed to install wingetcreate" -ForegroundColor Red
        Write-Host "Please install manually: winget install wingetcreate" -ForegroundColor Red
        exit 1
    }
}

# Create temporary directory
$tempDir = "temp-winget-$Version"
if (Test-Path $tempDir) {
    Remove-Item -Recurse -Force $tempDir
}
New-Item -ItemType Directory -Path $tempDir | Out-Null

# Construct URLs
$tagName = "v$Version"
$baseUrl = "https://github.com/uncleLukie/jaybird/releases/download/$tagName"
$zipUrl = "$baseUrl/jaybird-$tagName-win-x64.zip"
$hashUrl = "$baseUrl/jaybird-$tagName-win-x64.zip.sha256"

Write-Host "📥 Downloading release assets from $tagName..." -ForegroundColor Blue

# Download Windows x64 zip
try {
    $zipPath = Join-Path $tempDir "jaybird-win-x64.zip"
    Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath
    Write-Host "  ✅ Downloaded Windows x64 zip" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Failed to download Windows x64 zip" -ForegroundColor Red
    Write-Host "  URL: $zipUrl" -ForegroundColor Red
    Write-Host "  Make sure the release exists on GitHub" -ForegroundColor Yellow
    Remove-Item -Recurse -Force $tempDir
    exit 1
}

# Download SHA256 hash file
try {
    $hashPath = Join-Path $tempDir "jaybird-win-x64.zip.sha256"
    Invoke-WebRequest -Uri $hashUrl -OutFile $hashPath
    Write-Host "  ✅ Downloaded SHA256 hash file" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Failed to download SHA256 hash file" -ForegroundColor Red
    Write-Host "  URL: $hashUrl" -ForegroundColor Red
    Remove-Item -Recurse -Force $tempDir
    exit 1
}

# Read and validate SHA256 hash
$hashContent = Get-Content $hashPath -Raw
$sha256 = ($hashContent -split '\s+')[0]

Write-Host "🔐 SHA256 hash: $sha256" -ForegroundColor Cyan

# Validate hash format
if ($sha256 -notmatch '^[a-fA-F0-9]{64}$') {
    Write-Host "❌ Invalid SHA256 hash format: $sha256" -ForegroundColor Red
    Remove-Item -Recurse -Force $tempDir
    exit 1
}

# Create winget manifest
Write-Host "📝 Creating winget manifest..." -ForegroundColor Blue

$manifestArgs = @(
    "new"
    "--id", "uncleLukie.jaybird"
    "--version", $Version
    "--urls", $zipUrl
    "--installer-type", "zip"
    "--nested-installer-type", "portable"
    "--nested-installer-files", "jaybird.exe|jaybird"
    "--publisher", "uncleLukie"
    "--publisher-url", "https://github.com/uncleLukie"
    "--publisher-support-url", "https://github.com/uncleLukie/jaybird/issues"
    "--package-name", "jaybird"
    "--package-url", "https://github.com/uncleLukie/jaybird"
    "--author", "uncleLukie"
    "--moniker", "jaybird"
    "--tags", "cli,radio,discord,music,abc,triplej,doublej,unearthed,streaming,audio"
    "--license", "MIT"
    "--license-url", "https://github.com/uncleLukie/jaybird/blob/main/LICENSE"
    "--copyright", "Copyright (c) uncleLukie"
    "--copyright-url", "https://github.com/uncleLukie/jaybird"
    "--short-description", "Discord Rich Presence-enabled CLI player for Australian ABC radio stations"
    "--description", "jaybird is a Discord Rich Presence-enabled CLI player for Australian ABC radio stations (Triple J, Double J, and Unearthed). It's a .NET 10 C# console application that streams AAC+ audio, displays currently playing songs, and updates Discord status."
    "--release-notes", "See release notes at https://github.com/uncleLukie/jaybird/releases/tag/$tagName"
    "--release-notes-url", "https://github.com/uncleLukie/jaybird/releases/tag/$tagName"
    "--out", "manifests"
    "--format"
    "--submit:false"
)

try {
    & wingetcreate @manifestArgs
    if ($LASTEXITCODE -ne 0) {
        throw "wingetcreate exited with code $LASTEXITCODE"
    }
    Write-Host "✅ Manifest created successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to create manifest:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Remove-Item -Recurse -Force $tempDir
    exit 1
}

# Validate manifest
Write-Host "🔍 Validating manifest..." -ForegroundColor Blue
try {
    & wingetcreate validate manifests
    if ($LASTEXITCODE -ne 0) {
        throw "Validation failed with exit code $LASTEXITCODE"
    }
    Write-Host "✅ Manifest validation passed" -ForegroundColor Green
} catch {
    Write-Host "❌ Manifest validation failed:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Remove-Item -Recurse -Force $tempDir
    exit 1
}

# Display manifest files
Write-Host "`n📁 Generated manifest files:" -ForegroundColor Blue
Get-ChildItem -Path manifests -Recurse -File | ForEach-Object {
    Write-Host "`n  === $($_.Name) ===" -ForegroundColor Cyan
    Get-Content $_.FullName | ForEach-Object { Write-Host "  $_" }
}

# Submit if requested
if ($Submit) {
    if ([string]::IsNullOrEmpty($Token)) {
        Write-Host "`n❌ Token required for submission." -ForegroundColor Red
        Write-Host "Use -Token parameter or set GITHUB_TOKEN environment variable." -ForegroundColor Yellow
        Remove-Item -Recurse -Force $tempDir
        exit 1
    }

    Write-Host "`n📤 Submitting manifest to winget-pkgs..." -ForegroundColor Blue
    try {
        & wingetcreate submit --token $Token manifests
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Manifest submitted successfully!" -ForegroundColor Green
            Write-Host "🔗 Track your submission at: https://github.com/microsoft/winget-pkgs/pulls" -ForegroundColor Blue
        } else {
            throw "Submission failed with exit code $LASTEXITCODE"
        }
    } catch {
        Write-Host "❌ Submission failed:" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        Remove-Item -Recurse -Force $tempDir
        exit 1
    }
} else {
    Write-Host "`n✅ Manifest prepared successfully!" -ForegroundColor Green
    Write-Host "📁 Manifests located in: manifests\u\uncleLukie\jaybird\$Version" -ForegroundColor Blue
    Write-Host "`n💡 To submit manually:" -ForegroundColor Yellow
    Write-Host "   1. Fork https://github.com/microsoft/winget-pkgs" -ForegroundColor Yellow
    Write-Host "   2. Copy manifests to the correct directory structure" -ForegroundColor Yellow
    Write-Host "   3. Create a pull request" -ForegroundColor Yellow
    Write-Host "`n💡 To submit automatically, run with:" -ForegroundColor Yellow
    Write-Host "   .\scripts\prepare-winget.ps1 -Version $Version -Submit -Token <github-token>" -ForegroundColor Yellow
}

# Cleanup
Write-Host "`n🧹 Cleaning up temporary files..." -ForegroundColor Blue
Remove-Item -Recurse -Force $tempDir
Write-Host "✅ Cleanup complete" -ForegroundColor Green

Write-Host "`n🎉 Done!" -ForegroundColor Green
