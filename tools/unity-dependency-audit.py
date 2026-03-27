#!/usr/bin/env python3
"""
Unity Dependency Audit Script — Phase 0
Scans a Unity C# project and produces a dependency matrix for Godot migration.

Usage:
    python3 unity-dependency-audit.py /path/to/unity/project [--output report.json]

Outputs:
    - JSON report with all Unity dependencies, classifications, and migration effort
    - Console summary with HIGH/MED/LOW risk breakdown
"""

import os
import re
import json
import argparse
from pathlib import Path
from collections import defaultdict
from dataclasses import dataclass, asdict
from typing import Optional

# ============================================================================
# Unity namespace -> Godot equivalent mapping
# ============================================================================

UNITY_TO_GODOT_MAP = {
    # Core Engine
    "UnityEngine": {
        "godot_equiv": "Godot",
        "effort": "M",
        "category": "Core Engine",
        "notes": "Namespace-level replacement. Most types have Godot equivalents."
    },
    "UnityEngine.SceneManagement": {
        "godot_equiv": "Godot.SceneTree",
        "effort": "M",
        "category": "Core Engine",
        "notes": "SceneManager.LoadScene() -> GetTree().ChangeSceneToFile()"
    },
    "UnityEngine.UI": {
        "godot_equiv": "Godot.Control nodes",
        "effort": "L",
        "category": "UI",
        "notes": "UGUI -> Godot Control system. Better architecture in Godot."
    },
    "UnityEngine.UIElements": {
        "godot_equiv": "Godot.Control + Theme",
        "effort": "L",
        "category": "UI",
        "notes": "UI Toolkit -> Godot Theme + Control nodes"
    },
    "UnityEngine.Rendering": {
        "godot_equiv": "Godot.RenderingServer",
        "effort": "XL",
        "category": "Rendering",
        "notes": "SRP/URP/HDRP -> Godot renderers (Forward+/Mobile/Compatibility). No SRP equivalent."
    },
    "UnityEngine.Rendering.Universal": {
        "godot_equiv": "Godot.CompositorEffect (4.3+)",
        "effort": "XL",
        "category": "Rendering",
        "notes": "URP pipeline -> Forward+/Mobile renderer. Post-processing via CompositorEffect."
    },
    "UnityEngine.InputSystem": {
        "godot_equiv": "Godot.Input + InputMap",
        "effort": "M",
        "category": "Input",
        "notes": "New Input System -> Godot InputMap + InputEvent system"
    },
    "UnityEngine.Networking": {
        "godot_equiv": "Godot.MultiplayerAPI",
        "effort": "M",
        "category": "Networking",
        "notes": "UNET/Netcode -> ENet/WebSocket/WebRTC in Godot"
    },
    "UnityEngine.Audio": {
        "godot_equiv": "Godot.AudioServer + AudioStreamPlayer",
        "effort": "S",
        "category": "Audio",
        "notes": "AudioSource/Mixer -> AudioStreamPlayer2D/3D + AudioBus"
    },
    "UnityEngine.AI": {
        "godot_equiv": "Godot.NavigationServer3D",
        "effort": "M",
        "category": "AI/Navigation",
        "notes": "NavMesh -> Godot NavigationRegion3D + NavigationAgent3D"
    },
    "UnityEngine.Animations": {
        "godot_equiv": "Godot.AnimationPlayer / AnimationTree",
        "effort": "M",
        "category": "Animation",
        "notes": "Animator/AnimationController -> AnimationPlayer + AnimationTree"
    },
    "UnityEngine.XR": {
        "godot_equiv": "Godot.XRServer",
        "effort": "L",
        "category": "XR",
        "notes": "XR Plugin -> Godot OpenXR integration"
    },

    # Third-party common
    "Mirror": {
        "godot_equiv": "Godot.MultiplayerAPI + ENet",
        "effort": "L",
        "category": "Plugins (3rd-party)",
        "notes": "Mirror networking -> Godot's built-in multiplayer"
    },
    "Newtonsoft.Json": {
        "godot_equiv": "System.Text.Json or Newtonsoft.Json (NuGet)",
        "effort": "S",
        "category": "Pure C# (portable)",
        "notes": "Portable as-is via NuGet. No engine dependency."
    },
    "TMPro": {
        "godot_equiv": "Godot.RichTextLabel (built-in MSDF)",
        "effort": "M",
        "category": "UI",
        "notes": "TextMeshPro -> Label/RichTextLabel with built-in MSDF fonts"
    },
    "DOTween": {
        "godot_equiv": "Godot.Tween",
        "effort": "M",
        "category": "Animation",
        "notes": "DOTween -> Godot Tween class (built-in, similar API)"
    },
}

