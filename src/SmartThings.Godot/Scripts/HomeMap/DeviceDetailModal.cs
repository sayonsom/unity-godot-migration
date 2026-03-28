// =============================================================================
// DeviceDetailModal.cs — Samsung SmartThings-style bottom sheet device modal
// Slides up from bottom when a device pin is tapped. Shows device controls.
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// A SmartThings-style bottom sheet modal for device control.
/// Displays device info, status, and category-specific controls.
/// Appears over the 3D scene with a dark overlay backdrop.
/// </summary>
public partial class DeviceDetailModal : GodotNative.Control
{
    /// <summary>Emitted when a device command is issued from the modal controls.</summary>
    [GodotNative.Signal]
    public delegate void DeviceCommandIssuedEventHandler(string deviceId, string capability, string commandName);

    private SmartDevice? _currentDevice;
    private SmartHome? _currentHome;

    // UI nodes built programmatically
    private GodotNative.ColorRect? _overlay;
    private GodotNative.PanelContainer? _sheet;
    private GodotNative.VBoxContainer? _contentBox;
    private GodotNative.Label? _iconLabel;
    private GodotNative.Label? _deviceNameLabel;
    private GodotNative.Label? _statusBadge;
    private GodotNative.Label? _roomLabel;
    private GodotNative.VBoxContainer? _controlsContainer;

    // Style constants
    private const int SheetMinHeight = 420;
    private const int ButtonHeight = 56;
    private const int ButtonFontSize = 18;
    private const int TitleFontSize = 22;
    private const int IconFontSize = 32;
    private const int TemperatureFontSize = 36;
    private const int HandleWidth = 40;
    private const int HandleHeight = 5;
    private const int Padding = 24;

    public override void _Ready()
    {
        // Fill entire screen
        SetAnchorsPreset(GodotNative.Control.LayoutPreset.FullRect);
        MouseFilter = GodotNative.Control.MouseFilterEnum.Ignore;
        Visible = false;

        BuildUI();

        GodotNative.GD.Print("[DeviceDetailModal] Ready.");
    }

    /// <summary>Show the modal for a specific device.</summary>
    public void ShowDevice(SmartDevice device, SmartHome home)
    {
        _currentDevice = device;
        _currentHome = home;

        PopulateHeader(device, home);
        PopulateControls(device);

        Visible = true;
        MouseFilter = GodotNative.Control.MouseFilterEnum.Stop;
    }

    /// <summary>Hide the modal.</summary>
    public void Hide()
    {
        Visible = false;
        MouseFilter = GodotNative.Control.MouseFilterEnum.Ignore;
        _currentDevice = null;
        _currentHome = null;
    }

    // ── UI Construction ──────────────────────────────────────────────────

    private void BuildUI()
    {
        // Dark semi-transparent overlay (tap to dismiss)
        _overlay = new GodotNative.ColorRect();
        _overlay.SetAnchorsPreset(GodotNative.Control.LayoutPreset.FullRect);
        _overlay.Color = new GodotNative.Color(0, 0, 0, 0.5f);
        _overlay.MouseFilter = GodotNative.Control.MouseFilterEnum.Stop;
        _overlay.GuiInput += OnOverlayInput;
        AddChild(_overlay);

        // Bottom sheet panel
        _sheet = new GodotNative.PanelContainer();
        _sheet.SetAnchorsPreset(GodotNative.Control.LayoutPreset.BottomWide);
        _sheet.AnchorTop = 0.4f; // ~60% screen height
        _sheet.AnchorBottom = 1.0f;
        _sheet.AnchorLeft = 0.0f;
        _sheet.AnchorRight = 1.0f;
        _sheet.OffsetTop = 0;
        _sheet.OffsetBottom = 0;
        _sheet.OffsetLeft = 0;
        _sheet.OffsetRight = 0;
        _sheet.MouseFilter = GodotNative.Control.MouseFilterEnum.Stop;

        // Style the panel with rounded corners and dark background
        var panelStyle = new GodotNative.StyleBoxFlat();
        panelStyle.BgColor = new GodotNative.Color(0.14f, 0.14f, 0.16f, 1.0f); // Dark card
        panelStyle.CornerRadiusTopLeft = 20;
        panelStyle.CornerRadiusTopRight = 20;
        panelStyle.CornerRadiusBottomLeft = 0;
        panelStyle.CornerRadiusBottomRight = 0;
        panelStyle.ContentMarginLeft = Padding;
        panelStyle.ContentMarginRight = Padding;
        panelStyle.ContentMarginTop = 12;
        panelStyle.ContentMarginBottom = Padding;
        _sheet.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_sheet);

