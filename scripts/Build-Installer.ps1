#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'IPChecker\IPChecker.csproj'
$publishDir = Join-Path $repoRoot 'dist\publish\win-x64'
$distRoot = Join-Path $repoRoot 'dist'
$redistDir = Join-Path $distRoot 'redist'
$vcRedistPath = Join-Path $redistDir 'vc_redist.x64.exe'
$vcRedistUrl = 'https://aka.ms/vs/17/release/vc_redist.x64.exe'
$issPath = Join-Path $repoRoot 'installer\IPChecker.iss'

function Stop-RunningApp {
    Write-Host 'Stopping IPChecker if running...'
    Stop-Process -Name IPChecker -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

function Get-ProjectVersion {
    $propertyGroups = ([xml](Get-Content $projectPath -Raw)).Project.PropertyGroup
    foreach ($group in $propertyGroups) {
        if ($group.Version) {
            return [string]$group.Version
        }
    }

    return '1.0.0'
}

function Remove-OldDistArtifacts {
    param(
        [string]$Version
    )

    Get-ChildItem $distRoot -Filter 'IPChecker-Setup-*-x64.exe' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne "IPChecker-Setup-$Version-x64.exe" } |
        Remove-Item -Force

    Get-ChildItem $distRoot -Filter 'IPChecker-*-x64-portable.zip' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne "IPChecker-$Version-x64-portable.zip" } |
        Remove-Item -Force
}

function New-PortableZip {
    param(
        [string]$Version
    )

    $zipPath = Join-Path $distRoot "IPChecker-$Version-x64-portable.zip"
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
    return $zipPath
}

function Ensure-VcRedist {
    if (Test-Path $vcRedistPath) {
        Write-Host "VC++ redist already present: $vcRedistPath"
        return
    }

    New-Item -ItemType Directory -Force -Path $redistDir | Out-Null
    Write-Host 'Downloading Visual C++ redistributable (x64)...'
    Invoke-WebRequest -Uri $vcRedistUrl -OutFile $vcRedistPath -UseBasicParsing
    Write-Host "VC++ redist saved: $vcRedistPath"
}

function Get-InnoSetupCompiler {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    foreach ($path in $candidates) {
        if (Test-Path $path) {
            return $path
        }
    }

    return $null
}

function Install-InnoSetup {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        return $null
    }

    Write-Host 'Inno Setup not found. Installing via winget...'
    $null = winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements 2>&1
    return Get-InnoSetupCompiler
}

Write-Host '=== IP Checker installer build ==='

Stop-RunningApp
Ensure-VcRedist

$version = Get-ProjectVersion
Remove-OldDistArtifacts -Version $version

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

Write-Host 'Publishing Release (self-contained)...'
dotnet publish $projectPath `
    -c Release `
    -p:Platform=x64 `
    /p:PublishProfile=win-x64 `
    -o $publishDir

if (-not (Test-Path (Join-Path $publishDir 'IPChecker.exe'))) {
    throw "Publish failed: IPChecker.exe not found in $publishDir"
}

$sizeMb = [math]::Round((Get-ChildItem $publishDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "Publish done: $publishDir ($sizeMb MB)"

Write-Host 'Creating portable ZIP...'
$zipPath = New-PortableZip -Version $version
$zipSizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "Portable ZIP: $zipPath ($zipSizeMb MB)"

$iscc = Get-InnoSetupCompiler
if (-not $iscc) {
    $iscc = Install-InnoSetup
}

if ($iscc) {
    Write-Host 'Building setup.exe with Inno Setup...'
    & $iscc $issPath
    $setupExe = Get-ChildItem (Join-Path $distRoot "IPChecker-Setup-$version-x64.exe") -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $setupExe) {
        $setupExe = Get-ChildItem (Join-Path $distRoot 'IPChecker-Setup-*-x64.exe') |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
    }

    if ($setupExe) {
        Write-Host ''
        Write-Host '=== Done ==='
        Write-Host "Installer: $($setupExe.FullName)"
        Write-Host "Size: $([math]::Round($setupExe.Length / 1MB, 1)) MB"
        Write-Host "Portable: $zipPath"
        exit 0
    }

    throw 'Inno Setup build finished but setup.exe was not found.'
}

Write-Host ''
Write-Host 'Inno Setup is unavailable. Portable ZIP was created.'
Write-Host "Portable: $zipPath"
Write-Host ''
Write-Host 'To build setup.exe, install Inno Setup 6 and rerun:'
Write-Host '  winget install --id JRSoftware.InnoSetup -e'
Write-Host '  .\scripts\Build-Installer.ps1'
