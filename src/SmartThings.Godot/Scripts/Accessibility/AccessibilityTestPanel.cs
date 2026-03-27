// =============================================================================
// AccessibilityTestPanel.cs — On-screen panel to manually test all a11y features
// Visible buttons for: navigate rooms, announce, toggle TalkBack mode, test PTT
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.Accessibility;

/// <summary>
/// Debug/test panel for accessibility features.
/// Shows a collapsible sidebar with buttons to:
///   - Navigate between rooms/devices (Next/Prev/Activate)
///   - Test TTS announcements
///   - Simulate device state changes
///   - Test voice commands without mic
///   - Show current focus info
///
/// This panel is essential for testing on a real phone where
/// TalkBack gestures may not map 1:1 to our custom 3D navigation.
/// </summary>
public partial class AccessibilityTestPanel : GodotNative.Control
{
    private HomeMapAccessibilityManager? _a11yManager;
    private IAccessibilityService? _a11yService;
    private SmartHome? _home;

    private GodotNative.VBoxContainer? _buttonContainer;
    private GodotNative.Label? _focusInfoLabel;
    private GodotNative.Label? _statusLabel;
    private GodotNative.Panel? _panel;
    private bool _isExpanded = true;

    // Voice command test cycling
    private int _voiceTestIndex;
    private static readonly string[] TestVoiceCommands =
    {
        "turn on kitchen light",
        "turn off bedroom light",
        "show me the living room",
        "what's the temperature",
        "good night",
        "go to balcony",
        "open smart blinds",
        "lock front door",
        "movie time",
        "i'm leaving",
    };

    // Simulated device events for testing
    private int _deviceEventIndex;
    private static readonly (string deviceId, string capability, string value)[] TestDeviceEvents =
    {
        ("dev_light_lr", "switch", "on"),
        ("dev_light_lr", "switch", "off"),
        ("dev_light_b1", "switch", "on"),
        ("dev_ac_b1", "thermostat", "72"),
        ("dev_cam_bal1", "motion", "active"),
        ("dev_light_kit", "switch", "error"),
        ("dev_light_b2", "switch", "on"),
    };

    /// <summary>Fired when a test voice command should be processed.</summary>
    [GodotNative.Signal] public delegate void TestVoiceCommandEventHandler(string command);

    /// <summary>Fired when a simulated device event should be dispatched.</summary>
    [GodotNative.Signal] public delegate void TestDeviceEventEventHandler(
        string deviceId, string capability, string value);

    public void Initialize(
        HomeMapAccessibilityManager a11yManager,
        IAccessibilityService a11yService,
        SmartHome home)
    {
        _a11yManager = a11yManager;
        _a11yService = a11yService;
        _home = home;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorsPreset = (int)LayoutPreset.FullRect;
        BuildPanel();
    }

    public void UpdateFocusInfo(string elementName, string elementType, int index, int total)
    {
        if (_focusInfoLabel != null)
            _focusInfoLabel.Text = $"Focus: {elementName}\n({elementType} {index + 1}/{total})";
    }

    public void SetStatus(string text)
    {
        if (_statusLabel != null)
            _statusLabel.Text = text;
    }

    private void BuildPanel()
    {
        // Collapsible panel on the left side
        _panel = new GodotNative.Panel();
        _panel.AnchorsPreset = (int)LayoutPreset.LeftWide;
        _panel.OffsetRight = 200;
        _panel.OffsetTop = 65;  // Below top bar
        _panel.OffsetBottom = -60; // Above bottom bar
        _panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_panel);

        var panelStyle = new GodotNative.StyleBoxFlat();
        panelStyle.BgColor = new GodotNative.Color(0.08f, 0.08f, 0.12f, 0.92f);
        panelStyle.ContentMarginLeft = 8;
        panelStyle.ContentMarginRight = 8;
        panelStyle.ContentMarginTop = 8;
        panelStyle.ContentMarginBottom = 8;
        _panel.AddThemeStyleboxOverride("panel", panelStyle);

        var scrollContainer = new GodotNative.ScrollContainer();
        scrollContainer.AnchorsPreset = (int)LayoutPreset.FullRect;
        scrollContainer.OffsetLeft = 4;
        scrollContainer.OffsetTop = 4;
        scrollContainer.OffsetRight = -4;
        scrollContainer.OffsetBottom = -4;
        _panel.AddChild(scrollContainer);

        _buttonContainer = new GodotNative.VBoxContainer();
        _buttonContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scrollContainer.AddChild(_buttonContainer);

        // Title
        AddLabel("-- A11Y TEST --", 14, new GodotNative.Color(1f, 0.8f, 0.2f));

