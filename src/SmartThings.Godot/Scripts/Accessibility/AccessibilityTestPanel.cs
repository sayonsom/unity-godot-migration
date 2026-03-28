// =============================================================================
// AccessibilityTestPanel.cs — On-screen panel to test a11y on real devices
// Bottom-right floating panel with large touch targets (48dp+)
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.Accessibility;

/// <summary>
/// Mobile-friendly test panel for accessibility features.
/// Positioned bottom-right with a toggle FAB button.
/// All buttons are 52px+ tall for easy phone touch targets.
/// Panel scrolls to fit all content on small screens.
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
    private GodotNative.Button? _toggleFab;
    private bool _isExpanded;

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
        BuildToggleFab();
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

    // ── Toggle FAB (always visible, bottom-right) ────────────────────────────

    private void BuildToggleFab()
    {
        _toggleFab = new GodotNative.Button();
        _toggleFab.Text = "A11Y";
        _toggleFab.MouseFilter = MouseFilterEnum.Stop;
        _toggleFab.CustomMinimumSize = new GodotNative.Vector2(64, 64);

        // Bottom-right anchors with safe margin from edges
        _toggleFab.AnchorLeft = 1.0f;
        _toggleFab.AnchorTop = 1.0f;
        _toggleFab.AnchorRight = 1.0f;
        _toggleFab.AnchorBottom = 1.0f;
        _toggleFab.OffsetLeft = -80;
        _toggleFab.OffsetTop = -148;  // above bottom bar area
        _toggleFab.OffsetRight = -12;
        _toggleFab.OffsetBottom = -80;

        _toggleFab.AddThemeFontSizeOverride("font_size", 16);

        var fabStyle = new GodotNative.StyleBoxFlat();
        fabStyle.BgColor = new GodotNative.Color(0.15f, 0.45f, 0.85f, 0.95f);
        fabStyle.CornerRadiusTopLeft = 16;
        fabStyle.CornerRadiusTopRight = 16;
        fabStyle.CornerRadiusBottomLeft = 16;
        fabStyle.CornerRadiusBottomRight = 16;
        _toggleFab.AddThemeStyleboxOverride("normal", fabStyle);

        var fabHover = new GodotNative.StyleBoxFlat();
        fabHover.BgColor = new GodotNative.Color(0.2f, 0.55f, 0.95f, 0.95f);
        fabHover.CornerRadiusTopLeft = 16;
        fabHover.CornerRadiusTopRight = 16;
        fabHover.CornerRadiusBottomLeft = 16;
        fabHover.CornerRadiusBottomRight = 16;
        _toggleFab.AddThemeStyleboxOverride("hover", fabHover);
        _toggleFab.AddThemeStyleboxOverride("pressed", fabHover);

        _toggleFab.Pressed += () =>
        {
            _isExpanded = !_isExpanded;
            if (_panel != null) _panel.Visible = _isExpanded;
            if (_toggleFab != null) _toggleFab.Text = _isExpanded ? "X" : "A11Y";
        };

        AddChild(_toggleFab);
    }

    // ── Main panel (shown/hidden by FAB) ─────────────────────────────────────

    private void BuildPanel()
    {
        _panel = new GodotNative.Panel();
        _panel.Visible = false; // starts hidden

        // Right side, from below safe area to above bottom bar
        // Width = 260px, right-aligned with 8px margin
        _panel.AnchorLeft = 1.0f;
        _panel.AnchorTop = 0.0f;
        _panel.AnchorRight = 1.0f;
        _panel.AnchorBottom = 1.0f;
        _panel.OffsetLeft = -268;
        _panel.OffsetTop = 48;     // below status bar / notch safe area
        _panel.OffsetRight = -8;
        _panel.OffsetBottom = -64; // above bottom nav area
        _panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_panel);

        var panelStyle = new GodotNative.StyleBoxFlat();
        panelStyle.BgColor = new GodotNative.Color(0.06f, 0.06f, 0.1f, 0.95f);
        panelStyle.CornerRadiusTopLeft = 12;
        panelStyle.CornerRadiusTopRight = 12;
        panelStyle.CornerRadiusBottomLeft = 12;
        panelStyle.CornerRadiusBottomRight = 12;
        panelStyle.ContentMarginLeft = 10;
        panelStyle.ContentMarginRight = 10;
        panelStyle.ContentMarginTop = 10;
        panelStyle.ContentMarginBottom = 10;
        _panel.AddThemeStyleboxOverride("panel", panelStyle);

        var scrollContainer = new GodotNative.ScrollContainer();
        scrollContainer.AnchorsPreset = (int)LayoutPreset.FullRect;
        scrollContainer.OffsetLeft = 6;
        scrollContainer.OffsetTop = 6;
        scrollContainer.OffsetRight = -6;
        scrollContainer.OffsetBottom = -6;
        scrollContainer.HorizontalScrollMode = GodotNative.ScrollContainer.ScrollMode.Disabled;
        _panel.AddChild(scrollContainer);

        _buttonContainer = new GodotNative.VBoxContainer();
        _buttonContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _buttonContainer.AddThemeConstantOverride("separation", 6);
        scrollContainer.AddChild(_buttonContainer);

        // ── Title ──
        AddLabel("ACCESSIBILITY TEST", 16, new GodotNative.Color(1f, 0.85f, 0.3f));

        // ── Focus info ──
        _focusInfoLabel = new GodotNative.Label();
        _focusInfoLabel.Text = "Focus: none";
        _focusInfoLabel.AddThemeFontSizeOverride("font_size", 15);
        _focusInfoLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.85f, 0.92f, 1f));
        _focusInfoLabel.AutowrapMode = GodotNative.TextServer.AutowrapMode.WordSmart;
        _buttonContainer.AddChild(_focusInfoLabel);

        // ── Status ──
        _statusLabel = new GodotNative.Label();
        _statusLabel.Text = "Tap A11Y to open";
        _statusLabel.AddThemeFontSizeOverride("font_size", 14);
        _statusLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.5f, 1f, 0.5f));
        _statusLabel.AutowrapMode = GodotNative.TextServer.AutowrapMode.WordSmart;
        _buttonContainer.AddChild(_statusLabel);

        AddSeparator();
        AddLabel("Navigate", 15, new GodotNative.Color(0.7f, 0.85f, 1f));

        // Navigation row — Prev / Next side by side
        var navRow = new GodotNative.HBoxContainer();
        navRow.AddThemeConstantOverride("separation", 8);
        _buttonContainer.AddChild(navRow);

        var prevBtn = MakeButton("< Prev", new GodotNative.Color(0.3f, 0.3f, 0.5f));
        prevBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        prevBtn.Pressed += () =>
        {
            _a11yManager?.FocusPrevious();
            SetStatus("Previous element");
        };
        navRow.AddChild(prevBtn);

        var nextBtn = MakeButton("Next >", new GodotNative.Color(0.3f, 0.3f, 0.5f));
        nextBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nextBtn.Pressed += () =>
        {
            _a11yManager?.FocusNext();
            SetStatus("Next element");
        };
        navRow.AddChild(nextBtn);

        AddActionButton("Activate Focused", new GodotNative.Color(0.2f, 0.5f, 0.3f), () =>
        {
            _a11yManager?.ActivateFocused();
            SetStatus("Activated");
        });

        AddSeparator();
        AddLabel("TTS Speech", 15, new GodotNative.Color(0.7f, 0.85f, 1f));

        AddActionButton("Announce Home", new GodotNative.Color(0.3f, 0.35f, 0.5f), () =>
        {
            if (_home != null)
            {
                var msg = $"{_home.Name}. {_home.Rooms.Count} rooms, {_home.Devices.Count} devices.";
                _a11yService?.Announce(msg, AnnouncePriority.Normal);
                SetStatus($"TTS: {msg}");
            }
        });

        AddActionButton("Announce Room", new GodotNative.Color(0.3f, 0.35f, 0.5f), () =>
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

        AddActionButton("Alert Test", new GodotNative.Color(0.6f, 0.2f, 0.2f), () =>
        {
            _a11yService?.Announce("Alert! Front door has been unlocked.", AnnouncePriority.Alert);
            SetStatus("Alert sent!");
        });

        AddSeparator();
        AddLabel("Voice / Events", 15, new GodotNative.Color(0.7f, 0.85f, 1f));

        AddActionButton("Test Voice Cmd", new GodotNative.Color(0.4f, 0.25f, 0.5f), () =>
        {
            var cmd = TestVoiceCommands[_voiceTestIndex % TestVoiceCommands.Length];
            _voiceTestIndex++;
            EmitSignal(SignalName.TestVoiceCommand, cmd);
            _a11yService?.Announce($"Voice: {cmd}", AnnouncePriority.Normal);
            SetStatus($"Voice: \"{cmd}\"");
        });

        AddActionButton("Simulate Event", new GodotNative.Color(0.4f, 0.35f, 0.2f), () =>
        {
            var evt = TestDeviceEvents[_deviceEventIndex % TestDeviceEvents.Length];
            _deviceEventIndex++;
            EmitSignal(SignalName.TestDeviceEvent, evt.deviceId, evt.capability, evt.value);
            var device = _home?.Devices.Find(d => d.DeviceId == evt.deviceId);
            SetStatus($"Event: {device?.Label ?? evt.deviceId} {evt.capability}={evt.value}");
        });
    }

    // ── Button helpers ────────────────────────────────────────────────────────

    private GodotNative.Button MakeButton(string text, GodotNative.Color bgColor)
    {
        var btn = new GodotNative.Button();
        btn.Text = text;
        btn.MouseFilter = MouseFilterEnum.Stop;
        btn.CustomMinimumSize = new GodotNative.Vector2(0, 52); // 52px = good mobile touch target
        btn.AddThemeFontSizeOverride("font_size", 16);

        var style = new GodotNative.StyleBoxFlat();
        style.BgColor = bgColor;
        style.CornerRadiusTopLeft = 8;
        style.CornerRadiusTopRight = 8;
        style.CornerRadiusBottomLeft = 8;
        style.CornerRadiusBottomRight = 8;
        style.ContentMarginLeft = 12;
        style.ContentMarginRight = 12;
        btn.AddThemeStyleboxOverride("normal", style);

        var pressed = new GodotNative.StyleBoxFlat();
        pressed.BgColor = new GodotNative.Color(
            bgColor.R + 0.15f, bgColor.G + 0.15f, bgColor.B + 0.15f, bgColor.A);
        pressed.CornerRadiusTopLeft = 8;
        pressed.CornerRadiusTopRight = 8;
        pressed.CornerRadiusBottomLeft = 8;
        pressed.CornerRadiusBottomRight = 8;
        pressed.ContentMarginLeft = 12;
        pressed.ContentMarginRight = 12;
        btn.AddThemeStyleboxOverride("hover", pressed);
        btn.AddThemeStyleboxOverride("pressed", pressed);

        return btn;
    }

    private void AddActionButton(string text, GodotNative.Color bgColor, Action callback)
    {
        var btn = MakeButton(text, bgColor);
        btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
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
        sep.CustomMinimumSize = new GodotNative.Vector2(0, 4);
        _buttonContainer?.AddChild(sep);
    }
}
