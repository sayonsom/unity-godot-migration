# =============================================================================
# Godot SmartThings Migration — Windows Dev Environment Setup
# Requires: PowerShell 7+, winget, Visual Studio 2022
# Run as Administrator: powershell -ExecutionPolicy Bypass -File setup-windows.ps1
# =============================================================================

$ErrorActionPreference = "Stop"
$GodotVersion = "4.5-stable"
$DotNetVersion = "8.0"
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Godot SmartThings Migration Setup (Windows)" -ForegroundColor Cyan
Write-Host "  Target: Godot $GodotVersion + .NET $DotNetVersion" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# --- Step 1: Install via winget ---
Write-Host "`n[1/6] Installing core tools via winget..." -ForegroundColor Green

$packages = @(
    "Microsoft.DotNet.SDK.8",
    "Git.Git",
    "Python.Python.3.11",
    "Kitware.CMake"
)

foreach ($pkg in $packages) {
    Write-Host "  Installing $pkg..."
    winget install --id $pkg --accept-package-agreements --accept-source-agreements -e 2>$null
}

# SCons via pip
Write-Host "  Installing SCons..."
pip install scons

# --- Step 2: Visual Studio Build Tools ---
Write-Host "`n[2/6] Checking Visual Studio..." -ForegroundColor Green
$vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vsWhere) {
    $vsPath = & $vsWhere -latest -property installationPath
    Write-Host "  Visual Studio found at: $vsPath"
} else {
    Write-Host "  WARNING: Visual Studio not found. Install VS 2022 with C++ desktop workload." -ForegroundColor Yellow
    Write-Host "  winget install Microsoft.VisualStudio.2022.Community"
}

# --- Step 3: Clone Godot ---
Write-Host "`n[3/6] Cloning Godot..." -ForegroundColor Green
$GodotDir = Join-Path $ProjectRoot "godot-smartthings"
if (-not (Test-Path $GodotDir)) {
    git clone --depth 1 --branch $GodotVersion https://github.com/godotengine/godot.git $GodotDir
} else {
    Write-Host "  Godot already cloned."
}

# --- Step 4: Build Godot ---
Write-Host "`n[4/6] Building Godot with .NET support..." -ForegroundColor Green
Push-Location $GodotDir

# Use VS developer command prompt
$vcvarsall = Get-ChildItem "${env:ProgramFiles}\Microsoft Visual Studio" -Recurse -Filter "vcvarsall.bat" | Select-Object -First 1
if ($vcvarsall) {
    cmd /c "`"$($vcvarsall.FullName)`" amd64 && scons platform=windows target=editor module_mono_enabled=yes dotnet_version=$DotNetVersion -j$env:NUMBER_OF_PROCESSORS"
}

# Generate .NET glue
& ".\bin\godot.windows.editor.x86_64.mono.exe" --headless --generate-mono-glue modules/mono/glue
Push-Location modules/mono
python build_assemblies.py --godot-output-dir=../../bin
Pop-Location
Pop-Location

Write-Host "  Godot built successfully!" -ForegroundColor Green

# --- Step 5: Setup .NET Solution ---
Write-Host "`n[5/6] Setting up .NET solution..." -ForegroundColor Green
Push-Location (Join-Path $ProjectRoot "src")

if (-not (Test-Path "SmartThings.Migration.sln")) {
    dotnet new sln -n SmartThings.Migration
    dotnet sln add SmartThings.Abstraction SmartThings.Godot SmartThings.Tests
    dotnet add SmartThings.Godot reference SmartThings.Abstraction
    dotnet add SmartThings.Tests reference SmartThings.Abstraction SmartThings.Godot

    dotnet add SmartThings.Godot package MQTTnet
    dotnet add SmartThings.Godot package Microsoft.Extensions.DependencyInjection
    dotnet add SmartThings.Godot package Microsoft.ML.OnnxRuntime
}
Pop-Location

# --- Step 6: Android SDK (optional) ---
Write-Host "`n[6/6] Android SDK..." -ForegroundColor Green
if ($env:SETUP_ANDROID -eq "true") {
    Write-Host "  Setting up Android SDK..."
    # Use Android Studio's SDK manager or sdkmanager CLI
} else {
    Write-Host "  Skipped. Set `$env:SETUP_ANDROID='true' to install." -ForegroundColor Yellow
}

Write-Host "`n=============================================" -ForegroundColor Cyan
Write-Host "  Setup Complete!" -ForegroundColor Cyan
Write-Host "  Godot:    $GodotDir\bin\godot.windows.editor.x86_64.mono.exe" -ForegroundColor White
Write-Host "  Solution: $ProjectRoot\src\SmartThings.Migration.sln" -ForegroundColor White
Write-Host "=============================================" -ForegroundColor Cyan
