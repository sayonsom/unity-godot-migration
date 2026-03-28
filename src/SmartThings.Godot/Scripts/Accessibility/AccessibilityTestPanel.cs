// =============================================================================
// AccessibilityTestPanel.cs — Compact floating card for a11y testing on phone
// Bottom-right FAB toggles a small overlay card (NOT a full sidebar)
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.Accessibility;

/// <summary>
/// Compact floating test panel for accessibility features.
/// - FAB button (bottom-right) toggles a small card above it
/// - Card is ~45% screen width, max 400px tall — never blocks the whole view
/// - All buttons 52px tall for easy phone touch targets
/// - Scrollable if content overflows on small screens
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

    // ── FAB toggle button ────────────────────────────────────────────────────

    private void BuildToggleFab()
    {
        _toggleFab = new GodotNative.Button();
        _toggleFab.Text = "A11Y";
        _toggleFab.MouseFilter = MouseFilterEnum.Stop;

        // Fixed size, bottom-right corner
        _toggleFab.AnchorLeft = 1.0f;
        _toggleFab.AnchorTop = 1.0f;
        _toggleFab.AnchorRight = 1.0f;
        _toggleFab.AnchorBottom = 1.0f;
        _toggleFab.OffsetLeft = -76;
        _toggleFab.OffsetTop = -140;
        _toggleFab.OffsetRight = -12;
        _toggleFab.OffsetBottom = -76;
        _toggleFab.AddThemeFontSizeOverride("font_size", 15);

        var style = new GodotNative.StyleBoxFlat();
        style.BgColor = new GodotNative.Color(0.15f, 0.45f, 0.85f, 0.92f);
        style.CornerRadiusTopLeft = 14;
        style.CornerRadiusTopRight = 14;
        style.CornerRadiusBottomLeft = 14;
        style.CornerRadiusBottomRight = 14;
        _toggleFab.AddThemeStyleboxOverride("normal", style);

        var hover = new GodotNative.StyleBoxFlat();
        hover.BgColor = new GodotNative.Color(0.2f, 0.55f, 0.95f, 0.95f);
        hover.CornerRadiusTopLeft = 14;
        hover.CornerRadiusTopRight = 14;
        hover.CornerRadiusBottomLeft = 14;
        hover.CornerRadiusBottomRight = 14;
        _toggleFab.AddThemeStyleboxOverride("hover", hover);
        _toggleFab.AddThemeStyleboxOverride("pressed", hover);

        _toggleFab.Pressed += TogglePanel;
        AddChild(_toggleFab);
    }

    private void TogglePanel()
    {
        _isExpanded = !_isExpanded;
        if (_card != null) _card.Visible = _isExpanded;
        if (_toggleFab != null) _toggleFab.Text = _isExpanded ? "X" : "A11Y";
    }

    // ── Floating card (compact, above the FAB) ───────────────────────────────

    private void BuildCard()
    {
        // Outer container — anchored bottom-right, compact size
        _card = new GodotNative.PanelContainer();
        _card.Visible = false;
        _card.MouseFilter = MouseFilterEnum.Stop;

        // Position: bottom-right, floating above the FAB
        // Width: ~45% of screen or 220px, whichever works
        _card.AnchorLeft = 1.0f;
        _card.AnchorTop = 1.0f;
        _card.AnchorRight = 1.0f;
        _card.AnchorBottom = 1.0f;

        // Card is 220px wide, up to 460px tall, sitting above the FAB
        _card.OffsetLeft = -232;
        _card.OffsetTop = -600;   // max extent upward
        _card.OffsetRight = -8;
        _card.OffsetBottom = -148; // just above FAB

        // Dark rounded card background
        var cardStyle = new GodotNative.StyleBoxFlat();
        cardStyle.BgColor = new GodotNative.Color(0.1f, 0.1f, 0.15f, 0.94f);
        cardStyle.CornerRadiusTopLeft = 14;
        cardStyle.CornerRadiusTopRight = 14;
        cardStyle.CornerRadiusBottomLeft = 14;
        cardStyle.CornerRadiusBottomRight = 14;
        cardStyle.ContentMarginLeft = 10;
        cardStyle.ContentMarginRight = 10;
        cardStyle.ContentMarginTop = 10;
        cardStyle.ContentMarginBottom = 10;
        _card.AddThemeStyleboxOverride("panel", cardStyle);
        AddChild(_card);

        // Scrollable content
        var scroll = new GodotNative.ScrollContainer();
        scroll.HorizontalScrollMode = GodotNative.ScrollContainer.ScrollMode.Disabled;
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        _card.AddChild(scroll);

        _buttonContainer = new GodotNative.VBoxContainer();
        _buttonContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _buttonContainer.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(_buttonContainer);

        // ── Focus info ──
        _focusInfoLabel = new GodotNative.Label();
        _focusInfoLabel.Text = "Focus: none";
        _focusInfoLabel.AddThemeFontSizeOverride("font_size", 14);
        _focusInfoLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.9f, 0.95f, 1f));
        _focusInfoLabel.AutowrapMode = GodotNative.TextServer.AutowrapMode.WordSmart;
        _buttonContainer.AddChild(_focusInfoLabel);

        // ── Status ──
        _statusLabel = new GodotNative.Label();
        _statusLabel.Text = "Ready";
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);
        _statusLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.5f, 1f, 0.5f));
        _statusLabel.AutowrapMode = GodotNative.TextServer.AutowrapMode.WordSmart;
        _buttonContainer.AddChild(_statusLabel);

        // ── Navigation (Prev / Next in a row) ──
        var navRow = new GodotNative.HBoxContainer();
        navRow.AddThemeConstantOverride("separation", 6);
        _buttonContainer.AddChild(navRow);

        var prevBtn = MakeButton("<", new GodotNative.Color(0.25f, 0.3f, 0.45f));
        prevBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        prevBtn.Pressed += () => { _a11yManager?.FocusPrevious(); SetStatus("Prev"); };
        navRow.AddChild(prevBtn);

        var actBtn = MakeButton("Select", new GodotNative.Color(0.2f, 0.45f, 0.3f));
        actBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actBtn.Pressed += () => { _a11yManager?.ActivateFocused(); SetStatus("Activated"); };
        navRow.AddChild(actBtn);

        var nextBtn = MakeButton(">", new GodotNative.Color(0.25f, 0.3f, 0.45f));
        nextBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nextBtn.Pressed += () => { _a11yManager?.FocusNext(); SetStatus("Next"); };
        navRow.AddChild(nextBtn);

        // ── TTS tests ──
        AddActionButton("Announce Home", new GodotNative.Color(0.28f, 0.32f, 0.48f), () =>
        {
            if (_home != null)
            {
                var msg = $"{_home.Name}. {_home.Rooms.Count} rooms, {_home.Devices.Count} devices.";
                _a11yService?.Announce(msg, AnnouncePriority.Normal);
                SetStatus("TTS sent");
            }
        });

        AddActionButton("Alert Test", new GodotNative.Color(0.5f, 0.2f, 0.2f), () =>
        {
            _a11yService?.Announce("Alert! Front door unlocked.", AnnouncePriority.Alert);
            SetStatus("Alert sent");
        });

        // ── Voice + Events ──
        AddActionButton("Voice Cmd", new GodotNative.Color(0.38f, 0.22f, 0.48f), () =>
        {
            var cmd = TestVoiceCommands[_voiceTestIndex % TestVoiceCommands.Length];
            _voiceTestIndex++;
            EmitSignal(SignalName.TestVoiceCommand, cmd);
            _a11yService?.Announce($"Voice: {cmd}", AnnouncePriority.Normal);
            SetStatus($"\"{cmd}\"");
        });

        AddActionButton("Sim Event", new GodotNative.Color(0.38f, 0.32f, 0.18f), () =>
        {
            var evt = TestDeviceEvents[_deviceEventIndex % TestDeviceEvents.Length];
            _deviceEventIndex++;
            EmitSignal(SignalName.TestDeviceEvent, evt.deviceId, evt.capability, evt.value);
            var device = _home?.Devices.Find(d => d.DeviceId == evt.deviceId);
            SetStatus($"{device?.Label ?? evt.deviceId} {evt.value}");
        });
    }

    // ── Button helpers ────────────────────────────────────────────────────────

    private GodotNative.Button MakeButton(string text, GodotNative.Color bgColor)
    {
        var btn = new GodotNative.Button();
        btn.Text = text;
        btn.MouseFilter = MouseFilterEnum.Stop;
        btn.CustomMinimumSize = new GodotNative.Vector2(0, 48);
        btn.AddThemeFontSizeOverride("font_size", 15);

        var style = new GodotNative.StyleBoxFlat();
        style.BgColor = bgColor;
        style.CornerRadiusTopLeft = 8;
        style.CornerRadiusTopRight = 8;
        style.CornerRadiusBottomLeft = 8;
        style.CornerRadiusBottomRight = 8;
        btn.AddThemeStyleboxOverride("normal", style);

        var pressed = new GodotNative.StyleBoxFlat();
        pressed.BgColor = new GodotNative.Color(
            bgColor.R + 0.12f, bgColor.G + 0.12f, bgColor.B + 0.12f, bgColor.A);
        pressed.CornerRadiusTopLeft = 8;
        pressed.CornerRadiusTopRight = 8;
        pressed.CornerRadiusBottomLeft = 8;
        pressed.CornerRadiusBottomRight = 8;
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
}
