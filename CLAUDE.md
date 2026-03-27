# CLAUDE.md — Unity to Godot Migration (Samsung SmartThings)

## Project Overview

This project migrates Samsung SmartThings IoT 3D applications from Unity to Godot Engine 4.5+ with full C# (.NET 8) support. The goal is to comprehensively replace Unity for all C# applications that use Unity for 3D rendering, shaders, accessibility (push-to-talk), networking, and UI.

**Repository:** https://github.com/sayonsom/unity-godot-migration
**Owner:** Sayon (phoenixunity2026@gmail.com)

## Architecture

```
SmartThings C# App Layer
        │
        ▼
IEngineAbstraction Interfaces  (src/SmartThings.Abstraction/)
        │
        ▼
Godot Backend Implementation   (src/SmartThings.Godot/)
        │
        ▼
Godot 4.5+ Engine (forked as godot-smartthings)
```

The abstraction layer (6 interfaces) keeps all business logic engine-agnostic. The Godot backend implements these interfaces. This means the C# code never imports `Godot` directly — only the backend does.

## Key Interfaces

- `IRenderService` — Scene loading, materials, shaders, camera, quality presets
- `IInputService` — Action-based input, pointer/touch, raycasting
- `IAudioService` — Playback + microphone capture (push-to-talk pipeline)
- `IAccessibilityService` — Screen reader, TalkBack, voice command dispatch
- `INetworkService` — HTTP (SmartThings Cloud), MQTT, CoAP, Matter
- `ISceneService` — Scene lifecycle replacing MonoBehaviour

## 6-Phase Migration Plan (~26 weeks)

| Phase | Name | Duration | Status |
|-------|------|----------|--------|
| 0 | Unity Dependency Audit | 2 weeks | NOT STARTED |
| 1 | Godot Fork + Abstraction Layer | 3 weeks | NOT STARTED |
| 2 | Vertical Slice Migration | 4 weeks | NOT STARTED |
| 3 | Shader & Rendering Migration | 6 weeks | NOT STARTED |
| 4 | Accessibility + IoT Feature Build | 6 weeks | NOT STARTED |
| 5 | Full Migration + Platform Export | 5 weeks | NOT STARTED |

## Critical Risks (3)

1. **C# Web Export BLOCKER** — Godot 4.5 does not support C# web export. Track 4.6/4.7.
2. **Shader Rewrite Volume** — Every Unity HLSL shader needs manual GDShader rewrite. No converter.
3. **Push-to-Talk Custom Build** — Full voice pipeline (capture → VAD → STT → intent) from scratch.

## Technology Stack

- **Engine:** Godot 4.5 stable (forked as godot-smartthings)
- **Language:** C# with .NET 8
- **Build System:** SCons (Godot) + dotnet CLI (.NET projects)
- **DI Container:** Microsoft.Extensions.DependencyInjection
- **IoT:** MQTTnet (NuGet), SmartThings Cloud API (HTTP)
- **Voice/AI:** Microsoft.ML.OnnxRuntime (Silero VAD), platform STT
- **Testing:** GDTest + Chickensoft test packages
- **Notifications:** whatsapp-web.js (phase completion alerts to Unity2Godot group)

## Target Platforms

- **Windows** — Vulkan Forward+ renderer, .NET export
- **Android** — Mobile renderer, Galaxy Store, Knox API
- **Web (WASM)** — BLOCKED for C#. GDScript-only fallback or wait for 4.6+

## Directory Structure

