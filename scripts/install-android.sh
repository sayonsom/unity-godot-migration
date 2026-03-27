#!/usr/bin/env bash
# =============================================================================
# install-android.sh — Android SDK + NDK for Godot Android export
# Run: chmod +x scripts/install-android.sh && ./scripts/install-android.sh
# =============================================================================

set -euo pipefail

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log()  { echo -e "${GREEN}[✓]${NC} $1"; }
warn() { echo -e "${YELLOW}[!]${NC} $1"; }

ANDROID_HOME="${ANDROID_HOME:-$HOME/Android/Sdk}"
CMDLINE_VERSION="11076708"  # Latest as of March 2026

echo "============================================="
echo "  Android SDK + NDK Setup"
echo "  Target: ${ANDROID_HOME}"
echo "============================================="

mkdir -p "$ANDROID_HOME"

# --- Download commandlinetools ---
if [ ! -d "$ANDROID_HOME/cmdline-tools/latest" ]; then
  log "Downloading Android command-line tools..."
  cd /tmp
  wget -q "https://dl.google.com/android/repository/commandlinetools-linux-${CMDLINE_VERSION}_latest.zip" -O cmdline-tools.zip
  unzip -qo cmdline-tools.zip
  mkdir -p "$ANDROID_HOME/cmdline-tools"
  mv cmdline-tools "$ANDROID_HOME/cmdline-tools/latest"
  log "Command-line tools installed"
else
  log "Command-line tools already installed"
fi

export PATH="$ANDROID_HOME/cmdline-tools/latest/bin:$ANDROID_HOME/platform-tools:$PATH"

# --- Accept licenses ---
log "Accepting Android SDK licenses..."
yes | sdkmanager --licenses 2>/dev/null || true

# --- Install SDK components ---
log "Installing SDK components..."
sdkmanager \
  "platform-tools" \
  "platforms;android-34" \
  "build-tools;34.0.0" \
  "ndk;26.3.11579264" \
  "cmake;3.22.1"

log "Android SDK components installed"

# --- Set up Godot export presets ---
GODOT_DIR="${GODOT_SMARTTHINGS_ROOT:-$HOME/godot-smartthings}"
EDITOR_SETTINGS="$HOME/.config/godot/editor_settings-4.tres"

if [ -d "$GODOT_DIR" ]; then
  mkdir -p "$(dirname "$EDITOR_SETTINGS")"
  cat > "$EDITOR_SETTINGS" << EOF
[gd_resource type="EditorSettings" format=3]

[resource]
export/android/android_sdk_path = "$ANDROID_HOME"
export/android/java_sdk_path = "/usr/lib/jvm/java-17-openjdk-amd64"
export/android/debug_keystore = "$HOME/.android/debug.keystore"
EOF
  log "Godot editor settings configured for Android"
fi

# --- Generate debug keystore ---
if [ ! -f "$HOME/.android/debug.keystore" ]; then
  mkdir -p "$HOME/.android"
  keytool -genkey -v -keystore "$HOME/.android/debug.keystore" \
    -alias androiddebugkey -keyalg RSA -keysize 2048 -validity 10000 \
    -storepass android -keypass android \
    -dname "CN=Debug, OU=Debug, O=Debug, L=Debug, ST=Debug, C=US" 2>/dev/null
  log "Debug keystore generated"
fi

# --- PATH ---
BASHRC="$HOME/.bashrc"
if ! grep -q "ANDROID_HOME" "$BASHRC" 2>/dev/null; then
  cat >> "$BASHRC" << ENVEOF

# --- Android SDK ---
export ANDROID_HOME="$ANDROID_HOME"
export PATH="\$ANDROID_HOME/cmdline-tools/latest/bin:\$ANDROID_HOME/platform-tools:\$PATH"
ENVEOF
  log "Android SDK added to PATH"
fi

echo ""
echo "============================================="
log "Android SDK ready!"
echo "  SDK: $ANDROID_HOME"
echo "  NDK: $ANDROID_HOME/ndk/26.3.11579264"
echo "  Build: Godot editor → Project → Export → Android"
echo "============================================="