# Unity API patterns that need migration
UNITY_API_PATTERNS = {
    r"MonoBehaviour": ("Godot.Node (partial class)", "M", "Lifecycle: Start->_Ready, Update->_Process"),
    r"ScriptableObject": ("Godot.Resource", "M", "Data containers: [CreateAssetMenu] -> [GlobalClass]"),
    r"GetComponent<": ("GetNode<T>()", "S", "Component access -> node tree traversal"),
    r"Instantiate\(": ("scene.Instantiate<T>()", "S", "Prefab instantiation -> PackedScene"),
    r"\[SerializeField\]": ("[Export]", "S", "Inspector-exposed fields"),
    r"\[Header\(": ("[ExportGroup()]", "S", "Inspector organization"),
    r"StartCoroutine": ("async/await or Tween", "M", "Coroutines -> native async or Godot Tween"),
    r"yield return": ("await ToSignal() or await Task.Delay()", "M", "Yield -> async/await"),
    r"DontDestroyOnLoad": ("Autoload / GetTree().Root", "S", "Singleton pattern -> Godot Autoload"),
    r"PlayerPrefs": ("Godot.ConfigFile or FileAccess", "S", "Persistent storage"),
    r"Resources\.Load": ("GD.Load<T>() or ResourceLoader", "S", "Resource loading"),
    r"Debug\.Log": ("GD.Print() / GD.PushWarning()", "S", "Logging"),
    r"#if UNITY_ANDROID": ("#if GODOT_ANDROID (custom define)", "M", "Platform conditionals"),
    r"#if UNITY_WEBGL": ("BLOCKER: C# web not supported in 4.5", "XL", "Web export needs GDScript or wait for 4.6+"),
}


@dataclass
class DependencyEntry:
    namespace: str
    file_path: str
    line_number: int
    usage_count: int
    category: str
    godot_equivalent: str
    migration_effort: str  # S, M, L, XL
    risk: str  # LOW, MED, HIGH
    notes: str


