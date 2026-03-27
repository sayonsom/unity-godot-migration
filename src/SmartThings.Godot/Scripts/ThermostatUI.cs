// =============================================================================
// ThermostatUI.cs — Thermostat UI overlay controller
// Manages Control nodes: temperature display, mode selector, PTT button
// =============================================================================

using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;
using SmartThings.Godot.Autoload;
using SmartThings.Godot.Data;
using GodotNative = Godot;

namespace SmartThings.Godot.Scripts;

/// <summary>
/// Controls the thermostat UI overlay (CanvasLayer).
/// Replaces Unity UGUI with Godot Control nodes.
///
/// Unity → Godot UI mapping:
///   Canvas → CanvasLayer
///   Text/TMP → Label
///   Slider → HSlider
///   Dropdown → OptionButton
///   Button → Button
///   LayoutGroup → HBoxContainer/VBoxContainer
/// </summary>
public partial class ThermostatUI : GodotNative.Control
{
    private MockDeviceProvider _deviceProvider = null!;
    private IAccessibilityService _accessibility = null!;
    private IAudioService _audio = null!;
    private ISceneService _scene = null!;

    private GodotNative.Label? _currentTempLabel;
    private GodotNative.Label? _targetTempLabel;
    private GodotNative.Label? _statusLabel;
    private GodotNative.Label? _onlineIndicator;
    private GodotNative.HSlider? _tempSlider;
    private GodotNative.OptionButton? _modeSelector;
    private GodotNative.Button? _pttButton;
    private GodotNative.Button? _backButton;

    private bool _isPTTActive;

    public override void _Ready()
    {
        // Resolve services
        _deviceProvider = GameBootstrap.Resolve<MockDeviceProvider>();
        _accessibility = GameBootstrap.Resolve<IAccessibilityService>();
        _audio = GameBootstrap.Resolve<IAudioService>();
        _scene = GameBootstrap.Resolve<ISceneService>();

        // Find UI nodes
        _currentTempLabel = GetNode<GodotNative.Label>("TemperatureDisplay/CurrentTemp");
        _targetTempLabel = GetNode<GodotNative.Label>("TemperatureDisplay/TargetTemp");
        _statusLabel = GetNode<GodotNative.Label>("TopBar/StatusLabel");
        _onlineIndicator = GetNode<GodotNative.Label>("TopBar/OnlineIndicator");
        _tempSlider = GetNode<GodotNative.HSlider>("TemperatureDisplay/TempSlider");
        _modeSelector = GetNode<GodotNative.OptionButton>("TemperatureDisplay/ModeSelector");
        _pttButton = GetNode<GodotNative.Button>("BottomBar/PTTButton");
        _backButton = GetNode<GodotNative.Button>("BottomBar/BackButton");

        // Setup mode selector
        SetupModeSelector();

        // Wire signals
        _tempSlider!.ValueChanged += OnTemperatureChanged;
        _modeSelector!.ItemSelected += OnModeSelected;
        _pttButton!.ButtonDown += OnPTTPressed;
        _pttButton!.ButtonUp += OnPTTReleased;
        _backButton!.Pressed += OnBackPressed;

        // Set initial slider value
        _tempSlider.Value = _deviceProvider.TargetTemperature;

        // Register UI elements for accessibility
        RegisterAccessibility();

        // Listen for device state changes
        _deviceProvider.OnDeviceStateChanged += OnDeviceStateChanged;
    }

    public override void _Process(double delta)
    {
        // Update temperature display every frame
        _currentTempLabel!.Text = $"{_deviceProvider.CurrentTemperature:F1}°F";
        _targetTempLabel!.Text = $"Target: {_deviceProvider.TargetTemperature:F0}°F";

        // Update online status
        var device = _deviceProvider.GetThermostat();
        _onlineIndicator!.Text = device.Status == DeviceStatus.Online
            ? "● Online"
            : "○ Offline";
    }

    private void SetupModeSelector()
    {
        _modeSelector!.Clear();
        _modeSelector.AddItem("Cool", 0);
        _modeSelector.AddItem("Heat", 1);
        _modeSelector.AddItem("Auto", 2);
        _modeSelector.AddItem("Off", 3);

        // Set current mode
        var currentMode = _deviceProvider.Mode.ToLowerInvariant();
        _modeSelector.Selected = currentMode switch
        {
            "cool" => 0,
            "heat" => 1,
            "auto" => 2,
            "off" => 3,
            _ => 0
        };
    }

    private void OnTemperatureChanged(double value)
    {
        _deviceProvider.SetTargetTemperature((float)value);
        _accessibility.Announce($"Temperature set to {value:F0} degrees.");
    }

    private void OnModeSelected(long index)
    {
        var mode = index switch
        {
            0 => "cool",
            1 => "heat",
            2 => "auto",
            3 => "off",
            _ => "cool"
        };

        _deviceProvider.SetMode(mode);
        _accessibility.Announce($"Mode changed to {mode}.");
    }

    private void OnPTTPressed()
    {
        _isPTTActive = true;
        _pttButton!.Text = "Listening...";
        _accessibility.Announce("Listening for voice command.");

        // Start mic capture
        var mic = GameBootstrap.Resolve<IAudioService>().Microphone;
        _ = mic.StartCaptureAsync(new MicrophoneConfig());
    }

    private async void OnPTTReleased()
    {
        if (!_isPTTActive) return;
        _isPTTActive = false;
        _pttButton!.Text = "Hold to Talk";

        // Stop mic capture and process
        var mic = _audio.Microphone;
        var buffer = await mic.StopCaptureAsync();

        if (buffer.Samples.Length > 0)
        {
            // In production: send buffer to STT service, then process utterance
            // For demo: log that we captured audio
            GodotNative.GD.Print($"[PTT] Captured {buffer.Duration.TotalSeconds:F1}s of audio " +
                                 $"({buffer.Samples.Length} samples)");
            _accessibility.Announce("Voice command processing not yet connected to STT service.");
        }
    }

    private void OnBackPressed()
    {
        _ = _scene.LoadSceneAsync("res://Scenes/MainMenu.tscn", SceneTransition.Fade);
    }

    private void OnDeviceStateChanged(DeviceStateChangedEvent evt)
    {
        GodotNative.GD.Print($"[UI] Device state changed: {evt.CapabilityId} = {evt.NewValue}");
    }

    private void RegisterAccessibility()
    {
        // Set accessibility metadata on UI controls
        _tempSlider!.SetMeta("accessible_name", "Temperature Slider");
        _tempSlider.SetMeta("accessible_description", "Adjust target temperature between 60 and 90 degrees");

        _modeSelector!.SetMeta("accessible_name", "Mode Selector");
        _modeSelector.SetMeta("accessible_description", "Choose thermostat mode: cool, heat, auto, or off");

        _pttButton!.SetMeta("accessible_name", "Push to Talk");
        _pttButton.SetMeta("accessible_description", "Hold to speak a voice command");
    }
}
