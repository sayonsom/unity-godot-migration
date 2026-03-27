// =============================================================================
// GodotAccessibilityService.cs — Godot 4.5 AccessKit implementation
// Covers screen reader, TalkBack, and push-to-talk voice command dispatch
// =============================================================================

using SmartThings.Abstraction;
using SmartThings.Abstraction.Interfaces;
using GodotNative = Godot;

namespace SmartThings.Godot.Services;

/// <summary>
/// Godot 4.5 accessibility backend using AccessKit.
///
/// AccessKit status in Godot 4.5:
///   - Control nodes: supported (experimental)
///   - 3D scene elements: requires custom bindings via Node accessibility API
///   - Android TalkBack: partial bridge via AccessKit
///   - Web ARIA: not automatic, needs manual work for web target
/// </summary>
public partial class GodotAccessibilityService : GodotNative.Node, IAccessibilityService
{
    private readonly Dictionary<INodeHandle, AccessibleInfo> _accessibleNodes = new();
    private IVoiceCommandProcessor? _voiceCommands;

    public IVoiceCommandProcessor VoiceCommands =>
        _voiceCommands ??= new GodotVoiceCommandProcessor();

    public bool IsScreenReaderActive =>
        GodotNative.DisplayServer.ScreenIsKeptOn(); // Placeholder — check platform a11y API

    public bool PrefersReducedMotion => false; // TODO: query OS preference
    public bool PrefersHighContrast => false;  // TODO: query OS preference
    public float TextScaleFactor => 1.0f;       // TODO: query OS preference

    public event Action<AccessibilityPreferencesChanged>? OnPreferencesChanged;

    // --- Screen Reader ---

    public void Announce(string text, AnnouncePriority priority = AnnouncePriority.Normal)
    {
        // Godot 4.5: Use DisplayServer.tts_speak() for text-to-speech
        // This bridges to platform TTS (Android TalkBack, Windows Narrator, etc.)
        var utteranceId = GodotNative.DisplayServer.TtsSpeak(
            text,
            voice: "",
            volume: 100,
            pitch: 1.0f,
            rate: 1.0f,
            utteranceId: 0,
            interrupt: priority >= AnnouncePriority.High
        );

        GodotNative.GD.Print($"[A11y] Announce ({priority}): {text}");
    }

    public void RegisterAccessibleNode(INodeHandle node, AccessibleInfo info)
    {
        _accessibleNodes[node] = info;

        // For Godot 4.5: set accessibility properties on the underlying Godot node
        // Control nodes get AccessKit integration automatically
        // 3D nodes need custom accessible name/description
        if (node is GodotNodeHandle godotNode && godotNode.IsValid)
        {
            // Set node metadata for accessibility
            godotNode.GodotNode.SetMeta("accessible_name", info.Name);
            godotNode.GodotNode.SetMeta("accessible_description", info.Description);
            godotNode.GodotNode.SetMeta("accessible_role", info.Role.ToString());
            if (info.Value != null)
                godotNode.GodotNode.SetMeta("accessible_value", info.Value);
        }
    }

    public void UnregisterAccessibleNode(INodeHandle node) =>
        _accessibleNodes.Remove(node);

    public void SetFocus(INodeHandle node)
    {
        if (node is GodotNodeHandle godotNode && godotNode.IsValid)
        {
            // For Control nodes, use GrabFocus()
            // For 3D nodes, announce the focused element
            var info = _accessibleNodes.GetValueOrDefault(node);
            if (info != null)
            {
                Announce($"{info.Name}. {info.Description}", AnnouncePriority.Normal);
            }
        }
    }
}

/// <summary>
/// Voice command processor for push-to-talk.
/// Takes STT text output and matches against registered command patterns.
/// </summary>
internal class GodotVoiceCommandProcessor : IVoiceCommandProcessor
{
    private readonly List<VoiceCommandPattern> _patterns = new();

    public event Action<VoiceCommandResult>? OnCommandRecognized;

    public void RegisterCommand(VoiceCommandPattern pattern) =>
        _patterns.Add(pattern);

    public VoiceCommandResult? ProcessUtterance(string utterance)
    {
        var normalized = utterance.Trim().ToLowerInvariant();

        foreach (var pattern in _patterns)
        {
            foreach (var template in pattern.Templates)
            {
                var result = TryMatchTemplate(normalized, template.ToLowerInvariant(), pattern.Id);
                if (result != null)
                {
                    OnCommandRecognized?.Invoke(result);
                    return result;
                }
            }
        }

        return null;
    }

    private static VoiceCommandResult? TryMatchTemplate(string utterance, string template, string commandId)
    {
        // Simple template matching: "turn on {device}" matches "turn on living room light"
        // In production, use a proper NLU/intent parser or regex patterns
        var parameters = new Dictionary<string, string>();

        var parts = template.Split('{', '}');
        var remaining = utterance;

        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 0) // Literal text
            {
                var literal = parts[i].Trim();
                if (string.IsNullOrEmpty(literal)) continue;

                var idx = remaining.IndexOf(literal, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return null;
                remaining = remaining[(idx + literal.Length)..].Trim();
            }
            else // Parameter placeholder
            {
                var paramName = parts[i];
                // Capture everything until the next literal or end of string
                var nextLiteral = i + 1 < parts.Length ? parts[i + 1].Trim() : "";
                string paramValue;

                if (string.IsNullOrEmpty(nextLiteral))
                {
                    paramValue = remaining.Trim();
                    remaining = "";
                }
                else
                {
                    var nextIdx = remaining.IndexOf(nextLiteral, StringComparison.OrdinalIgnoreCase);
                    if (nextIdx < 0) return null;
                    paramValue = remaining[..nextIdx].Trim();
                    remaining = remaining[nextIdx..];
                }

                if (!string.IsNullOrEmpty(paramValue))
                    parameters[paramName] = paramValue;
            }
        }

        return new VoiceCommandResult(
            CommandId: commandId,
            MatchedUtterance: utterance,
            Parameters: parameters,
            Confidence: 0.85f // Placeholder — real confidence from STT model
        );
    }
}
