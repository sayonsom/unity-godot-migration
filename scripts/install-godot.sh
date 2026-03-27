#!/usr/bin/env bash
# =============================================================================
# install-godot.sh — Clone, build, and configure Godot 4.5 with C# support
# Run: chmod +x scripts/install-godot.sh && ./scripts/install-godot.sh
# Prerequisites: install-base.sh + install-dotnet.sh
# =============================================================================

set -euo pipefail

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

log()  { echo -e "${GREEN}[✓]${NC} $1"; }
warn() { echo -e "${YELLOW}[!]${NC} $1"; }
err()  { echo -e "${RED}[✗]${NC} $1"; exit 1; }

GODOT_VERSION="4.5-stable"
GODOT_DIR="${GODOT_SMARTTHINGS_ROOT:-$HOME/godot-smartthings}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
JOBS=$(nproc 2>/dev/null || echo 4)

echo "============================================="
echo "  Godot ${GODOT_VERSION} Build (C# / .NET)"
echo "  Target: ${GODOT_DIR}"
echo "  Parallel jobs: ${JOBS}"
echo "============================================="

# --- Verify prerequisites ---
for cmd in git scons python3 g++ dotnet; do
  if ! command -v "$cmd" &>/dev/null; then
    err "$cmd not found. Run install-base.sh and install-dotnet.sh first."
  fi
done
log "Prerequisites verified: git, scons, python3, g++, dotnet"

# --- Clone Godot ---
if [ -d "$GODOT_DIR/.git" ]; then
  log "Godot already cloned at $GODOT_DIR"
  cd "$GODOT_DIR"
  CURRENT_TAG=$(git describe --tags 2>/dev/null || echo "unknown")
  log "Current version: $CURRENT_TAG"
else
  log "Cloning Godot ${GODOT_VERSION}..."
  git clone --depth 1 --branch "$GODOT_VERSION" \
    https://github.com/godotengine/godot.git "$GODOT_DIR"
  cd "$GODOT_DIR"
  log "Godot cloned to $GODOT_DIR"
fi

# --- Detect platform ---
PLATFORM="linuxbsd"
ARCH=$(uname -m)
case "$ARCH" in
  x86_64)  ARCH_FLAG="x86_64" ;;
  aarch64) ARCH_FLAG="arm64" ;;
  *)       warn "Unknown arch: $ARCH. Defaulting to x86_64."; ARCH_FLAG="x86_64" ;;
esac
log "Building for: ${PLATFORM} / ${ARCH_FLAG}"

# --- Build Godot Editor with .NET ---
log "Building Godot editor with C# support (this will take 15-30 minutes)..."
scons \
  platform="$PLATFORM" \
  target=editor \
  module_mono_enabled=yes \
  arch="$ARCH_FLAG" \
  -j"$JOBS" \
  2>&1 | tee /tmp/godot-build.log

EDITOR_BIN=$(find bin/ -name "godot.${PLATFORM}.editor.*mono*" -type f | head -1)
if [ -z "$EDITOR_BIN" ]; then
  err "Build failed. Check /tmp/godot-build.log"
fi
log "Editor built: $EDITOR_BIN"

# --- Generate .NET glue ---
log "Generating .NET glue code..."
"./$EDITOR_BIN" --headless --generate-mono-glue modules/mono/glue

# --- Build .NET assemblies ---
log "Building Godot .NET assemblies..."
cd modules/mono
python3 build_assemblies.py --godot-output-dir=../../bin
cd ../..

log "Godot .NET assemblies built"

# --- Build export templates (release) ---
log "Building release template (for exporting projects)..."
scons \
  platform="$PLATFORM" \
  target=template_release \
  module_mono_enabled=yes \
  arch="$ARCH_FLAG" \
  -j"$JOBS" \
  2>&1 | tee -a /tmp/godot-build.log

# --- Symlink for easy access ---
mkdir -p "$GODOT_DIR/bin"
ln -sf "$(pwd)/$EDITOR_BIN" "$GODOT_DIR/bin/godot" 2>/dev/null || true

# --- Setup .NET solution in project ---
log "Setting up .NET solution..."
cd "$PROJECT_ROOT/src"

if [ ! -f "SmartThings.Migration.sln" ]; then
  dotnet new sln -n SmartThings.Migration
  dotnet sln add SmartThings.Abstraction/SmartThings.Abstraction.csproj
  dotnet sln add SmartThings.Godot/SmartThings.Godot.csproj
  dotnet sln add SmartThings.Tests/SmartThings.Tests.csproj

  # Add project references
  dotnet add SmartThings.Godot reference SmartThings.Abstraction/SmartThings.Abstraction.csproj
  dotnet add SmartThings.Tests reference SmartThings.Abstraction/SmartThings.Abstraction.csproj
  dotnet add SmartThings.Tests reference SmartThings.Godot/SmartThings.Godot.csproj

  # Add NuGet packages
  dotnet add SmartThings.Godot package MQTTnet
  dotnet add SmartThings.Godot package Microsoft.Extensions.DependencyInjection
  dotnet add SmartThings.Godot package Microsoft.ML.OnnxRuntime

  dotnet add SmartThings.Tests package xunit
  dotnet add SmartThings.Tests package xunit.runner.visualstudio
  dotnet add SmartThings.Tests package Microsoft.NET.Test.Sdk

  log ".NET solution created with all projects + NuGet packages"
else
  log ".NET solution already exists"
fi

# --- Verify build ---
log "Verifying .NET build..."
dotnet build --verbosity quiet && log ".NET solution builds successfully!" || warn "Build has errors (expected until all stubs are filled)"

echo ""
echo "============================================="
log "Godot ${GODOT_VERSION} with C# support is ready!"
echo ""
echo "  Godot editor:  $GODOT_DIR/bin/godot"
echo "  .NET solution: $PROJECT_ROOT/src/SmartThings.Migration.sln"
echo ""
echo "  Quick test:"
echo "    $GODOT_DIR/bin/godot --version"
echo "    cd src && dotnet build"
echo ""
echo "  Next: ./scripts/install-android.sh (optional)"
echo "============================================="
