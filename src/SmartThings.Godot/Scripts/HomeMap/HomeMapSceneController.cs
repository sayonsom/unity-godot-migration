// =============================================================================
// HomeMapSceneController.cs — Root controller for the Home Map scene
// Wires assembler, camera, UI, accessibility, push-to-talk, and IoT events
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;
using SmartThings.Godot.Data;
using SmartThings.Godot.Scripts.Accessibility;
using SmartThings.Godot.Scripts.IoT;
using SmartThings.Godot.Scripts.Voice;
using SmartThings.Godot.Services;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// Root controller for the HomeMapScene. Initializes all components
/// and wires their signals together, including Phase 4 features:
///   - Push-to-talk voice commands
///   - TalkBack/screen reader accessibility
///   - Real-time IoT device events via MQTT
/// </summary>
public partial class HomeMapSceneController : GodotNative.Node3D
{
    private HomeMapAssembler? _assembler;
    private DevicePinManager? _pinManager;
    private IsometricCameraController? _camera;
    private HomeMapUI? _ui;

    // Phase 4: Accessibility + Voice + IoT
    private PushToTalkController? _pttController;
    private PushToTalkUI? _pttUI;
    private HomeMapAccessibilityManager? _a11yManager;
    private AccessibleDeviceAnnouncer? _deviceAnnouncer;
    private SmartThingsEventBus? _eventBus;

    // Services (would come from DI in production)
    private GodotAccessibilityService? _a11yService;
    private GodotAudioService? _audioService;

    private SmartHome? _home;

    public override void _Ready()
    {
        _assembler = GetNode<HomeMapAssembler>("HomeRoot");
        _pinManager = GetNode<DevicePinManager>("DevicePins");
        _camera = GetNode<IsometricCameraController>("IsometricCamera");
        _ui = GetNode<HomeMapUI>("UIOverlay/HomeMapUI");

        // Setup environment
        SetupEnvironment();

        // Wire UI signals
        _ui.ResetViewPressed += () => _camera?.ResetView();
        _assembler.RoomSelected += (roomId, roomName) => _ui.ShowRoomInfo(roomId, roomName);

        // Load mock home data
        _home = MockHomeProvider.CreateSampleHome();
        _ui.SetHome(_home);
        _assembler.BuildHome(_home);

        // Phase 4: Initialize accessibility, voice, and IoT
        InitializeAccessibility();
        InitializePushToTalk();
        InitializeEventBus();

        // Register rooms and devices for accessibility after they're built
        RegisterAccessibleElements();

        GodotNative.GD.Print("[HomeMapScene] Loaded with Phase 4: accessibility, voice, and IoT.");
    }

    // ── Phase 4: Accessibility ──────────────────────────────────────────────

    private void InitializeAccessibility()
    {
        // Create accessibility service
        _a11yService = new GodotAccessibilityService();
        AddChild(_a11yService);

        // Accessibility manager for 3D scene navigation
        _a11yManager = new HomeMapAccessibilityManager();
        AddChild(_a11yManager);
        _a11yManager.Initialize(_a11yService, _home!);

        // Device announcer for state change alerts
        _deviceAnnouncer = new AccessibleDeviceAnnouncer();
        AddChild(_deviceAnnouncer);
        _deviceAnnouncer.Initialize(_a11yService);

        // Wire accessibility navigation to camera
        _a11yManager.RoomFocused += OnAccessibilityRoomFocused;
        _a11yManager.DeviceFocused += OnAccessibilityDeviceFocused;

        // Register voice commands for accessibility
        RegisterVoiceCommands();

        GodotNative.GD.Print("[A11y] Accessibility system initialized");
    }

    private void RegisterAccessibleElements()
    {
        if (_a11yManager == null || _home == null) return;

        // Register each room
        foreach (var room in _home.Rooms)
        {
            var roomNode = _assembler?.GetRoomNode(room.RoomId);
            if (roomNode != null)
            {
                _a11yManager.RegisterRoom(roomNode, room);
            }
        }

        // Register each device
        foreach (var device in _home.Devices)
        {
            var pinNode = _pinManager?.GetPinNode(device.DeviceId);
            if (pinNode != null)
            {
                _a11yManager.RegisterDevice(pinNode, device);
            }
        }

        GodotNative.GD.Print($"[A11y] Registered {_home.Rooms.Count} rooms and {_home.Devices.Count} devices");
    }

    private void RegisterVoiceCommands()
    {
        var vcp = _a11yService?.VoiceCommands;
        if (vcp == null) return;

        vcp.RegisterCommand(new VoiceCommandPattern(
            "turn_on", new[] { "turn on {device}", "switch on {device}", "enable {device}" },
            "Turn on a device"));
        vcp.RegisterCommand(new VoiceCommandPattern(
            "turn_off", new[] { "turn off {device}", "switch off {device}", "disable {device}" },
            "Turn off a device"));
        vcp.RegisterCommand(new VoiceCommandPattern(
            "set_level", new[] { "set {device} to {value}", "dim {device} to {value}" },
            "Set device level"));
        vcp.RegisterCommand(new VoiceCommandPattern(
            "navigate", new[] { "go to {room}", "show {room}", "navigate to {room}" },
            "Navigate to a room"));
        vcp.RegisterCommand(new VoiceCommandPattern(
            "status", new[] { "what's the status of {device}", "is {device} on" },
            "Check device status"));
    }

