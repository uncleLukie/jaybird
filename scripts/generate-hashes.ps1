#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Generates SHA256 hashes for release assets
    
.DESCRIPTION
    This script generates SHA256 hashes for zip files, which are required
    for winget manifest submission.
    
.PARAMETER InputPath
    Path to the file or directory to hash
    
.PARAMETER Version
    Version string for output file naming
    
.EXAMPLE
    .\scripts\generate-hashes.ps1 -InputPath "release-assets" -Version "1.0.0"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$InputPath,
    
    [Parameter(Mandatory=$false)]
    [string]$Version = "latest"
)

$ErrorActionPreference = "Stop"

function Get-FileHashSimple {
    param(
        [string]$FilePath,
        [string]$Algorithm = "SHA256"
    )
    
    $hash = Get-FileHash -Path $FilePath -Algorithm $Algorithm
    return "$($hash.Hash.ToLower())  $(Split-Path $FilePath -Leaf)"
}

if (Test-Path $InputPath -PathType Container) {
    # Directory - process all zip files
    $zipFiles = Get-ChildItem -Path $InputPath -Filter "*.zip"
    
    foreach ($zipFile in $zipFiles) {
        $hashFile = "$($zipFile.FullName).sha256"
        $hash = Get-FileHashSimple -FilePath $zipFile.FullName
        $hash | Out-File -FilePath $hashFile -Encoding UTF8
        Write-Host "✅ Generated hash for $($zipFile.Name)" -ForegroundColor Green
        Write-Host "   $hash" -ForegroundColor Cyan
    }
} elseif (Test-Path $InputPath -PathType Leaf) {
    # Single file
    $hashFile = "$InputPath.sha256"
    $hash = Get-FileHashSimple -FilePath $InputPath
    $hash | Out-File -FilePath $hashFile -Encoding UTF8
    Write-Host "✅ Generated hash for $(Split-Path $InputPath -Leaf)" -ForegroundColor Green
    Write-Host "   $hash" -ForegroundColor Cyan
} else {
    Write-Host "❌ Path not found: $InputPath" -ForegroundColor Red
    exit 1
}

Write-Host "🎉 Hash generation complete!" -ForegroundColor Green