<#
.SYNOPSIS
    Builds the File Backup MSI installer.

.DESCRIPTION
    1. Publishes the application as self-contained (no .NET runtime required on target machine).
    2. Generates WiX file authoring from the publish output.
    3. Builds the WiX installer project to produce the .msi file.

.PARAMETER Configuration
    Build configuration. Default: Release.

.PARAMETER OutputDir
    Where to copy the final .msi. Default: installer\bin.

.EXAMPLE
    .\build-installer.ps1
    .\build-installer.ps1 -Configuration Debug
#>

param(
    [string]$Configuration = "Release",
    [string]$OutputDir     = ""
)

$ErrorActionPreference = "Stop"

# ── Ensure dotnet is on PATH ───────────────────────────────────────────────
$dotnetExe = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetExe) {
    $candidates = @(
        "$env:ProgramFiles\dotnet\dotnet.exe",
        "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $dotnetExe = $c; break }
    }
    if (-not $dotnetExe) {
        Write-Error "dotnet SDK not found. Install from https://dot.net"
        exit 1
    }
    $env:PATH = "$([System.IO.Path]::GetDirectoryName($dotnetExe));$env:PATH"
    Write-Host "  Resolved dotnet at: $dotnetExe" -ForegroundColor DarkGray
}

# ── Paths ───────────────────────────────────────────────────────────────────
$Root       = Split-Path -Parent $PSScriptRoot
$AppProject = Join-Path $Root "src\klst-backup\klst-backup.csproj"
$WixProject = Join-Path $Root "installer\klst-backup.Installer.wixproj"
$PublishDir = Join-Path $Root "src\klst-backup\bin\publish\"
$HarvestWxs = Join-Path $Root "installer\HarvestedFiles.wxs"

if (-not $OutputDir) {
    $OutputDir = Join-Path $Root "installer\bin\$Configuration"
}

# ── Step 1: Publish self-contained ─────────────────────────────────────────
Write-Host "==> Publishing application (self-contained, win-x64)..." -ForegroundColor Cyan
dotnet publish $AppProject -c $Configuration -r win-x64 --self-contained true -o $PublishDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# ── Step 2: Generate WiX file authoring from publish output ────────────────
Write-Host "==> Generating WiX file authoring..." -ForegroundColor Cyan

$md5 = [System.Security.Cryptography.MD5]::Create()
$files = Get-ChildItem -Path $PublishDir -Recurse -File

# Build directory tree from file paths
$tree = @{}
foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($PublishDir.Length)
    $dir = Split-Path -Parent $relativePath
    if (-not $dir) { $dir = '' }
    if (-not $tree.ContainsKey($dir)) { $tree[$dir] = @() }
    $tree[$dir] += [PSCustomObject]@{
        Name = $file.Name
        FullPath = $file.FullName
        RelativePath = $relativePath
    }
}

# Helper: deterministic directory ID from relative path
function Get-DirId($relDir) {
    if (-not $relDir) { return 'AppFolder' }
    return 'dir_' + ($relDir -replace '[\\.\-]', '_')
}

# Helper: write directory tree recursively
function Write-DirTree($currentPath, $indent) {
    $children = $tree.Keys | Where-Object {
        if (-not $currentPath) {
            # Root-level children: directories with no further separator
            $_ -ne '' -and ($_ -notmatch '[\\]')
        } else {
            $_ -ne $currentPath -and $_.StartsWith($currentPath + '\') -and
            ($_.Substring($currentPath.Length + 1) -notmatch '[\\]')
        }
    }
    foreach ($child in $children) {
        $dirId = Get-DirId $child
        $dirName = if ($currentPath) { $child.Substring($currentPath.Length + 1) } else { $child }
        [void]$sb.AppendLine("$indent<Directory Id=`"$dirId`" Name=`"$dirName`">")
        # Write components for this directory
        if ($tree.ContainsKey($child)) {
            foreach ($f in $tree[$child]) {
                $componentId = "cmp_" + ($f.RelativePath -replace '[\\.\-]', '_')
                $fileId      = "fil_" + $componentId.Substring(4)
                $hash = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($f.RelativePath))
                $guid = [Guid]::new($hash).ToString()
                $allComponentIds.Add($componentId) | Out-Null
                [void]$sb.AppendLine("$indent  <Component Id=`"$componentId`" Guid=`"$guid`">")
                [void]$sb.AppendLine("$indent    <File Id=`"$fileId`" Source=`"$($f.FullPath)`" Name=`"$($f.Name)`" />")
                [void]$sb.AppendLine("$indent  </Component>")
            }
        }
        Write-DirTree $child "$indent  "
        [void]$sb.AppendLine("$indent</Directory>")
    }
}

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')

# Collect all component IDs for the ComponentGroup
$allComponentIds = [System.Collections.ArrayList]::new()

# Fragment 1: Directory structure with components
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <DirectoryRef Id="AppFolder">')

# Root-level files
if ($tree.ContainsKey('')) {
    foreach ($f in $tree['']) {
        $componentId = "cmp_" + ($f.RelativePath -replace '[\\.\-]', '_')
        $fileId      = "fil_" + $componentId.Substring(4)
        $hash = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($f.RelativePath))
        $guid = [Guid]::new($hash).ToString()
        $allComponentIds.Add($componentId) | Out-Null
        [void]$sb.AppendLine("      <Component Id=`"$componentId`" Guid=`"$guid`">")
        [void]$sb.AppendLine("        <File Id=`"$fileId`" Source=`"$($f.FullPath)`" Name=`"$($f.Name)`" />")
        [void]$sb.AppendLine("      </Component>")
    }
}

# Subdirectory files: nest Directory elements
Write-DirTree '' '      '

[void]$sb.AppendLine('    </DirectoryRef>')
[void]$sb.AppendLine('  </Fragment>')

# Fragment 2: ComponentGroup referencing all components
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <ComponentGroup Id="AppHarvestedComponents">')
foreach ($cid in $allComponentIds) {
    [void]$sb.AppendLine("      <ComponentRef Id=`"$cid`" />")
}
[void]$sb.AppendLine('    </ComponentGroup>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('</Wix>')

[System.IO.File]::WriteAllText($HarvestWxs, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Host "  Generated $($files.Count) components in HarvestedFiles.wxs" -ForegroundColor DarkGray

# ── Step 3: Build MSI ──────────────────────────────────────────────────────
Write-Host "==> Building MSI installer..." -ForegroundColor Cyan
dotnet build $WixProject -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet build (WiX) failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# ── Step 4: Copy to output ─────────────────────────────────────────────────
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$msiFiles = Get-ChildItem -Path (Join-Path $Root "installer\bin") -Recurse -Filter "*.msi"
foreach ($msi in $msiFiles) {
    $dest = Join-Path $OutputDir $msi.Name
    if ($msi.FullName -ne $dest) {
        Copy-Item $msi.FullName -Destination $OutputDir -Force
    }
    Write-Host "==> MSI: $dest" -ForegroundColor Green
}

# ── Cleanup: remove generated .wxs (it's a build artifact) ────────────────
Remove-Item $HarvestWxs -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Output: $OutputDir" -ForegroundColor Green
