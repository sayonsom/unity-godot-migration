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
        if (_devicePopup != null) _devicePopup.Visible = false;
    }

    /// <summary>Show device detail popup.</summary>
    public void ShowDevicePopup(SmartDevice device)
    {
        if (_devicePopup == null || _devicePopupLabel == null) return;

        _devicePopupLabel.Text = $"{device.Label}\n{device.Category} - {device.Status}";
        _devicePopup.Visible = true;
        if (_roomInfoPopup != null) _roomInfoPopup.Visible = false;
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
        // CRITICAL: Let touch events pass through to the 3D scene
        // The root Control must ignore mouse/touch so the camera can receive input
        MouseFilter = GodotNative.Control.MouseFilterEnum.Ignore;
        AnchorsPreset = (int)GodotNative.Control.LayoutPreset.FullRect;

        // === Top Status Bar ===
        var topBar = new GodotNative.PanelContainer();
        topBar.AnchorsPreset = (int)GodotNative.Control.LayoutPreset.TopWide;
        topBar.OffsetBottom = 60;
        // Top bar DOES capture input (for buttons)
        topBar.MouseFilter = GodotNative.Control.MouseFilterEnum.Stop;
        AddChild(topBar);

        // Dark semi-transparent background for top bar
        var topBarStyle = new GodotNative.StyleBoxFlat();
        topBarStyle.BgColor = new GodotNative.Color(0.15f, 0.15f, 0.2f, 0.85f);
        topBarStyle.ContentMarginLeft = 10;
        topBarStyle.ContentMarginRight = 10;
        topBarStyle.ContentMarginTop = 5;
        topBarStyle.ContentMarginBottom = 5;
        topBar.AddThemeStyleboxOverride("panel", topBarStyle);

        var topBarHbox = new GodotNative.HBoxContainer();
        topBar.AddChild(topBarHbox);

        var backLabel = new GodotNative.Label();
        backLabel.Text = "<";
        backLabel.AddThemeFontSizeOverride("font_size", 24);
        topBarHbox.AddChild(backLabel);

        _homeNameLabel = new GodotNative.Label();
        _homeNameLabel.Text = "My home";
        _homeNameLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _homeNameLabel.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
        _homeNameLabel.AddThemeFontSizeOverride("font_size", 22);
        topBarHbox.AddChild(_homeNameLabel);

        var settingsBtn = new GodotNative.Button();
        settingsBtn.Text = "Settings";
        topBarHbox.AddChild(settingsBtn);

        // === Room Info Popup (centered) ===
        _roomInfoPopup = new GodotNative.PanelContainer();
        _roomInfoPopup.AnchorsPreset = (int)GodotNative.Control.LayoutPreset.Center;
        _roomInfoPopup.OffsetLeft = -150;
        _roomInfoPopup.OffsetTop = -60;
        _roomInfoPopup.OffsetRight = 150;
        _roomInfoPopup.OffsetBottom = 60;
        _roomInfoPopup.Visible = false;
        _roomInfoPopup.MouseFilter = GodotNative.Control.MouseFilterEnum.Stop;
        AddChild(_roomInfoPopup);

        var popupStyle = new GodotNative.StyleBoxFlat();
        popupStyle.BgColor = new GodotNative.Color(0.1f, 0.1f, 0.15f, 0.9f);
        popupStyle.CornerRadiusTopLeft = 12;
        popupStyle.CornerRadiusTopRight = 12;
        popupStyle.CornerRadiusBottomLeft = 12;
        popupStyle.CornerRadiusBottomRight = 12;
        popupStyle.ContentMarginLeft = 20;
        popupStyle.ContentMarginRight = 20;
        popupStyle.ContentMarginTop = 15;
        popupStyle.ContentMarginBottom = 15;
        _roomInfoPopup.AddThemeStyleboxOverride("panel", popupStyle);

        var roomInfoVbox = new GodotNative.VBoxContainer();
        _roomInfoPopup.AddChild(roomInfoVbox);

        _roomNameLabel = new GodotNative.Label();
        _roomNameLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _roomNameLabel.AddThemeFontSizeOverride("font_size", 20);
        roomInfoVbox.AddChild(_roomNameLabel);

        _roomDeviceCountLabel = new GodotNative.Label();
        _roomDeviceCountLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _roomDeviceCountLabel.AddThemeFontSizeOverride("font_size", 16);
        roomInfoVbox.AddChild(_roomDeviceCountLabel);

        // === Device Popup ===
        _devicePopup = new GodotNative.PanelContainer();
        _devicePopup.AnchorsPreset = (int)GodotNative.Control.LayoutPreset.Center;
        _devicePopup.OffsetLeft = -150;
        _devicePopup.OffsetTop = -50;
        _devicePopup.OffsetRight = 150;
        _devicePopup.OffsetBottom = 50;
        _devicePopup.Visible = false;
        _devicePopup.MouseFilter = GodotNative.Control.MouseFilterEnum.Stop;
        _devicePopup.AddThemeStyleboxOverride("panel", popupStyle);
        AddChild(_devicePopup);

        _devicePopupLabel = new GodotNative.Label();
        _devicePopupLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _devicePopupLabel.AddThemeFontSizeOverride("font_size", 18);
        _devicePopup.AddChild(_devicePopupLabel);

        // === Bottom Tab Bar ===
        var bottomBar = new GodotNative.PanelContainer();
        bottomBar.AnchorsPreset = (int)GodotNative.Control.LayoutPreset.BottomWide;
        bottomBar.OffsetTop = -55;
        bottomBar.MouseFilter = GodotNative.Control.MouseFilterEnum.Stop;
        AddChild(bottomBar);

        var bottomStyle = new GodotNative.StyleBoxFlat();
        bottomStyle.BgColor = new GodotNative.Color(0.15f, 0.15f, 0.2f, 0.85f);
        bottomStyle.ContentMarginLeft = 5;
        bottomStyle.ContentMarginRight = 5;
        bottomStyle.ContentMarginTop = 5;
        bottomStyle.ContentMarginBottom = 5;
        bottomBar.AddThemeStyleboxOverride("panel", bottomStyle);

        var bottomHbox = new GodotNative.HBoxContainer();
        bottomBar.AddChild(bottomHbox);

        string[] tabs = { "Favorites", "Devices", "Routines", "Find", "Menu" };
        foreach (var tab in tabs)
        {
            var btn = new GodotNative.Button();
            btn.Text = tab;
            btn.SizeFlagsHorizontal = GodotNative.Control.SizeFlags.ExpandFill;
            bottomHbox.AddChild(btn);
        }

        // === Floating action buttons (top-right) ===
        var actionBox = new GodotNative.VBoxContainer();
        actionBox.AnchorsPreset = (int)GodotNative.Control.LayoutPreset.TopRight;
        actionBox.OffsetLeft = -130;
        actionBox.OffsetTop = 70;
        actionBox.OffsetRight = -10;
        actionBox.OffsetBottom = 170;
        actionBox.MouseFilter = GodotNative.Control.MouseFilterEnum.Ignore;
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
