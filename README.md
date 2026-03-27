# Unity to Godot Migration — Samsung SmartThings

Migrating Samsung SmartThings IoT 3D applications from Unity to Godot Engine 4.5+ with full C# (.NET 8) support.

## Quick Start

```bash
# 1. Clone
git clone https://github.com/sayonsom/unity-godot-migration.git
cd unity-godot-migration

# 2. Install dependencies (Linux/WSL2)
chmod +x scripts/*.sh
./scripts/install-base.sh       # System packages
./scripts/install-dotnet.sh     # .NET 8 SDK
./scripts/install-godot.sh      # Godot 4.5 + C# (builds from source)

# 3. Build
cd src && dotnet build

# 4. Set up WhatsApp notifications (optional)
cd notifier && npm install && node setup.js
```

## Architecture

```
SmartThings C# App Layer  →  IEngineAbstraction Interfaces  →  Godot Backend
```

Engine-agnostic abstraction layer keeps all business logic portable. Six interfaces cover rendering, input, audio, accessibility, networking, and scene lifecycle.

## Migration Phases

| Phase | Name | Duration |
|-------|------|----------|
| 0 | Unity Dependency Audit | 2 weeks |
| 1 | Godot Fork + Abstraction Layer | 3 weeks |
| 2 | Vertical Slice Migration | 4 weeks |
| 3 | Shader & Rendering Migration | 6 weeks |
| 4 | Accessibility + IoT Feature Build | 6 weeks |
| 5 | Full Migration + Platform Export | 5 weeks |

## Target Platforms

- **Windows** — Vulkan Forward+
- **Android** — Mobile renderer, Galaxy Store
- **Web** — Blocked (C# web export not in Godot 4.5)

## Key Tools

- `tools/unity-dependency-audit.py` — Scans Unity project for all dependencies
- `shader-migration/` — HLSL→GDShader cheatsheet + ready-to-use IoT shaders
- `notifier/` — WhatsApp phase completion alerts via whatsapp-web.js

## Tech Stack

Godot 4.5 · C# / .NET 8 · SCons · MQTTnet · ONNX Runtime · AccessKit · whatsapp-web.js
