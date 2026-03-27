#!/usr/bin/env bash
# =============================================================================
# Godot SmartThings Migration — Dev Environment Setup
# Target: Ubuntu 22.04+ / Debian-based (also works on WSL2 for Windows dev)
# Run as: bash setup-dev-environment.sh
# =============================================================================
set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log()   { echo -e "${GREEN}[✓]${NC} $1"; }
warn()  { echo -e "${YELLOW}[!]${NC} $1"; }
err()   { echo -e "${RED}[✗]${NC} $1"; }

GODOT_VERSION="4.5-stable"
DOTNET_VERSION="8.0"
PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "============================================="
echo "  Godot SmartThings Migration Setup"
echo "  Target: Godot ${GODOT_VERSION} + .NET ${DOTNET_VERSION}"
echo "============================================="
echo ""

# --- Step 1: System packages ---
log "Installing system dependencies..."
sudo apt-get update -qq
sudo apt-get install -y \
    build-essential \
    scons \
    pkg-config \
    libx11-dev libxcursor-dev libxinerama-dev libxi-dev libxrandr-dev \
    libgl1-mesa-dev \
    libasound2-dev \
    libpulse-dev \
    libfreetype-dev \
    libvulkan-dev \
    libdbus-1-dev \
    libudev-dev \
    libspeechd-dev \
    git \
    curl \
    wget \
    unzip

# --- Step 2: .NET 8 SDK ---
if ! command -v dotnet &> /dev/null || ! dotnet --list-sdks | grep -q "^8\."; then
    log "Installing .NET ${DOTNET_VERSION} SDK..."
    wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
    chmod +x /tmp/dotnet-install.sh
    /tmp/dotnet-install.sh --channel ${DOTNET_VERSION} --install-dir "$HOME/.dotnet"

    # Add to PATH if not already there
    if ! grep -q '\.dotnet' "$HOME/.bashrc"; then
        echo 'export DOTNET_ROOT=$HOME/.dotnet' >> "$HOME/.bashrc"
        echo 'export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools' >> "$HOME/.bashrc"
    fi
    export DOTNET_ROOT="$HOME/.dotnet"
    export PATH="$PATH:$HOME/.dotnet:$HOME/.dotnet/tools"
    log ".NET $(dotnet --version) installed."
else
    log ".NET SDK already installed: $(dotnet --version)"
fi

# --- Step 3: Clone Godot (if not already present) ---
GODOT_DIR="${PROJECT_ROOT}/godot-smartthings"
if [ ! -d "$GODOT_DIR" ]; then
    log "Cloning Godot ${GODOT_VERSION}..."
    git clone --depth 1 --branch ${GODOT_VERSION} \
        https://github.com/godotengine/godot.git "$GODOT_DIR"
    log "Godot source cloned to ${GODOT_DIR}"
else
    log "Godot source already exists at ${GODOT_DIR}"
fi

# --- Step 4: Build Godot with .NET support ---
log "Building Godot with .NET/C# support..."
cd "$GODOT_DIR"

# Generate the .NET glue
scons platform=linuxbsd target=editor module_mono_enabled=yes \
    dotnet_version=${DOTNET_VERSION} -j$(nproc) 2>&1 | tee "${PROJECT_ROOT}/logs/godot-build.log"

# Build the .NET solution
log "Generating Godot .NET assemblies..."
./bin/godot.linuxbsd.editor.x86_64.mono --headless --generate-mono-glue modules/mono/glue
cd modules/mono
python3 build_assemblies.py --godot-output-dir=../../bin

log "Godot editor built successfully!"

# --- Step 5: Install Chickensoft tools ---
log "Installing Chickensoft GodotEnv..."
dotnet tool install --global Chickensoft.GodotEnv

# --- Step 6: Android SDK (optional, for mobile export) ---
if [ "${SETUP_ANDROID:-false}" = "true" ]; then
    log "Setting up Android SDK for Godot export..."
    ANDROID_HOME="$HOME/Android/Sdk"
    mkdir -p "$ANDROID_HOME"

    CMDLINE_TOOLS_URL="https://dl.google.com/android/repository/commandlinetools-linux-latest.zip"
    wget -q "$CMDLINE_TOOLS_URL" -O /tmp/cmdline-tools.zip
    unzip -q /tmp/cmdline-tools.zip -d "$ANDROID_HOME/cmdline-tools"
    mv "$ANDROID_HOME/cmdline-tools/cmdline-tools" "$ANDROID_HOME/cmdline-tools/latest"

    yes | "$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager" \
        "platform-tools" \
        "platforms;android-34" \
        "build-tools;34.0.0" \
        "ndk;26.1.10909125"

    echo "export ANDROID_HOME=$ANDROID_HOME" >> "$HOME/.bashrc"
    echo 'export PATH=$PATH:$ANDROID_HOME/cmdline-tools/latest/bin:$ANDROID_HOME/platform-tools' >> "$HOME/.bashrc"
    log "Android SDK installed."
else
    warn "Skipping Android SDK setup. Run with SETUP_ANDROID=true to install."
fi

# --- Step 7: Create project solution ---
log "Setting up .NET solution..."
cd "$PROJECT_ROOT/src"

if [ ! -f "SmartThings.Migration.sln" ]; then
    dotnet new sln -n SmartThings.Migration
    dotnet new classlib -n SmartThings.Abstraction -f net8.0 --force
    dotnet new classlib -n SmartThings.Godot -f net8.0 --force
    dotnet new xunit -n SmartThings.Tests -f net8.0 --force

    dotnet sln SmartThings.Migration.sln add \
        SmartThings.Abstraction/SmartThings.Abstraction.csproj \
        SmartThings.Godot/SmartThings.Godot.csproj \
        SmartThings.Tests/SmartThings.Tests.csproj

    dotnet add SmartThings.Godot reference SmartThings.Abstraction
    dotnet add SmartThings.Tests reference SmartThings.Abstraction SmartThings.Godot

    # Add key NuGet packages
    dotnet add SmartThings.Godot package MQTTnet --version 4.*
    dotnet add SmartThings.Godot package Microsoft.Extensions.DependencyInjection
    dotnet add SmartThings.Godot package Microsoft.ML.OnnxRuntime  # For Silero VAD
    dotnet add SmartThings.Tests package Chickensoft.GoDotTest
fi

log "Solution created."

# --- Step 8: Install FBX2glTF ---
log "Installing FBX2glTF for asset conversion..."
FBX2GLTF_URL="https://github.com/godotengine/FBX2glTF/releases/latest/download/FBX2glTF-linux-x86_64.zip"
wget -q "$FBX2GLTF_URL" -O /tmp/fbx2gltf.zip
unzip -qo /tmp/fbx2gltf.zip -d "$HOME/.local/bin/"
chmod +x "$HOME/.local/bin/FBX2glTF"
log "FBX2glTF installed."

# --- Summary ---
echo ""
echo "============================================="
echo "  Setup Complete!"
echo "============================================="
echo ""
echo "  Godot source:     ${GODOT_DIR}"
echo "  .NET SDK:          $(dotnet --version 2>/dev/null || echo 'check PATH')"
echo "  Project solution:  ${PROJECT_ROOT}/src/SmartThings.Migration.sln"
echo ""
echo "  Next steps:"
echo "    1. Open Godot editor:  ${GODOT_DIR}/bin/godot.linuxbsd.editor.x86_64.mono"
echo "    2. Create a Godot project in src/SmartThings.Godot/"
echo "    3. Run tests:  cd src && dotnet test"
echo ""
echo "  For Android builds:  SETUP_ANDROID=true bash scripts/setup-dev-environment.sh"
echo "============================================="
