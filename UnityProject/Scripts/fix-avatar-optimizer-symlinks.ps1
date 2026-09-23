<#
.SYNOPSIS
  Repairs com.anatawa12.avatar-optimizer's internal csc.rsp/.ruleset symlinked files after Unity
  resolves it as a git-URL package, on machines/Unity versions where they come out missing or
  broken.

.DESCRIPTION
  AAO ships several `csc.rsp` / `AvatarOptimizer.ruleset` files as real symlinks in its git repo
  (pointing back to `.csc.rsp.nullsafe` / `.AvatarOptimizer.ruleset` at the package root). Unity's
  own internal git-package resolver does not reliably materialize these as real symlinks on
  Windows -- even with Developer Mode on and `core.symlinks=true` (verified: a plain `git clone`
  of the same repo with the system `git.exe` DOES create correct symlinks; Unity's resolver still
  drops them). The result is compile errors (CS2001/CS8035) from Library/PackageCache, because the
  symlinked files are either missing entirely or checked out as a plain-text placeholder
  containing just the link target path.

  This script clones AAO fresh with the system git (which handles the symlinks correctly) and
  copies the *resolved* (dereferenced) file content over the broken/missing paths under
  UnityProject/Library/PackageCache/com.anatawa12.avatar-optimizer@*/.

.NOTES
  Re-run this any time `unity test`/`unity run` reports compile errors originating from
  Library/PackageCache/com.anatawa12.avatar-optimizer@*/ mentioning a missing `.csc.rsp.nullsafe`
  source file or an invalid `AvatarOptimizer.ruleset` ("Data at the root level is invalid").
  See tasks.md's "Setup checkpoint verified" note for the full root-cause writeup.
#>

param(
    [string]$Version = "v1.9.19"
)

$ErrorActionPreference = "Stop"
$packageCacheGlob = Join-Path $PSScriptRoot "..\Library\PackageCache\com.anatawa12.avatar-optimizer@*"
$destRoots = Get-ChildItem -Path $packageCacheGlob -Directory -ErrorAction SilentlyContinue

if (-not $destRoots) {
    Write-Host "No com.anatawa12.avatar-optimizer@* folder found under Library/PackageCache -- nothing to repair (has the project been opened/resolved yet?)."
    exit 0
}

$tempClone = Join-Path $env:TEMP "aao-symlink-fix-$([guid]::NewGuid().ToString('N'))"
Write-Host "Cloning anatawa12/AvatarOptimizer $Version with system git to resolve real symlinks..."
git clone --quiet --depth 1 --branch $Version https://github.com/anatawa12/AvatarOptimizer.git $tempClone

try {
    $symlinks = Get-ChildItem -Path $tempClone -Recurse -Force -File | Where-Object {
        $_.LinkType -eq "SymbolicLink" -and $_.FullName -notmatch '\\\.docs\\' -and $_.FullName -notmatch '\\Test~\\'
    }

    foreach ($destRoot in $destRoots) {
        Write-Host "Repairing symlinked files under: $($destRoot.FullName)"
        $count = 0
        foreach ($link in $symlinks) {
            $rel = $link.FullName.Substring($tempClone.Length + 1)
            $destFile = Join-Path $destRoot.FullName $rel
            $destDir = Split-Path $destFile -Parent
            if (-not (Test-Path $destDir)) {
                New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            }
            # Copy the resolved (dereferenced) real content, overwriting any broken placeholder
            # text file or filling in a missing file.
            Copy-Item -Path $link.FullName -Destination $destFile -Force
            $count++
            Write-Host "  fixed: $rel"
        }
        Write-Host "Fixed $count file(s) under $($destRoot.FullName)"
    }
} finally {
    Remove-Item -Recurse -Force $tempClone -ErrorAction SilentlyContinue
}

Write-Host "Done. Re-run 'unity test'/'unity run' to confirm the compile errors are gone."
