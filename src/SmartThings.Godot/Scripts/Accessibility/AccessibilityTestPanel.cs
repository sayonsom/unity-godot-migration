// =============================================================================
// AccessibilityTestPanel.cs — On-device accessibility controls
// Professional, Samsung SmartThings-style floating panel for beta testers
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.Accessibility;

/// <summary>
/// Accessibility control panel for real phone testing.
///
/// Design principles (matching Samsung SmartThings):
///   - Large touch targets: 60px height, 18pt+ font
///   - FAB button 72x72 px — easy to find and tap
///   - Compact card floats above FAB, max ~55% screen height
///   - "Stop" button always at top to halt any runaway TTS
///   - Clean, dark semi-transparent with rounded corners
///   - Scrollable content for small screens
/// </summary>
public partial class AccessibilityTestPanel : GodotNative.Control
{
    private HomeMapAccessibilityManager? _a11yManager;
    private IAccessibilityService? _a11yService;
    private SmartHome? _home;

    private GodotNative.VBoxContainer? _buttonContainer;
    private GodotNative.Label? _focusInfoLabel;
    private GodotNative.Label? _statusLabel;
    private GodotNative.PanelContainer? _card;
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

    // Simulated device events
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

    [GodotNative.Signal] public delegate void TestVoiceCommandEventHandler(string command);
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
        BuildCard();
    }

    public void UpdateFocusInfo(string elementName, string elementType, int index, int total)
    {
        if (_focusInfoLabel != null)
            _focusInfoLabel.Text = $"{elementType}: {elementName} ({index + 1}/{total})";
    }

    public void SetStatus(string text)
    {
        if (_statusLabel != null)
            _statusLabel.Text = text;
    }

    // ── FAB button — 72x72, bottom-right ─────────────────────────────────────

    private void BuildToggleFab()
    {
        _toggleFab = new GodotNative.Button();
        _toggleFab.Text = "A11Y";
        _toggleFab.MouseFilter = MouseFilterEnum.Stop;

        _toggleFab.AnchorLeft = 1.0f;
        _toggleFab.AnchorTop = 1.0f;
        _toggleFab.AnchorRight = 1.0f;
        _toggleFab.AnchorBottom = 1.0f;
        _toggleFab.OffsetLeft = -88;
        _toggleFab.OffsetTop = -148;
        _toggleFab.OffsetRight = -16;
        _toggleFab.OffsetBottom = -76;
        _toggleFab.AddThemeFontSizeOverride("font_size", 18);
        _toggleFab.AddThemeColorOverride("font_color", GodotNative.Colors.White);

        ApplyRoundedStyle(_toggleFab, new GodotNative.Color(0.13f, 0.42f, 0.82f), 16);
        _toggleFab.Pressed += TogglePanel;
        AddChild(_toggleFab);
    }

    private void TogglePanel()
    {
        _isExpanded = !_isExpanded;
        if (_card != null) _card.Visible = _isExpanded;
        if (_toggleFab != null)
        {
            _toggleFab.Text = _isExpanded ? "CLOSE" : "A11Y";
        }
    }

    // ── Floating card ─────────────────────────────────────────────────────────

    private void BuildCard()
    {
        _card = new GodotNative.PanelContainer();
        _card.Visible = false;
        _card.MouseFilter = MouseFilterEnum.Stop;

        // Anchored bottom-right, above the FAB
        _card.AnchorLeft = 0.08f;   // 8% from left = ~92% width available
        _card.AnchorTop = 1.0f;
        _card.AnchorRight = 1.0f;
        _card.AnchorBottom = 1.0f;
        _card.OffsetLeft = 0;
        _card.OffsetTop = -720;    // tall enough — will be bounded by content
        _card.OffsetRight = -12;
        _card.OffsetBottom = -156; // above FAB

        var cardStyle = new GodotNative.StyleBoxFlat();
        cardStyle.BgColor = new GodotNative.Color(0.08f, 0.08f, 0.12f, 0.96f);
        cardStyle.CornerRadiusTopLeft = 16;
        cardStyle.CornerRadiusTopRight = 16;
        cardStyle.CornerRadiusBottomLeft = 16;
        cardStyle.CornerRadiusBottomRight = 16;
        cardStyle.ContentMarginLeft = 14;
        cardStyle.ContentMarginRight = 14;
        cardStyle.ContentMarginTop = 14;
        cardStyle.ContentMarginBottom = 14;
        _card.AddThemeStyleboxOverride("panel", cardStyle);
        AddChild(_card);

        // Scrollable content
        var scroll = new GodotNative.ScrollContainer();
        scroll.HorizontalScrollMode = GodotNative.ScrollContainer.ScrollMode.Disabled;
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        _card.AddChild(scroll);

        _buttonContainer = new GodotNative.VBoxContainer();
        _buttonContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _buttonContainer.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_buttonContainer);

        // ── STOP button — always first, always prominent ──
        AddActionButton("STOP SPEAKING", new GodotNative.Color(0.7f, 0.15f, 0.15f), () =>
        {
            _a11yService?.StopSpeaking();
            SetStatus("Speech stopped");
        });

        // ── Focus info ──
        _focusInfoLabel = new GodotNative.Label();
        _focusInfoLabel.Text = "Tap a room or use navigation";
        _focusInfoLabel.AddThemeFontSizeOverride("font_size", 17);
        _focusInfoLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.92f, 0.95f, 1f));
        _focusInfoLabel.AutowrapMode = GodotNative.TextServer.AutowrapMode.WordSmart;
        _buttonContainer.AddChild(_focusInfoLabel);

        // ── Status ──
        _statusLabel = new GodotNative.Label();
        _statusLabel.Text = "Ready";
        _statusLabel.AddThemeFontSizeOverride("font_size", 15);
        _statusLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.45f, 0.9f, 0.45f));
        _statusLabel.AutowrapMode = GodotNative.TextServer.AutowrapMode.WordSmart;
        _buttonContainer.AddChild(_statusLabel);

        // ── Navigation row: < Prev | Select | Next > ──
        var navRow = new GodotNative.HBoxContainer();
        navRow.AddThemeConstantOverride("separation", 8);
        _buttonContainer.AddChild(navRow);

        var prevBtn = MakeButton("< Prev", new GodotNative.Color(0.22f, 0.28f, 0.42f));
        prevBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        prevBtn.Pressed += () => { _a11yManager?.FocusPrevious(); SetStatus("Previous"); };
        navRow.AddChild(prevBtn);

        var actBtn = MakeButton("Select", new GodotNative.Color(0.18f, 0.42f, 0.28f));
        actBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actBtn.Pressed += () => { _a11yManager?.ActivateFocused(); SetStatus("Activated"); };
        navRow.AddChild(actBtn);

        var nextBtn = MakeButton("Next >", new GodotNative.Color(0.22f, 0.28f, 0.42f));
        nextBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nextBtn.Pressed += () => { _a11yManager?.FocusNext(); SetStatus("Next"); };
        navRow.AddChild(nextBtn);

        // ── TTS ──
        var ttsRow = new GodotNative.HBoxContainer();
        ttsRow.AddThemeConstantOverride("separation", 8);
        _buttonContainer.AddChild(ttsRow);

        var announceBtn = MakeButton("Announce Home", new GodotNative.Color(0.25f, 0.3f, 0.48f));
        announceBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        announceBtn.Pressed += () =>
        {
            if (_home != null)
            {
                var msg = $"{_home.Name}. {_home.Rooms.Count} rooms, {_home.Devices.Count} devices.";
                _a11yService?.Announce(msg, AnnouncePriority.Normal);
                SetStatus("Announced");
            }
        };
        ttsRow.AddChild(announceBtn);

        var alertBtn = MakeButton("Alert Test", new GodotNative.Color(0.55f, 0.18f, 0.18f));
        alertBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        alertBtn.Pressed += () =>
        {
            _a11yService?.Announce("Alert! Front door unlocked.", AnnouncePriority.Alert);
            SetStatus("Alert sent");
        };
        ttsRow.AddChild(alertBtn);

        // ── Voice + Events row ──
        var actionRow = new GodotNative.HBoxContainer();
        actionRow.AddThemeConstantOverride("separation", 8);
        _buttonContainer.AddChild(actionRow);

        var voiceBtn = MakeButton("Voice Cmd", new GodotNative.Color(0.35f, 0.2f, 0.45f));
        voiceBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        voiceBtn.Pressed += () =>
        {
            var cmd = TestVoiceCommands[_voiceTestIndex % TestVoiceCommands.Length];
            _voiceTestIndex++;
            EmitSignal(SignalName.TestVoiceCommand, cmd);
            _a11yService?.Announce($"Voice: {cmd}", AnnouncePriority.Normal);
            SetStatus($"\"{cmd}\"");
        };
        actionRow.AddChild(voiceBtn);

        var eventBtn = MakeButton("Sim Event", new GodotNative.Color(0.35f, 0.3f, 0.15f));
        eventBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        eventBtn.Pressed += () =>
        {
            var evt = TestDeviceEvents[_deviceEventIndex % TestDeviceEvents.Length];
            _deviceEventIndex++;
            EmitSignal(SignalName.TestDeviceEvent, evt.deviceId, evt.capability, evt.value);
            var device = _home?.Devices.Find(d => d.DeviceId == evt.deviceId);
            SetStatus($"{device?.Label ?? evt.deviceId}: {evt.value}");
        };
        actionRow.AddChild(eventBtn);
    }

    // ── Button helpers ────────────────────────────────────────────────────────

    private GodotNative.Button MakeButton(string text, GodotNative.Color bgColor)
    {
        var btn = new GodotNative.Button();
        btn.Text = text;
        btn.MouseFilter = MouseFilterEnum.Stop;
        btn.CustomMinimumSize = new GodotNative.Vector2(0, 60); // 60px — large touch target
        btn.AddThemeFontSizeOverride("font_size", 18);
        btn.AddThemeColorOverride("font_color", GodotNative.Colors.White);
        ApplyRoundedStyle(btn, bgColor, 10);
        return btn;
    }

    private void AddActionButton(string text, GodotNative.Color bgColor, Action callback)
    {
        var btn = MakeButton(text, bgColor);
        btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        btn.Pressed += callback;
        _buttonContainer?.AddChild(btn);
    }

    private static void ApplyRoundedStyle(GodotNative.Button btn, GodotNative.Color bgColor, int radius)
    {
        var normal = new GodotNative.StyleBoxFlat();
        normal.BgColor = bgColor;
        normal.CornerRadiusTopLeft = radius;
        normal.CornerRadiusTopRight = radius;
        normal.CornerRadiusBottomLeft = radius;
        normal.CornerRadiusBottomRight = radius;
        normal.ContentMarginLeft = 8;
        normal.ContentMarginRight = 8;
        btn.AddThemeStyleboxOverride("normal", normal);

        var pressed = new GodotNative.StyleBoxFlat();
        pressed.BgColor = new GodotNative.Color(
            MathF.Min(1, bgColor.R + 0.15f),
            MathF.Min(1, bgColor.G + 0.15f),
            MathF.Min(1, bgColor.B + 0.15f),
            bgColor.A);
        pressed.CornerRadiusTopLeft = radius;
        pressed.CornerRadiusTopRight = radius;
        pressed.CornerRadiusBottomLeft = radius;
        pressed.CornerRadiusBottomRight = radius;
        pressed.ContentMarginLeft = 8;
        pressed.ContentMarginRight = 8;
        btn.AddThemeStyleboxOverride("hover", pressed);
        btn.AddThemeStyleboxOverride("pressed", pressed);
    }
}
