#!/usr/bin/env bash
# =============================================================================
# install-dotnet.sh — .NET 8 SDK installation
# Run: chmod +x scripts/install-dotnet.sh && ./scripts/install-dotnet.sh
# =============================================================================

set -euo pipefail

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log()  { echo -e "${GREEN}[✓]${NC} $1"; }
warn() { echo -e "${YELLOW}[!]${NC} $1"; }

DOTNET_VERSION="8.0"
INSTALL_DIR="$HOME/.dotnet"

echo "============================================="
echo "  .NET ${DOTNET_VERSION} SDK Installation"
echo "============================================="

# --- Check existing ---
if command -v dotnet &>/dev/null; then
  EXISTING=$(dotnet --version 2>/dev/null || echo "unknown")
  if [[ "$EXISTING" == 8.* ]]; then
    log ".NET 8 SDK already installed: $EXISTING"
    exit 0
  else
    warn "Found .NET $EXISTING, installing .NET 8 alongside..."
  fi
fi

# --- Install via official script ---
log "Downloading .NET install script..."
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh

log "Installing .NET ${DOTNET_VERSION} SDK to ${INSTALL_DIR}..."
/tmp/dotnet-install.sh \
  --channel "$DOTNET_VERSION" \
  --install-dir "$INSTALL_DIR" \
  --verbose

# --- Update PATH ---
export DOTNET_ROOT="$INSTALL_DIR"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"

BASHRC="$HOME/.bashrc"
if ! grep -q "DOTNET_ROOT" "$BASHRC" 2>/dev/null; then
  log "Adding .NET to PATH in ~/.bashrc..."
  cat >> "$BASHRC" << ENVEOF

# --- .NET SDK ---
export DOTNET_ROOT="$INSTALL_DIR"
export PATH="\$DOTNET_ROOT:\$DOTNET_ROOT/tools:\$PATH"
ENVEOF
fi

# --- Verify ---
log ".NET SDK installed: $($INSTALL_DIR/dotnet --version)"
log ".NET location: $INSTALL_DIR"

# --- Install global tools ---
log "Installing dotnet tools..."
"$INSTALL_DIR/dotnet" tool install -g dotnet-format 2>/dev/null || true

echo ""
echo "============================================="
log ".NET ${DOTNET_VERSION} SDK ready!"
echo "  dotnet: $INSTALL_DIR/dotnet"
echo "  Next: ./scripts/install-godot.sh"
echo "============================================="
