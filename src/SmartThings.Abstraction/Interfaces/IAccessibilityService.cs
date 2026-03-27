// =============================================================================
// IAccessibilityService.cs — Engine-agnostic accessibility abstraction
// Covers screen readers, TalkBack, ARIA, and push-to-talk intent dispatch
// =============================================================================

namespace SmartThings.Abstraction.Interfaces;

/// <summary>
/// Accessibility service covering:
///   1. Screen reader integration (AccessKit on Godot 4.5+, TalkBack on Android)
///   2. Push-to-talk voice command pipeline
///   3. Focus management for keyboard/switch navigation
///   4. High contrast / reduced motion preferences
///
/// Godot 4.5 introduced AccessKit for Control nodes. 3D scene elements
/// need custom accessibility bindings — see <see cref="RegisterAccessibleNode"/>.
/// </summary>
public interface IAccessibilityService
{
    // --- Screen Reader ---

    /// <summary>Announce text to the screen reader (e.g., "Smart thermostat set to 72°F").</summary>
    void Announce(string text, AnnouncePriority priority = AnnouncePriority.Normal);

    /// <summary>Register a 3D node as accessible (gives it a name + role for screen readers).</summary>
    void RegisterAccessibleNode(INodeHandle node, AccessibleInfo info);

    /// <summary>Unregister a node from accessibility tree.</summary>
    void UnregisterAccessibleNode(INodeHandle node);

    /// <summary>Set focus to an accessible element (for keyboard/switch navigation).</summary>
    void SetFocus(INodeHandle node);

    // --- Push-to-Talk Voice Commands ---

    /// <summary>Get the voice command processor for PTT intent dispatch.</summary>
    IVoiceCommandProcessor VoiceCommands { get; }

    // --- User Preferences ---

    /// <summary>Is screen reader active on the platform?</summary>
    bool IsScreenReaderActive { get; }

    /// <summary>User prefers reduced motion?</summary>
    bool PrefersReducedMotion { get; }

    /// <summary>User prefers high contrast?</summary>
    bool PrefersHighContrast { get; }

    /// <summary>Current text scale factor (1.0 = default).</summary>
    float TextScaleFactor { get; }

    /// <summary>Fired when accessibility preferences change.</summary>
    event Action<AccessibilityPreferencesChanged>? OnPreferencesChanged;
}

/// <summary>
/// Voice command processor: takes STT text output and maps to SmartThings device commands.
/// Pipeline: Mic capture -> VAD -> STT -> Intent Parser -> Device Command
/// </summary>
public interface IVoiceCommandProcessor
{
    /// <summary>Register a voice command pattern (e.g., "turn on {device}", "set {device} to {value}").</summary>
    void RegisterCommand(VoiceCommandPattern pattern);

    /// <summary>Process raw STT text and attempt to match a registered command.</summary>
    VoiceCommandResult? ProcessUtterance(string utterance);

    /// <summary>Fired when a voice command is successfully matched and ready to dispatch.</summary>
    event Action<VoiceCommandResult>? OnCommandRecognized;
}

public record AccessibleInfo(
    string Name,            // "Smart Thermostat - Living Room"
    string Description,     // "Currently set to 72°F, mode: cooling"
    AccessibleRole Role,
    string? Value = null    // "72" for slider-like controls
);

public enum AccessibleRole
{
    Button,
    Slider,
    Toggle,
    Label,
    Image,
    Container,
    Device3D,       // Custom: IoT device in 3D scene
    StatusIndicator // Custom: energy/status display
}

public enum AnnouncePriority { Low, Normal, High, Alert }

public record VoiceCommandPattern(
    string Id,
    string[] Templates,     // ["turn on {device}", "switch on {device}", "enable {device}"]
    string Description
);

public record VoiceCommandResult(
    string CommandId,
    string MatchedUtterance,
    Dictionary<string, string> Parameters, // {"device": "living room light", "value": "on"}
    float Confidence
);

public record AccessibilityPreferencesChanged(
    bool ScreenReaderActive,
    bool ReducedMotion,
    bool HighContrast,
    float TextScale
);
