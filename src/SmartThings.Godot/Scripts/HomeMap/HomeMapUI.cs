// =============================================================================
// HomeMapUI.cs — UI overlay for the 3D Home Map View
// Top status bar, room info popup, bottom tabs, minimap
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// UI overlay controller for the Home Map View.
/// Manages the status bar, room info popup, bottom navigation, and minimap.
/// </summary>
public partial class HomeMapUI : GodotNative.Control
{
    // ── Node references ──────────────────────────────────────────────────────

    private GodotNative.Label? _homeNameLabel;
    private GodotNative.PanelContainer? _roomInfoPopup;
    private GodotNative.Label? _roomNameLabel;
    private GodotNative.Label? _roomDeviceCountLabel;
    private GodotNative.Label? _devicePopupLabel;
    private GodotNative.PanelContainer? _devicePopup;
    private GodotNative.Button? _resetViewBtn;
    private GodotNative.Button? _toggleViewBtn;
    private GodotNative.Button? _backBtn;

    private SmartHome? _home;
    private bool _is3DView = true;

    /// <summary>Fired when back button is pressed.</summary>
    [GodotNative.Signal] public delegate void BackPressedEventHandler();

    /// <summary>Fired when reset view button is pressed.</summary>
    [GodotNative.Signal] public delegate void ResetViewPressedEventHandler();

    /// <summary>Fired when 3D/2D toggle is pressed.</summary>
    [GodotNative.Signal] public delegate void ToggleViewPressedEventHandler(bool is3D);

    public override void _Ready()
    {
        BuildUI();
        GodotNative.GD.Print("[HomeMapUI] Ready.");
    }

    /// <summary>Set the home data for display.</summary>
    public void SetHome(SmartHome home)
    {
        _home = home;
        if (_homeNameLabel != null)
            _homeNameLabel.Text = home.Name;
    }

    /// <summary>Show room info popup when a room is selected.</summary>
    public void ShowRoomInfo(string roomId, string roomName)
    {
        if (_roomInfoPopup == null) return;

        var room = _home?.Rooms.Find(r => r.RoomId == roomId);
        int deviceCount = room?.DeviceIds?.Count ?? room?.Devices.Count ?? 0;

        if (_roomNameLabel != null)
            _roomNameLabel.Text = roomName;
        if (_roomDeviceCountLabel != null)
            _roomDeviceCountLabel.Text = $"{deviceCount} device{(deviceCount != 1 ? "s" : "")}";

        _roomInfoPopup.Visible = true;
        _devicePopup!.Visible = false;
    }

    /// <summary>Show device detail popup.</summary>
    public void ShowDevicePopup(SmartDevice device)
    {
        if (_devicePopup == null || _devicePopupLabel == null) return;

        _devicePopupLabel.Text = $"{device.Label}\n{device.Category} - {device.Status}";
        _devicePopup.Visible = true;
        _roomInfoPopup!.Visible = false;
    }

    /// <summary>Hide all popups.</summary>
    public void HidePopups()
    {
        if (_roomInfoPopup != null) _roomInfoPopup.Visible = false;
        if (_devicePopup != null) _devicePopup.Visible = false;
    }

    // ── UI Construction ──────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Make this Control fill the entire screen
        AnchorsPreset = (int)GodotNative.Control.LayoutPreset.FullRect;

        // === Top Status Bar ===
        var topBar = new GodotNative.HBoxContainer();
        topBar.AnchorsPreset = (int)GodotNative.Control.LayoutPreset.TopWide;
        topBar.OffsetBottom = 50;
        AddChild(topBar);

        _backBtn = new GodotNative.Button();
        _backBtn.Text = "< Back";
        _backBtn.Pressed += () => EmitSignal(SignalName.BackPressed);
        topBar.AddChild(_backBtn);

        _homeNameLabel = new GodotNative.Label();
        _homeNameLabel.Text = "My Home";
        _homeNameLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _homeNameLabel.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
        topBar.AddChild(_homeNameLabel);

        var settingsBtn = new GodotNative.Button();
        settingsBtn.Text = "Settings";
        topBar.AddChild(settingsBtn);

        // === Room Info Popup (centered) ===
        _roomInfoPopup = new GodotNative.PanelContainer();
        _roomInfoPopup.AnchorsPreset = (int)GodotNative.Control.LayoutPreset.Center;
        _roomInfoPopup.OffsetLeft = -120;
        _roomInfoPopup.OffsetTop = -50;
        _roomInfoPopup.OffsetRight = 120;
        _roomInfoPopup.OffsetBottom = 50;
        _roomInfoPopup.Visible = false;
        AddChild(_roomInfoPopup);

        var roomInfoVbox = new GodotNative.VBoxContainer();
        _roomInfoPopup.AddChild(roomInfoVbox);

        _roomNameLabel = new GodotNative.Label();
        _roomNameLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        roomInfoVbox.AddChild(_roomNameLabel);

        _roomDeviceCountLabel = new GodotNative.Label();
        _roomDeviceCountLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        roomInfoVbox.AddChild(_roomDeviceCountLabel);

        // === Device Popup ===
        _devicePopup = new GodotNative.PanelContainer();
        _devicePopup.AnchorsPreset = (int)GodotNative.Control.LayoutPreset.Center;
        _devicePopup.OffsetLeft = -120;
        _devicePopup.OffsetTop = -40;
        _devicePopup.OffsetRight = 120;
        _devicePopup.OffsetBottom = 40;
        _devicePopup.Visible = false;
        AddChild(_devicePopup);

        _devicePopupLabel = new GodotNative.Label();
        _devicePopupLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _devicePopup.AddChild(_devicePopupLabel);

        // === Bottom Controls ===
        var bottomBar = new GodotNative.HBoxContainer();
        bottomBar.AnchorsPreset = (int)GodotNative.Control.LayoutPreset.BottomWide;
        bottomBar.OffsetTop = -50;
        bottomBar.Alignment = GodotNative.BoxContainer.AlignmentMode.Center;
        AddChild(bottomBar);

        string[] tabs = { "Favorites", "Devices", "Routines", "Find", "Menu" };
        foreach (var tab in tabs)
        {
            var btn = new GodotNative.Button();
            btn.Text = tab;
            btn.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
            bottomBar.AddChild(btn);
        }

        // === Floating action buttons (top-right) ===
        var actionBox = new GodotNative.VBoxContainer();
        actionBox.AnchorsPreset = (int)GodotNative.Control.LayoutPreset.TopRight;
        actionBox.OffsetLeft = -100;
        actionBox.OffsetTop = 60;
        actionBox.OffsetRight = -10;
        actionBox.OffsetBottom = 160;
        AddChild(actionBox);

        _resetViewBtn = new GodotNative.Button();
        _resetViewBtn.Text = "Reset View";
        _resetViewBtn.Pressed += () => EmitSignal(SignalName.ResetViewPressed);
        actionBox.AddChild(_resetViewBtn);

        _toggleViewBtn = new GodotNative.Button();
        _toggleViewBtn.Text = "2D View";
        _toggleViewBtn.Pressed += () =>
        {
            _is3DView = !_is3DView;
            _toggleViewBtn.Text = _is3DView ? "2D View" : "3D View";
            EmitSignal(SignalName.ToggleViewPressed, _is3DView);
        };
        actionBox.AddChild(_toggleViewBtn);
    }
}