        // Focus info display
        _focusInfoLabel = new GodotNative.Label();
        _focusInfoLabel.Text = "Focus: none";
        _focusInfoLabel.AddThemeFontSizeOverride("font_size", 12);
        _focusInfoLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.8f, 0.9f, 1f));
        _focusInfoLabel.AutowrapMode = GodotNative.TextServer.AutowrapMode.WordSmart;
        _buttonContainer.AddChild(_focusInfoLabel);

        // Status label
        _statusLabel = new GodotNative.Label();
        _statusLabel.Text = "";
        _statusLabel.AddThemeFontSizeOverride("font_size", 11);
        _statusLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.5f, 1f, 0.5f));
        _statusLabel.AutowrapMode = GodotNative.TextServer.AutowrapMode.WordSmart;
        _buttonContainer.AddChild(_statusLabel);

        AddSeparator();
        AddLabel("Navigation", 13, new GodotNative.Color(0.7f, 0.8f, 1f));

        // Navigation buttons
        AddButton("< Prev", () =>
        {
            _a11yManager?.FocusPrevious();
            SetStatus("Moved to previous element");
        });
        AddButton("Next >", () =>
        {
            _a11yManager?.FocusNext();
            SetStatus("Moved to next element");
        });
        AddButton("Activate", () =>
        {
            _a11yManager?.ActivateFocused();
            SetStatus("Activated focused element");
        });

        AddSeparator();
        AddLabel("TTS Test", 13, new GodotNative.Color(0.7f, 0.8f, 1f));

        // TTS announcement buttons
        AddButton("Announce Home", () =>
        {
            if (_home != null)
            {
                var msg = $"{_home.Name}. {_home.Rooms.Count} rooms, {_home.Devices.Count} devices.";
                _a11yService?.Announce(msg, AnnouncePriority.Normal);
                SetStatus($"TTS: {msg}");
            }
        });

        AddButton("Announce Room", () =>
        {
            if (_home != null && _home.Rooms.Count > 0)
            {
                var room = _home.Rooms[new Random().Next(_home.Rooms.Count)];
                var devCount = _home.Devices.Count(d => d.RoomId == room.RoomId);
                var msg = $"{room.Name}. {room.RoomType}. {devCount} devices.";
                _a11yService?.Announce(msg, AnnouncePriority.Normal);
                SetStatus($"TTS: {msg}");
            }
        });

        AddButton("Alert Test", () =>
        {
            _a11yService?.Announce("Alert! Front door has been unlocked.", AnnouncePriority.Alert);
            SetStatus("TTS Alert sent");
        });

        AddSeparator();
        AddLabel("Voice Cmd", 13, new GodotNative.Color(0.7f, 0.8f, 1f));

        // Voice command test button (cycles through commands)
        AddButton("Test Voice Cmd", () =>
        {
            var cmd = TestVoiceCommands[_voiceTestIndex % TestVoiceCommands.Length];
            _voiceTestIndex++;
            EmitSignal(SignalName.TestVoiceCommand, cmd);
            _a11yService?.Announce($"Voice command: {cmd}", AnnouncePriority.Normal);
            SetStatus($"Voice: \"{cmd}\"");
        });

        AddSeparator();
        AddLabel("Device Events", 13, new GodotNative.Color(0.7f, 0.8f, 1f));

        // Simulated device event button
        AddButton("Simulate Event", () =>
        {
            var evt = TestDeviceEvents[_deviceEventIndex % TestDeviceEvents.Length];
            _deviceEventIndex++;
            EmitSignal(SignalName.TestDeviceEvent, evt.deviceId, evt.capability, evt.value);
            var device = _home?.Devices.Find(d => d.DeviceId == evt.deviceId);
            SetStatus($"Event: {device?.Label ?? evt.deviceId} → {evt.capability}={evt.value}");
        });

        AddSeparator();
        AddLabel("Panel", 13, new GodotNative.Color(0.7f, 0.8f, 1f));

        // Toggle panel visibility
        AddButton("Hide Panel", () =>
        {
            _isExpanded = !_isExpanded;
            if (_panel != null)
            {
                _panel.OffsetRight = _isExpanded ? 200 : 50;
                if (_buttonContainer != null) _buttonContainer.Visible = _isExpanded;
            }
        });
    }

    private void AddButton(string text, Action callback)
    {
        var btn = new GodotNative.Button();
        btn.Text = text;
        btn.MouseFilter = MouseFilterEnum.Stop;
        btn.CustomMinimumSize = new GodotNative.Vector2(0, 36);
        btn.AddThemeFontSizeOverride("font_size", 13);
        btn.Pressed += callback;
        _buttonContainer?.AddChild(btn);
    }

    private void AddLabel(string text, int fontSize, GodotNative.Color color)
    {
        var label = new GodotNative.Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _buttonContainer?.AddChild(label);
    }

    private void AddSeparator()
    {
        var sep = new GodotNative.HSeparator();
        sep.CustomMinimumSize = new GodotNative.Vector2(0, 8);
        _buttonContainer?.AddChild(sep);
    }
}