    private void OnAccessibilityRoomFocused(string roomId)
    {
        _assembler?.SelectRoom(roomId);
        var room = _home?.Rooms.Find(r => r.RoomId == roomId);
        if (room != null)
        {
            _ui?.ShowRoomInfo(roomId, room.Name);

            // Announce room details
            var devices = _home?.Devices.Where(d => d.RoomId == roomId).ToList();
            if (devices != null)
                _deviceAnnouncer?.AnnounceRoomSummary(room, devices);
        }
    }

    private void OnAccessibilityDeviceFocused(string deviceId)
    {
        var device = _home?.Devices.Find(d => d.DeviceId == deviceId);
        if (device != null)
            _ui?.ShowDevicePopup(device);
    }

    // ── Phase 4: Push-to-Talk ───────────────────────────────────────────────

    private void InitializePushToTalk()
    {
        // Create audio service for microphone access
        _audioService = new GodotAudioService();
        AddChild(_audioService);

        // Push-to-talk controller (full pipeline)
        _pttController = new PushToTalkController();
        AddChild(_pttController);
        _pttController.SetHome(_home!);
        _pttController.SetMicrophone(_audioService.Microphone);

        // Push-to-talk UI overlay
        _pttUI = new PushToTalkUI();
        var uiLayer = GetNode<GodotNative.CanvasLayer>("UIOverlay");
        uiLayer.AddChild(_pttUI);
        _pttUI.SetController(_pttController);

        // Wire voice command execution to event bus
        _pttController.CommandExecuted += OnVoiceCommandExecuted;
        _pttController.IntentParsed += OnVoiceIntentParsed;

        GodotNative.GD.Print("[PTT] Push-to-talk system initialized");
    }

    private void OnVoiceCommandExecuted(string deviceLabel, string action)
    {
        GodotNative.GD.Print($"[PTT] Voice command executed: {action} on {deviceLabel}");

        // Announce via accessibility
        _a11yService?.Announce($"Executed: {action} on {deviceLabel}", AnnouncePriority.Normal);
    }

    private void OnVoiceIntentParsed(string description, float confidence)
    {
        // If it's a room navigation intent, move camera there
        if (description.StartsWith("Navigate to "))
        {
            var roomName = description["Navigate to ".Length..];
            var room = _home?.Rooms.Find(r =>
                r.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase));
            if (room != null)
            {
                _assembler?.SelectRoom(room.RoomId);
            }
        }
    }

    // ── Phase 4: IoT Event Bus ──────────────────────────────────────────────

    private void InitializeEventBus()
    {
        // Create network service for MQTT
        var networkService = new GodotNetworkService();
        AddChild(networkService);

        _eventBus = new SmartThingsEventBus();
        AddChild(_eventBus);
        _eventBus.Initialize(networkService, _home!);

        // Register a handler that bridges events to the scene
        _eventBus.AddHandler(new SceneDeviceEventHandler(this));

        // Wire device state changes to pin updates
        _eventBus.DeviceStateChanged += OnDeviceStateChanged;

        GodotNative.GD.Print("[IoT] Event bus initialized (connect via MQTT when broker available)");
    }

    private void OnDeviceStateChanged(string deviceId, string capability, string value)
    {
        // Update pin visual
        _pinManager?.UpdateDeviceState(deviceId, capability, value);

        // Announce change for accessibility
        var device = _home?.Devices.Find(d => d.DeviceId == deviceId);
        if (device != null)
        {
            _deviceAnnouncer?.OnDeviceStateChanged(device, device.Status.ToString(), value);
            _a11yManager?.AnnounceDeviceChange(device, $"{capability}: {value}");
        }
    }

    // ── Environment Setup ───────────────────────────────────────────────────

    private void SetupEnvironment()
    {
        var envNode = GetNode<GodotNative.WorldEnvironment>("WorldEnvironment");

        var env = new GodotNative.Environment();
        env.BackgroundMode = GodotNative.Environment.BGMode.Color;
        env.BackgroundColor = new GodotNative.Color(0.96f, 0.96f, 0.98f, 1.0f);
        env.AmbientLightSource = GodotNative.Environment.AmbientSource.Color;
        env.AmbientLightColor = new GodotNative.Color(1.0f, 1.0f, 1.0f, 1.0f);
        env.AmbientLightEnergy = 0.8f;
        env.TonemapMode = GodotNative.Environment.ToneMapper.Linear;

        envNode.Environment = env;
    }

    // ── Scene Event Handler ─────────────────────────────────────────────────

    private class SceneDeviceEventHandler : IDeviceEventHandler
    {
        private readonly HomeMapSceneController _controller;

        public SceneDeviceEventHandler(HomeMapSceneController controller)
            => _controller = controller;

        public void OnDeviceEvent(SmartDevice device, string capability, string value)
        {
            GodotNative.GD.Print($"[Scene] Device event: {device.Label} → {capability}={value}");
        }

        public void OnCommandSent(SmartDevice device, DeviceCommand command)
        {
            GodotNative.GD.Print($"[Scene] Command sent: {device.Label} → {command.CommandName}");
        }

        public void OnSceneExecuted(string sceneName)
        {
            GodotNative.GD.Print($"[Scene] Scene executed: {sceneName}");
            _controller._a11yService?.Announce(
                $"Scene {sceneName} activated", AnnouncePriority.Normal);
        }
    }
}