```
godot-smartthings-migration/
├── CLAUDE.md                          ← You are here
├── app_spec.txt                       ← Full application specification
├── README.md                          ← GitHub repo README
├── scripts/
│   ├── install-base.sh                ← Core system dependencies
│   ├── install-dotnet.sh              ← .NET 8 SDK
│   ├── install-godot.sh               ← Clone + build Godot with C#
│   ├── install-android.sh             ← Android SDK + NDK
│   └── setup-windows.ps1              ← Windows all-in-one setup
├── notifier/
│   ├── package.json                   ← whatsapp-web.js dependencies
│   ├── setup.js                       ← QR code auth for WhatsApp
│   ├── send-phase-update.js           ← Send phase notifications
│   └── .wwebjs_auth/                  ← Session data (gitignored)
├── src/
│   ├── SmartThings.Abstraction/       ← Engine-agnostic interfaces
│   │   ├── Interfaces/                ← IRenderService, IAudioService, etc.
│   │   ├── Models/                    ← DeviceModels, MathTypes
│   │   └── SmartThings.Abstraction.csproj
│   ├── SmartThings.Godot/             ← Godot backend implementations
│   │   ├── Services/                  ← GodotRenderService, etc.
│   │   ├── Shaders/                   ← Converted .gdshader files
│   │   ├── Scenes/                    ← .tscn scene files
│   │   └── SmartThings.Godot.csproj
│   └── SmartThings.Tests/             ← Unit + integration tests
├── shader-migration/
│   ├── HLSL_to_GDShader_Cheatsheet.gdshader
│   ├── device_state_glow.gdshader
│   └── energy_flow.gdshader
├── tools/
│   └── unity-dependency-audit.py      ← Scans Unity project for dependencies
├── docs/
│   └── Unity-to-Godot-Migration-Plan.docx
└── godot-smartthings/                 ← Godot engine fork (cloned by install-godot.sh)
```

## Commands

```bash
# Setup (run in order)
./scripts/install-base.sh           # System packages (apt)
./scripts/install-dotnet.sh         # .NET 8 SDK
./scripts/install-godot.sh          # Clone & build Godot 4.5 with C#
./scripts/install-android.sh        # Android SDK (optional)

# Build .NET solution
cd src && dotnet build

# Run tests
cd src && dotnet test

# Run Unity audit on existing project
python3 tools/unity-dependency-audit.py /path/to/unity/project -o audit.json

# WhatsApp notifications
cd notifier && node setup.js         # First time: scan QR
node send-phase-update.js 0 "completed" "Unity dependency matrix generated"

# Build Godot editor
cd godot-smartthings && scons platform=linuxbsd target=editor module_mono_enabled=yes -j$(nproc)
```

## Coding Conventions

- All engine-facing code goes through `IEngineAbstraction` interfaces — never import `Godot` namespace in application layer
- Every Godot C# object that wraps an engine object MUST be disposed (use `using` blocks or explicit `Dispose()`)
- Use `[Export]` instead of Unity's `[SerializeField]`
- Use `partial class : Node` instead of `MonoBehaviour`
- Shaders are GDShader (GLSL ES 3.0 syntax), NOT HLSL
- Platform conditionals: use Godot defines, not `#if UNITY_*`
- NuGet packages for pure C# libraries (MQTTnet, System.Text.Json, etc.)
- GDExtension for native platform bindings (Knox SDK, Samsung APIs)

## When Starting a New Phase

1. Update the phase status table above
2. Send WhatsApp notification: `cd notifier && node send-phase-update.js <phase> "started" "<description>"`
3. Create a feature branch: `git checkout -b phase-<N>/<feature-name>`
4. On completion, send notification and merge to main

## Environment Variables

```bash
export GODOT_SMARTTHINGS_ROOT="$HOME/godot-smartthings"
export DOTNET_ROOT="$HOME/.dotnet"
export ANDROID_HOME="$HOME/Android/Sdk"
export PATH="$GODOT_SMARTTHINGS_ROOT/bin:$DOTNET_ROOT:$PATH"
```

## Known Gotchas

- Godot C# objects are NOT garbage collected like Unity. Dispose everything.
- `GetComponent<T>()` → `GetNode<T>()` (tree traversal, not component lookup)
- Coroutines (`yield return`) → `async/await` or `await ToSignal()`
- `DontDestroyOnLoad` → Autoload nodes
- `Resources.Load<T>()` → `GD.Load<T>()` or `ResourceLoader.Load<T>()`
- `Debug.Log()` → `GD.Print()` / `GD.PushWarning()` / `GD.PushError()`
- No C# web export in Godot 4.5. This is a HARD BLOCKER for browser targets.
