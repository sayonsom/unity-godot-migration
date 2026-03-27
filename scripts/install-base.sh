#!/usr/bin/env bash
# =============================================================================
# install-base.sh — Core system dependencies for Godot + .NET development
# Run: chmod +x scripts/install-base.sh && ./scripts/install-base.sh
# Platform: Ubuntu 22.04+ / Debian 12+ / WSL2
# =============================================================================

set -euo pipefail

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

log()  { echo -e "${GREEN}[✓]${NC} $1"; }
warn() { echo -e "${YELLOW}[!]${NC} $1"; }
err()  { echo -e "${RED}[✗]${NC} $1"; }

echo "============================================="
echo "  Base Dependencies Setup"
echo "  Godot SmartThings Migration"
echo "============================================="

# --- Update package lists ---
log "Updating apt package lists..."
sudo apt-get update -qq

# --- Build essentials ---
log "Installing build tools (gcc, g++, make, cmake)..."
sudo apt-get install -y -qq \
  build-essential \
  cmake \
  pkg-config \
  autoconf \
  automake \
  libtool

# --- SCons (Godot build system) ---
log "Installing Python3 + SCons..."
sudo apt-get install -y -qq python3 python3-pip python3-venv
pip3 install --user scons
export PATH="$HOME/.local/bin:$PATH"

# Verify scons
if command -v scons &>/dev/null; then
  log "SCons installed: $(scons --version 2>&1 | head -1)"
else
  warn "SCons not in PATH. Add to ~/.bashrc: export PATH=\"\$HOME/.local/bin:\$PATH\""
fi

# --- Godot build dependencies ---
log "Installing Godot build dependencies..."
sudo apt-get install -y -qq \
  libx11-dev \
  libxcursor-dev \
  libxinerama-dev \
  libgl1-mesa-dev \
  libglu1-mesa-dev \
  libasound2-dev \
  libpulse-dev \
  libudev-dev \
  libxi-dev \
  libxrandr-dev \
  libwayland-dev \
  libdbus-1-dev \
  libspeechd-dev \
  libfontconfig1-dev

# --- Git ---
if ! command -v git &>/dev/null; then
  log "Installing Git..."
  sudo apt-get install -y -qq git
fi
log "Git: $(git --version)"

# --- Node.js (for whatsapp-web.js notifier) ---
if ! command -v node &>/dev/null; then
  log "Installing Node.js 20 LTS..."
  curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
  sudo apt-get install -y -qq nodejs
else
  log "Node.js already installed: $(node --version)"
fi

# --- Useful tools ---
log "Installing additional tools..."
sudo apt-get install -y -qq \
  curl \
  wget \
  unzip \
  jq \
  htop

# --- Chromium for whatsapp-web.js (puppeteer) ---
log "Installing Chromium dependencies for whatsapp-web.js..."
sudo apt-get install -y -qq \
  chromium-browser 2>/dev/null || \
  sudo apt-get install -y -qq chromium 2>/dev/null || \
  warn "Chromium not available via apt. whatsapp-web.js will download its own."

# --- PATH setup ---
BASHRC="$HOME/.bashrc"
if ! grep -q "godot-smartthings" "$BASHRC" 2>/dev/null; then
  log "Adding environment variables to ~/.bashrc..."
  cat >> "$BASHRC" << 'ENVEOF'

# --- Godot SmartThings Migration ---
export GODOT_SMARTTHINGS_ROOT="$HOME/godot-smartthings"
export PATH="$HOME/.local/bin:$GODOT_SMARTTHINGS_ROOT/bin:$PATH"
ENVEOF
fi

echo ""
echo "============================================="
log "Base dependencies installed!"
echo "  Next: ./scripts/install-dotnet.sh"
echo "============================================="