def scan_cs_files(project_path: Path) -> list[DependencyEntry]:
    """Scan all .cs files for Unity dependencies."""
    entries = []
    cs_files = list(project_path.rglob("*.cs"))

    print(f"Scanning {len(cs_files)} C# files...")

    namespace_counts = defaultdict(lambda: {"count": 0, "files": set()})

    for cs_file in cs_files:
        try:
            content = cs_file.read_text(encoding="utf-8", errors="replace")
            lines = content.split("\n")

            for i, line in enumerate(lines, 1):
                # Check using statements
                using_match = re.match(r"\s*using\s+([\w.]+)\s*;", line)
                if using_match:
                    ns = using_match.group(1)
                    namespace_counts[ns]["count"] += 1
                    namespace_counts[ns]["files"].add(str(cs_file.relative_to(project_path)))

                # Check Unity API patterns
                for pattern, (equiv, effort, note) in UNITY_API_PATTERNS.items():
                    if re.search(pattern, line):
                        risk = "HIGH" if effort in ("L", "XL") else "MED" if effort == "M" else "LOW"
                        entries.append(DependencyEntry(
                            namespace=f"API: {pattern}",
                            file_path=str(cs_file.relative_to(project_path)),
                            line_number=i,
                            usage_count=1,
                            category="API Pattern",
                            godot_equivalent=equiv,
                            migration_effort=effort,
                            risk=risk,
                            notes=note,
                        ))

        except Exception as e:
            print(f"  Warning: Could not read {cs_file}: {e}")

    # Process namespace-level dependencies
    for ns, data in namespace_counts.items():
        mapping = UNITY_TO_GODOT_MAP.get(ns, None)

        # Check partial matches (e.g., "UnityEngine.UI.Extensions" -> "UnityEngine.UI")
        if not mapping:
            for known_ns, known_map in UNITY_TO_GODOT_MAP.items():
                if ns.startswith(known_ns):
                    mapping = known_map
                    break

        if mapping:
            risk = "HIGH" if mapping["effort"] in ("L", "XL") else "MED" if mapping["effort"] == "M" else "LOW"
            entries.append(DependencyEntry(
                namespace=ns,
                file_path=", ".join(sorted(data["files"])[:5]),
                line_number=0,
                usage_count=data["count"],
                category=mapping["category"],
                godot_equivalent=mapping["godot_equiv"],
                migration_effort=mapping["effort"],
                risk=risk,
                notes=mapping["notes"],
            ))
        elif ns.startswith("Unity") or ns.startswith("UnityEngine") or ns.startswith("UnityEditor"):
            entries.append(DependencyEntry(
                namespace=ns,
                file_path=", ".join(sorted(data["files"])[:5]),
                line_number=0,
                usage_count=data["count"],
                category="Unknown Unity",
                godot_equivalent="NEEDS RESEARCH",
                migration_effort="?",
                risk="HIGH",
                notes=f"Unity namespace not in mapping database. Found in {data['count']} file(s).",
            ))

    return entries


def scan_shader_files(project_path: Path) -> list[dict]:
    """Catalog all Unity shader files."""
    shaders = []
    shader_exts = [".shader", ".shadergraph", ".hlsl", ".cginc", ".compute"]

    for ext in shader_exts:
        for f in project_path.rglob(f"*{ext}"):
            rel_path = str(f.relative_to(project_path))
            content = f.read_text(encoding="utf-8", errors="replace")

            shader_type = "Unknown"
            if ext == ".compute":
                shader_type = "Compute"
            elif ext == ".shadergraph":
                shader_type = "Visual (ShaderGraph)"
            elif "frag" in content.lower() or "fragment" in content.lower():
                shader_type = "Surface/Fragment"
            elif "vert" in content.lower():
                shader_type = "Vertex"

            shaders.append({
                "file": rel_path,
                "type": shader_type,
                "extension": ext,
                "line_count": len(content.split("\n")),
                "godot_target": "GDShader (.gdshader)" if ext != ".compute" else "RenderingDevice compute",
                "effort": "L" if ext == ".shadergraph" else "XL" if ext == ".compute" else "L",
                "notes": f"Manual rewrite required. {ext} -> .gdshader (GLSL ES 3.0 syntax)"
            })

    return shaders


def scan_platform_conditionals(project_path: Path) -> list[dict]:
    """Find all #if UNITY_* platform conditionals."""
    conditionals = []
    for cs_file in project_path.rglob("*.cs"):
        try:
            content = cs_file.read_text(encoding="utf-8", errors="replace")
            for i, line in enumerate(content.split("\n"), 1):
                match = re.search(r"#if\s+(UNITY_\w+)", line)
                if match:
                    conditionals.append({
                        "file": str(cs_file.relative_to(project_path)),
                        "line": i,
                        "conditional": match.group(1),
                        "godot_equiv": {
                            "UNITY_ANDROID": "GODOT_ANDROID (custom build flag)",
                            "UNITY_IOS": "GODOT_IOS (custom build flag)",
                            "UNITY_WEBGL": "BLOCKER: C# web export not available",
                            "UNITY_EDITOR": "Godot.Engine.IsEditorHint()",
                            "UNITY_STANDALONE_WIN": "Godot.OS.GetName() == 'Windows'",
                            "UNITY_STANDALONE_LINUX": "Godot.OS.GetName() == 'Linux'",
                        }.get(match.group(1), "NEEDS RESEARCH")
                    })
        except:
            pass

    return conditionals


