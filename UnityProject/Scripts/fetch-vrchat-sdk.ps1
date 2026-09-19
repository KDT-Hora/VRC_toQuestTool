<#
.SYNOPSIS
  Downloads VRChat SDK3 - Base and SDK3 - Avatars and extracts them as embedded local packages
  under Packages/, since they are proprietary and cannot be committed to this repo (see
  UnityProject/.gitignore) and are not resolvable via a standard Unity scoped registry (VRChat
  distributes them as VPM zip release assets, normally installed via VRChat Creator Companion).

.NOTES
  Re-run this after cloning the repo, or whenever bumping the pinned version in
  specs/001-quest-avatar-converter/research.md §2.
#>

param(
    [string]$Version = "3.10.5"
)

$ErrorActionPreference = "Stop"
$packagesDir = Join-Path $PSScriptRoot "..\Packages"

function Install-VrcPackage($Name) {
    $url = "https://github.com/vrchat/packages/releases/download/$Version/$Name-$Version.zip"
    $zipPath = Join-Path $env:TEMP "$Name-$Version.zip"
    $destDir = Join-Path $packagesDir $Name

    Write-Host "Downloading $Name $Version ..."
    Invoke-WebRequest -Uri $url -OutFile $zipPath

    if (Test-Path $destDir) {
        Remove-Item -Recurse -Force $destDir
    }
    New-Item -ItemType Directory -Path $destDir | Out-Null

    Write-Host "Extracting to $destDir ..."
    Expand-Archive -Path $zipPath -DestinationPath $destDir -Force
    Remove-Item $zipPath
}

Install-VrcPackage "com.vrchat.base"
Install-VrcPackage "com.vrchat.avatars"

Write-Host "Done. VRChat SDK3 $Version installed as embedded packages."