        // Scrollable content area
        var scroll = new GodotNative.ScrollContainer();
        scroll.HorizontalScrollMode = GodotNative.ScrollContainer.ScrollMode.Disabled;
        scroll.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical = GodotNative.Control.SizeFlags.ExpandFill;
        _sheet.AddChild(scroll);

        _contentBox = new GodotNative.VBoxContainer();
        _contentBox.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
        _contentBox.SizeFlagsVertical = GodotNative.Control.SizeFlags.ExpandFill;
        _contentBox.AddThemeConstantOverride("separation", 16);
        scroll.AddChild(_contentBox);

        // Drag handle (visual only)
        var handleContainer = new GodotNative.CenterContainer();
        handleContainer.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
        _contentBox.AddChild(handleContainer);

        var handle = new GodotNative.ColorRect();
        handle.CustomMinimumSize = new GodotNative.Vector2(HandleWidth, HandleHeight);
        handle.Color = new GodotNative.Color(0.4f, 0.4f, 0.42f, 1.0f);
        handleContainer.AddChild(handle);

        // Header row: icon + name + status badge
        var headerRow = new GodotNative.HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 12);
        headerRow.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
        _contentBox.AddChild(headerRow);

        _iconLabel = new GodotNative.Label();
        _iconLabel.AddThemeFontSizeOverride("font_size", IconFontSize);
        _iconLabel.VerticalAlignment = GodotNative.VerticalAlignment.Center;
        headerRow.AddChild(_iconLabel);

        var nameStatusCol = new GodotNative.VBoxContainer();
        nameStatusCol.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
        nameStatusCol.AddThemeConstantOverride("separation", 4);
        headerRow.AddChild(nameStatusCol);

        _deviceNameLabel = new GodotNative.Label();
        _deviceNameLabel.AddThemeFontSizeOverride("font_size", TitleFontSize);
        _deviceNameLabel.AddThemeColorOverride("font_color", new GodotNative.Color(1, 1, 1, 1));
        nameStatusCol.AddChild(_deviceNameLabel);

        _statusBadge = new GodotNative.Label();
        _statusBadge.AddThemeFontSizeOverride("font_size", 14);
        nameStatusCol.AddChild(_statusBadge);

        // Separator line
        var sep = new GodotNative.HSeparator();
        sep.AddThemeStyleboxOverride("separator", CreateSeparatorStyle());
        _contentBox.AddChild(sep);

        // Controls container (populated dynamically per device category)
        _controlsContainer = new GodotNative.VBoxContainer();
        _controlsContainer.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
        _controlsContainer.AddThemeConstantOverride("separation", 12);
        _contentBox.AddChild(_controlsContainer);

        // Bottom separator
        var sep2 = new GodotNative.HSeparator();
        sep2.AddThemeStyleboxOverride("separator", CreateSeparatorStyle());
        _contentBox.AddChild(sep2);

        // Room label (subtle, at bottom)
        _roomLabel = new GodotNative.Label();
        _roomLabel.AddThemeFontSizeOverride("font_size", 14);
        _roomLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.6f, 0.6f, 0.62f, 1));
        _roomLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _contentBox.AddChild(_roomLabel);

        // "More Options" placeholder button
        var moreBtn = CreateStyledButton("More Options", new GodotNative.Color(0.25f, 0.25f, 0.28f, 1));
        moreBtn.Pressed += () => GodotNative.GD.Print("[DeviceDetailModal] More Options pressed (placeholder).");
        _contentBox.AddChild(moreBtn);
    }

    // ── Populate header ──────────────────────────────────────────────────

    private void PopulateHeader(SmartDevice device, SmartHome home)
    {
        if (_iconLabel != null)
            _iconLabel.Text = GetCategoryEmoji(device.Category);

        if (_deviceNameLabel != null)
            _deviceNameLabel.Text = device.Label;

        if (_statusBadge != null)
        {
            _statusBadge.Text = device.Status switch
            {
                DeviceStatus.Online => "Online",
                DeviceStatus.Offline => "Offline",
                DeviceStatus.Error => "Error",
                DeviceStatus.Updating => "Updating...",
                _ => "Unknown"
            };
            _statusBadge.AddThemeColorOverride("font_color", device.Status switch
            {
                DeviceStatus.Online => new GodotNative.Color(0.3f, 0.69f, 0.31f, 1),
                DeviceStatus.Error => new GodotNative.Color(0.96f, 0.26f, 0.21f, 1),
                DeviceStatus.Updating => new GodotNative.Color(1.0f, 0.6f, 0.0f, 1),
                _ => new GodotNative.Color(0.6f, 0.6f, 0.6f, 1)
            });
        }

        if (_roomLabel != null)
        {
            var room = home.Rooms.Find(r => r.RoomId == device.RoomId);
            _roomLabel.Text = room != null ? $"Room: {room.Name}" : "";
        }
    }

    // ── Populate controls per category ───────────────────────────────────

    private void PopulateControls(SmartDevice device)
    {
        if (_controlsContainer == null) return;

        // Clear previous controls
        foreach (var child in _controlsContainer.GetChildren())
        {
            child.QueueFree();
        }

        switch (device.Category)
        {
            case DeviceCategory.Light:
                BuildLightControls(device);
                break;
            case DeviceCategory.Thermostat:
                BuildThermostatControls(device);
                break;
            case DeviceCategory.Lock:
                BuildLockControls(device);
                break;
            case DeviceCategory.Camera:
                BuildCameraControls(device);
                break;
            case DeviceCategory.Switch:
                BuildSwitchControls(device);
                break;
            case DeviceCategory.Television:
            case DeviceCategory.Speaker:
                BuildMediaControls(device);
                break;
            case DeviceCategory.Sensor:
                BuildSensorControls(device);
                break;
            default:
                BuildGenericControls(device);
                break;
        }
    }

    // ── Light controls: ON/OFF toggle + brightness slider ────────────────

    private void BuildLightControls(SmartDevice device)
    {
        if (_controlsContainer == null) return;

        bool isOn = GetCapabilityValue(device, "switch") == "on";

        var toggleBtn = CreateToggleButton(isOn, "ON", "OFF",
            new GodotNative.Color(0.3f, 0.69f, 0.31f, 1),
            new GodotNative.Color(0.4f, 0.4f, 0.42f, 1));
        toggleBtn.Pressed += () =>
        {
            string cmd = isOn ? "off" : "on";
            EmitSignal(SignalName.DeviceCommandIssued, device.DeviceId, "switch", cmd);
        };
        _controlsContainer.AddChild(toggleBtn);

        // Brightness slider
        var brightnessLabel = CreateSubLabel("Brightness");
        _controlsContainer.AddChild(brightnessLabel);

        var slider = new GodotNative.HSlider();
        slider.MinValue = 0;
        slider.MaxValue = 100;
        slider.Step = 1;
        slider.CustomMinimumSize = new GodotNative.Vector2(0, 40);
        slider.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;

        var brightnessVal = GetCapabilityValue(device, "switchLevel");
        slider.Value = float.TryParse(brightnessVal, out float bv) ? bv : 100;
        slider.ValueChanged += (val) =>
        {
            EmitSignal(SignalName.DeviceCommandIssued, device.DeviceId, "switchLevel", $"setLevel:{(int)val}");
        };
        _controlsContainer.AddChild(slider);
    }

    // ── Thermostat controls: temp display, +/- buttons, mode selector ───

    private void BuildThermostatControls(SmartDevice device)
    {
        if (_controlsContainer == null) return;

        var tempVal = GetCapabilityValue(device, "temperatureMeasurement");
        string tempDisplay = string.IsNullOrEmpty(tempVal) ? "--" : tempVal;

        // Large temperature display
        var tempLabel = new GodotNative.Label();
        tempLabel.Text = $"{tempDisplay}\u00B0F";
        tempLabel.AddThemeFontSizeOverride("font_size", TemperatureFontSize);
        tempLabel.AddThemeColorOverride("font_color", new GodotNative.Color(1, 1, 1, 1));
        tempLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _controlsContainer.AddChild(tempLabel);

        // +/- buttons row
        var tempRow = new GodotNative.HBoxContainer();
        tempRow.AddThemeConstantOverride("separation", 16);
        tempRow.Alignment = GodotNative.BoxContainer.AlignmentMode.Center;
        _controlsContainer.AddChild(tempRow);

        var minusBtn = CreateStyledButton(" - ", new GodotNative.Color(0.25f, 0.25f, 0.28f, 1));
        minusBtn.Pressed += () =>
        {
            EmitSignal(SignalName.DeviceCommandIssued, device.DeviceId,
                "thermostatCoolingSetpoint", "decrease");
        };
        tempRow.AddChild(minusBtn);

        var setpointVal = GetCapabilityValue(device, "thermostatCoolingSetpoint");
        string setpointDisplay = string.IsNullOrEmpty(setpointVal) ? "72" : setpointVal;

        var setpointLabel = new GodotNative.Label();
        setpointLabel.Text = $"Set: {setpointDisplay}\u00B0F";
        setpointLabel.AddThemeFontSizeOverride("font_size", 20);
        setpointLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.8f, 0.8f, 0.82f, 1));
        setpointLabel.VerticalAlignment = GodotNative.VerticalAlignment.Center;
        tempRow.AddChild(setpointLabel);

        var plusBtn = CreateStyledButton(" + ", new GodotNative.Color(0.25f, 0.25f, 0.28f, 1));
        plusBtn.Pressed += () =>
        {
            EmitSignal(SignalName.DeviceCommandIssued, device.DeviceId,
                "thermostatCoolingSetpoint", "increase");
        };
        tempRow.AddChild(plusBtn);

        // Mode selector row
        var modeLabel = CreateSubLabel("Mode");
        _controlsContainer.AddChild(modeLabel);

        var modeRow = new GodotNative.HBoxContainer();
        modeRow.AddThemeConstantOverride("separation", 8);
        modeRow.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
        _controlsContainer.AddChild(modeRow);

        string currentMode = GetCapabilityValue(device, "thermostatMode") ?? "auto";
        foreach (string mode in new[] { "Cool", "Heat", "Auto" })
        {
            bool selected = string.Equals(currentMode, mode, StringComparison.OrdinalIgnoreCase);
            var modeBtn = CreateStyledButton(mode,
                selected ? new GodotNative.Color(0.13f, 0.59f, 0.95f, 1)
                         : new GodotNative.Color(0.25f, 0.25f, 0.28f, 1));
            modeBtn.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
            string capturedMode = mode.ToLowerInvariant();
            modeBtn.Pressed += () =>
            {
                EmitSignal(SignalName.DeviceCommandIssued, device.DeviceId,
                    "thermostatMode", $"setThermostatMode:{capturedMode}");
            };
            modeRow.AddChild(modeBtn);
        }
    }

    // ── Lock controls: LOCK/UNLOCK with confirmation ─────────────────────

    private void BuildLockControls(SmartDevice device)
    {
        if (_controlsContainer == null) return;

        bool isLocked = GetCapabilityValue(device, "lock") == "locked";

        var lockBtn = CreateToggleButton(isLocked, "LOCKED", "UNLOCKED",
            new GodotNative.Color(0.45f, 0.15f, 0.63f, 1),  // Purple when locked
            new GodotNative.Color(0.96f, 0.26f, 0.21f, 1)); // Red when unlocked
        lockBtn.Pressed += () =>
        {
            string cmd = isLocked ? "unlock" : "lock";
            EmitSignal(SignalName.DeviceCommandIssued, device.DeviceId, "lock", cmd);
        };
        _controlsContainer.AddChild(lockBtn);

        // Confirmation warning label
        var warnLabel = new GodotNative.Label();
        warnLabel.Text = isLocked ? "Tap to unlock this device" : "Tap to lock this device";
        warnLabel.AddThemeFontSizeOverride("font_size", 14);
        warnLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.7f, 0.5f, 0.2f, 1));
        warnLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _controlsContainer.AddChild(warnLabel);
    }

    // ── Camera controls: View Feed + motion status ───────────────────────

    private void BuildCameraControls(SmartDevice device)
    {
        if (_controlsContainer == null) return;

        var motionVal = GetCapabilityValue(device, "motionSensor") ?? "inactive";
        var motionLabel = new GodotNative.Label();
        motionLabel.Text = $"Motion: {(motionVal == "active" ? "Detected" : "No motion")}";
        motionLabel.AddThemeFontSizeOverride("font_size", 18);
        motionLabel.AddThemeColorOverride("font_color",
            motionVal == "active"
                ? new GodotNative.Color(1.0f, 0.6f, 0.0f, 1)
                : new GodotNative.Color(0.6f, 0.6f, 0.62f, 1));
        motionLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _controlsContainer.AddChild(motionLabel);

        var feedBtn = CreateStyledButton("View Feed", new GodotNative.Color(0.13f, 0.59f, 0.95f, 1));
        feedBtn.Pressed += () =>
        {
            EmitSignal(SignalName.DeviceCommandIssued, device.DeviceId, "camera", "viewFeed");
        };
        _controlsContainer.AddChild(feedBtn);
    }

    // ── Switch controls: ON/OFF toggle ───────────────────────────────────

    private void BuildSwitchControls(SmartDevice device)
    {
        if (_controlsContainer == null) return;

        bool isOn = GetCapabilityValue(device, "switch") == "on";

        var toggleBtn = CreateToggleButton(isOn, "ON", "OFF",
            new GodotNative.Color(0.3f, 0.69f, 0.31f, 1),
            new GodotNative.Color(0.4f, 0.4f, 0.42f, 1));
        toggleBtn.Pressed += () =>
        {
            string cmd = isOn ? "off" : "on";
            EmitSignal(SignalName.DeviceCommandIssued, device.DeviceId, "switch", cmd);
        };
        _controlsContainer.AddChild(toggleBtn);
    }

    // ── Media controls (TV/Speaker): ON/OFF + volume slider ──────────────

    private void BuildMediaControls(SmartDevice device)
    {
        if (_controlsContainer == null) return;

        bool isOn = GetCapabilityValue(device, "switch") == "on";

        var toggleBtn = CreateToggleButton(isOn, "ON", "OFF",
            new GodotNative.Color(0.3f, 0.69f, 0.31f, 1),
            new GodotNative.Color(0.4f, 0.4f, 0.42f, 1));
        toggleBtn.Pressed += () =>
        {
            string cmd = isOn ? "off" : "on";
            EmitSignal(SignalName.DeviceCommandIssued, device.DeviceId, "switch", cmd);
        };
        _controlsContainer.AddChild(toggleBtn);

        // Volume slider
        var volLabel = CreateSubLabel("Volume");
        _controlsContainer.AddChild(volLabel);

        var slider = new GodotNative.HSlider();
        slider.MinValue = 0;
        slider.MaxValue = 100;
        slider.Step = 1;
        slider.CustomMinimumSize = new GodotNative.Vector2(0, 40);
        slider.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;

        var volVal = GetCapabilityValue(device, "audioVolume");
        slider.Value = float.TryParse(volVal, out float vv) ? vv : 50;
        slider.ValueChanged += (val) =>
        {
            EmitSignal(SignalName.DeviceCommandIssued, device.DeviceId, "audioVolume", $"setVolume:{(int)val}");
        };
        _controlsContainer.AddChild(slider);
    }

    // ── Sensor controls: read-only status display ────────────────────────

    private void BuildSensorControls(SmartDevice device)
    {
        if (_controlsContainer == null) return;

        // Display all capabilities as read-only values
        foreach (var kvp in device.Capabilities)
        {
            var cap = kvp.Value;
            var row = new GodotNative.HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;

            var capLabel = new GodotNative.Label();
            capLabel.Text = FormatCapabilityName(cap.CapabilityId);
            capLabel.AddThemeFontSizeOverride("font_size", 16);
            capLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.6f, 0.6f, 0.62f, 1));
            capLabel.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
            row.AddChild(capLabel);

            var valLabel = new GodotNative.Label();
            string unit = string.IsNullOrEmpty(cap.Unit) ? "" : $" {cap.Unit}";
            valLabel.Text = $"{cap.Value ?? "--"}{unit}";
            valLabel.AddThemeFontSizeOverride("font_size", 18);
            valLabel.AddThemeColorOverride("font_color", new GodotNative.Color(1, 1, 1, 1));
            valLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Right;
            row.AddChild(valLabel);

            _controlsContainer.AddChild(row);
        }

        // Last update time note
        var timeLabel = new GodotNative.Label();
        timeLabel.Text = "Read-only sensor data";
        timeLabel.AddThemeFontSizeOverride("font_size", 13);
        timeLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.45f, 0.45f, 0.48f, 1));
        timeLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _controlsContainer.AddChild(timeLabel);
    }

    // ── Generic controls: ON/OFF toggle ──────────────────────────────────

    private void BuildGenericControls(SmartDevice device)
    {
        if (_controlsContainer == null) return;

        bool isOn = GetCapabilityValue(device, "switch") == "on";

        var toggleBtn = CreateToggleButton(isOn, "ON", "OFF",
            new GodotNative.Color(0.3f, 0.69f, 0.31f, 1),
            new GodotNative.Color(0.4f, 0.4f, 0.42f, 1));
        toggleBtn.Pressed += () =>
        {
            string cmd = isOn ? "off" : "on";
            EmitSignal(SignalName.DeviceCommandIssued, device.DeviceId, "switch", cmd);
        };
        _controlsContainer.AddChild(toggleBtn);
    }

    // ── Overlay dismiss ──────────────────────────────────────────────────

    private void OnOverlayInput(GodotNative.InputEvent inputEvent)
    {
        if (inputEvent is GodotNative.InputEventMouseButton mb && mb.Pressed
            && mb.ButtonIndex == GodotNative.MouseButton.Left)
        {
            Hide();
            GetViewport().SetInputAsHandled();
        }
        if (inputEvent is GodotNative.InputEventScreenTouch touch && touch.Pressed)
        {
            Hide();
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Helper: create styled button ─────────────────────────────────────

    private GodotNative.Button CreateStyledButton(string text, GodotNative.Color bgColor)
    {
        var btn = new GodotNative.Button();
        btn.Text = text;
        btn.CustomMinimumSize = new GodotNative.Vector2(0, ButtonHeight);
        btn.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
        btn.AddThemeFontSizeOverride("font_size", ButtonFontSize);

        var style = new GodotNative.StyleBoxFlat();
        style.BgColor = bgColor;
        style.CornerRadiusTopLeft = 12;
        style.CornerRadiusTopRight = 12;
        style.CornerRadiusBottomLeft = 12;
        style.CornerRadiusBottomRight = 12;
        style.ContentMarginLeft = 16;
        style.ContentMarginRight = 16;
        style.ContentMarginTop = 8;
        style.ContentMarginBottom = 8;
        btn.AddThemeStyleboxOverride("normal", style);

        // Slightly lighter for hover
        var hoverStyle = new GodotNative.StyleBoxFlat();
        hoverStyle.BgColor = new GodotNative.Color(
            GodotNative.Mathf.Min(bgColor.R + 0.1f, 1.0f),
            GodotNative.Mathf.Min(bgColor.G + 0.1f, 1.0f),
            GodotNative.Mathf.Min(bgColor.B + 0.1f, 1.0f),
            bgColor.A);
        hoverStyle.CornerRadiusTopLeft = 12;
        hoverStyle.CornerRadiusTopRight = 12;
        hoverStyle.CornerRadiusBottomLeft = 12;
        hoverStyle.CornerRadiusBottomRight = 12;
        hoverStyle.ContentMarginLeft = 16;
        hoverStyle.ContentMarginRight = 16;
        hoverStyle.ContentMarginTop = 8;
        hoverStyle.ContentMarginBottom = 8;
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        // Pressed style
        var pressedStyle = new GodotNative.StyleBoxFlat();
        pressedStyle.BgColor = new GodotNative.Color(
            GodotNative.Mathf.Max(bgColor.R - 0.05f, 0.0f),
            GodotNative.Mathf.Max(bgColor.G - 0.05f, 0.0f),
            GodotNative.Mathf.Max(bgColor.B - 0.05f, 0.0f),
            bgColor.A);
        pressedStyle.CornerRadiusTopLeft = 12;
        pressedStyle.CornerRadiusTopRight = 12;
        pressedStyle.CornerRadiusBottomLeft = 12;
        pressedStyle.CornerRadiusBottomRight = 12;
        pressedStyle.ContentMarginLeft = 16;
        pressedStyle.ContentMarginRight = 16;
        pressedStyle.ContentMarginTop = 8;
        pressedStyle.ContentMarginBottom = 8;
        btn.AddThemeStyleboxOverride("pressed", pressedStyle);

        return btn;
    }

    private GodotNative.Button CreateToggleButton(bool isActive, string onText, string offText,
        GodotNative.Color onColor, GodotNative.Color offColor)
    {
        var bgColor = isActive ? onColor : offColor;
        var text = isActive ? onText : offText;
        return CreateStyledButton(text, bgColor);
    }

    private GodotNative.Label CreateSubLabel(string text)
    {
        var label = new GodotNative.Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new GodotNative.Color(0.6f, 0.6f, 0.62f, 1));
        return label;
    }

    private static GodotNative.StyleBoxFlat CreateSeparatorStyle()
    {
        var style = new GodotNative.StyleBoxFlat();
        style.BgColor = new GodotNative.Color(0.25f, 0.25f, 0.28f, 1);
        style.ContentMarginTop = 1;
        style.ContentMarginBottom = 1;
        return style;
    }

    // ── Data helpers ─────────────────────────────────────────────────────

    private static string? GetCapabilityValue(SmartDevice device, string capabilityId)
    {
        if (device.Capabilities.TryGetValue(capabilityId, out var cap))
            return cap.Value?.ToString();
        return null;
    }

    private static string GetCategoryEmoji(DeviceCategory category) => category switch
    {
        DeviceCategory.Light => "\U0001F4A1",
        DeviceCategory.Thermostat => "\u2744\uFE0F",
        DeviceCategory.Lock => "\U0001F512",
        DeviceCategory.Camera => "\U0001F4F7",
        DeviceCategory.Sensor => "\U0001F4E1",
        DeviceCategory.Switch => "\u2699\uFE0F",
        DeviceCategory.Television => "\U0001F4FA",
        DeviceCategory.Speaker => "\U0001F50A",
        DeviceCategory.Appliance => "\U0001F3E0",
        DeviceCategory.Hub => "\U0001F310",
        _ => "\u2699\uFE0F"
    };

    private static string FormatCapabilityName(string capabilityId)
    {
        // Convert camelCase to Title Case with spaces
        var result = new System.Text.StringBuilder();
        foreach (char c in capabilityId)
        {
            if (char.IsUpper(c) && result.Length > 0)
                result.Append(' ');
            result.Append(result.Length == 0 ? char.ToUpper(c) : c);
        }
        return result.ToString();
    }
}