def generate_report(project_path: str, output_path: Optional[str] = None):
    """Generate the full Unity Dependency Matrix."""
    path = Path(project_path)
    if not path.exists():
        print(f"Error: Path '{project_path}' does not exist.")
        return

    print(f"\n{'='*60}")
    print(f"  Unity Dependency Audit — Phase 0")
    print(f"  Project: {path.name}")
    print(f"{'='*60}\n")

    # Scan
    dependencies = scan_cs_files(path)
    shaders = scan_shader_files(path)
    conditionals = scan_platform_conditionals(path)

    # Build report
    report = {
        "project": str(path),
        "scan_date": __import__("datetime").datetime.now().isoformat(),
        "summary": {
            "total_dependencies": len(dependencies),
            "total_shaders": len(shaders),
            "total_platform_conditionals": len(conditionals),
            "risk_breakdown": {
                "HIGH": sum(1 for d in dependencies if d.risk == "HIGH"),
                "MED": sum(1 for d in dependencies if d.risk == "MED"),
                "LOW": sum(1 for d in dependencies if d.risk == "LOW"),
            },
            "effort_breakdown": {
                "S": sum(1 for d in dependencies if d.migration_effort == "S"),
                "M": sum(1 for d in dependencies if d.migration_effort == "M"),
                "L": sum(1 for d in dependencies if d.migration_effort == "L"),
                "XL": sum(1 for d in dependencies if d.migration_effort == "XL"),
            },
        },
        "dependencies": [asdict(d) for d in sorted(dependencies, key=lambda x: {"HIGH": 0, "MED": 1, "LOW": 2}.get(x.risk, 3))],
        "shaders": shaders,
        "platform_conditionals": conditionals,
        "blockers": [asdict(d) for d in dependencies if d.risk == "HIGH" or "BLOCKER" in d.notes],
    }

    # Output
    out_path = output_path or f"{path.name}-dependency-audit.json"
    with open(out_path, "w") as f:
        json.dump(report, f, indent=2)

    # Console summary
    s = report["summary"]
    print(f"  Dependencies found:  {s['total_dependencies']}")
    print(f"  Shaders to rewrite:  {s['total_shaders']}")
    print(f"  Platform conditionals: {s['total_platform_conditionals']}")
    print(f"\n  Risk Breakdown:")
    print(f"    HIGH: {s['risk_breakdown']['HIGH']}")
    print(f"    MED:  {s['risk_breakdown']['MED']}")
    print(f"    LOW:  {s['risk_breakdown']['LOW']}")
    print(f"\n  Effort Breakdown:")
    print(f"    S (Small):  {s['effort_breakdown']['S']}")
    print(f"    M (Medium): {s['effort_breakdown']['M']}")
    print(f"    L (Large):  {s['effort_breakdown']['L']}")
    print(f"    XL (Extra): {s['effort_breakdown']['XL']}")

    if report["blockers"]:
        print(f"\n  ⚠️  BLOCKERS ({len(report['blockers'])}):")
        for b in report["blockers"]:
            print(f"    - {b['namespace']}: {b['notes']}")

    print(f"\n  Full report saved to: {out_path}")
    print(f"{'='*60}\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Unity Dependency Audit for Godot Migration")
    parser.add_argument("project_path", help="Path to Unity project root")
    parser.add_argument("--output", "-o", help="Output JSON file path", default=None)
    args = parser.parse_args()
    generate_report(args.project_path, args.output)
