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
using SmartThings.Godot.Scripts.Performance;
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
    private AccessibilityTestPanel? _a11yTestPanel;
    private SmartThingsEventBus? _eventBus;
    private DeviceDetailModal? _deviceModal;
    private AndroidProfiler? _profiler;

    // Services (would come from DI in production)
    private GodotAccessibilityService? _a11yService;
    private GodotAudioService? _audioService;

    private SmartHome? _home;

    // Guard against infinite signal loops (room selected → focus → room focused → select → ...)
    private bool _isHandlingSelection;

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
        _assembler.RoomSelected += OnRoomSelected;

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

        // Phase 5: Device detail modal + performance profiler
        InitializeDeviceModal();
        InitializeProfiler();

        GodotNative.GD.Print("[HomeMapScene] Loaded with Phase 5: device controls, profiler, CI/CD.");
    }

    // ── Phase 5: Device Detail Modal ──────────────────────────────────────────

    private void InitializeDeviceModal()
    {
        _deviceModal = new DeviceDetailModal();
        var uiLayerModal = GetNode<GodotNative.CanvasLayer>("UIOverlay");
        uiLayerModal.AddChild(_deviceModal);

        // Wire device pin taps to open the modal
        _pinManager!.DevicePinTapped += OnDevicePinTapped;

        // Wire modal commands to event bus
        _deviceModal.DeviceCommandIssued += (deviceId, capability, command) =>
        {
            GodotNative.GD.Print($"[Device] Command: {deviceId} → {capability}.{command}");
            _eventBus?.SimulateDeviceEvent(deviceId, capability, command);
            _a11yService?.Announce($"{command} sent", AnnouncePriority.Normal);
        };
    }

    private void OnDevicePinTapped(string deviceId)
    {
        var device = _home?.Devices.Find(d => d.DeviceId == deviceId);
        if (device != null && _home != null)
        {
            _deviceModal?.ShowDevice(device, _home);
            _a11yService?.Announce($"{device.Label}. {device.Category}. Tap controls to operate.", AnnouncePriority.Normal);
        }
    }

    // ── Phase 5: Performance Profiler ─────────────────────────────────────────

    private void InitializeProfiler()
    {
        _profiler = new AndroidProfiler();
        var uiLayerPerf = GetNode<GodotNative.CanvasLayer>("UIOverlay");
        uiLayerPerf.AddChild(_profiler);
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

        // Accessibility test panel (on-screen buttons for testing on phone)
        _a11yTestPanel = new AccessibilityTestPanel();
        var uiLayerA11y = GetNode<GodotNative.CanvasLayer>("UIOverlay");
        uiLayerA11y.AddChild(_a11yTestPanel);
        _a11yTestPanel.Initialize(_a11yManager, _a11yService, _home!);

        // Wire test panel signals
        _a11yTestPanel.TestVoiceCommand += OnTestVoiceCommand;
        _a11yTestPanel.TestDeviceEvent += OnTestDeviceEvent;

        // Wire focus changes back to the test panel
        _a11yManager.RoomFocused += (roomId) =>
        {
            var room = _home?.Rooms.Find(r => r.RoomId == roomId);
            if (room != null)
            {
                var idx = _home!.Rooms.IndexOf(room);
                _a11yTestPanel.UpdateFocusInfo(room.Name, "Room", idx, _home.Rooms.Count);
            }
        };
        _a11yManager.DeviceFocused += (deviceId) =>
        {
            var device = _home?.Devices.Find(d => d.DeviceId == deviceId);
            if (device != null)
            {
                var idx = _home!.Devices.IndexOf(device);
                _a11yTestPanel.UpdateFocusInfo(device.Label, "Device", idx, _home.Devices.Count);
            }
        };

        GodotNative.GD.Print("[A11y] Accessibility system initialized with test panel");
    }

    private void OnTestVoiceCommand(string command)
    {
        GodotNative.GD.Print($"[A11y Test] Voice command: \"{command}\"");

        // Feed directly to intent parser pipeline
        var intent = new SmartHomeIntentParser();
        intent.SetHome(_home!);
        var parsed = intent.Parse(command);

        if (parsed != null)
        {
            _a11yService?.Announce($"Parsed: {parsed.Description}", AnnouncePriority.Normal);
            _a11yTestPanel?.SetStatus($"Intent: {parsed.Type} — {parsed.Description}");

            // Execute navigation intents
            if (parsed.Type == IntentType.RoomNavigation && parsed.Room != null)
            {
                _assembler?.SelectRoom(parsed.Room.RoomId);
                _a11yManager?.FocusElement(parsed.Room.RoomId);
            }
        }
        else
        {
            _a11yService?.Announce($"Could not parse: {command}", AnnouncePriority.Normal);
            _a11yTestPanel?.SetStatus($"No intent for: \"{command}\"");
        }
    }

    private void OnTestDeviceEvent(string deviceId, string capability, string value)
    {
        GodotNative.GD.Print($"[A11y Test] Device event: {deviceId} → {capability}={value}");
        _eventBus?.SimulateDeviceEvent(deviceId, capability, value);
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

    /// <summary>
    /// Called when user taps a room on the 3D map.
    /// Single consolidated announcement — no cascading TTS.
    /// </summary>
    private void OnRoomSelected(string roomId, string roomName)
    {
        if (_isHandlingSelection) return; // break infinite loop
        _isHandlingSelection = true;

        try
        {
            // Show room info in UI
            _ui?.ShowRoomInfo(roomId, roomName);

            // Move focus ring (but suppress its TTS — we'll do our own)
            _a11yManager?.FocusElementSilent(roomId);

            // Update test panel focus info
            var room = _home?.Rooms.Find(r => r.RoomId == roomId);
            if (room != null)
            {
                var idx = _home!.Rooms.IndexOf(room);
                _a11yTestPanel?.UpdateFocusInfo(room.Name, "Room", idx, _home.Rooms.Count);
            }

            // ONE clean TTS announcement — room name + device summary
            if (room != null && _a11yService != null)
            {
                var devices = _home?.Devices.Where(d => d.RoomId == roomId).ToList();
                int count = devices?.Count ?? 0;

                string announcement = $"{room.Name}.";
                if (count > 0)
                {
                    var deviceNames = string.Join(", ", devices!.Take(4).Select(d => d.Label));
                    announcement += $" {count} device{(count != 1 ? "s" : "")}: {deviceNames}.";
                }

                _a11yService.Announce(announcement, AnnouncePriority.Normal);
            }
        }
        finally
        {
            _isHandlingSelection = false;
        }
    }

    /// <summary>
    /// Called from accessibility panel navigation (Prev/Next/Select buttons).
    /// Updates scene visuals without re-triggering the selection loop.
    /// </summary>
    private void OnAccessibilityRoomFocused(string roomId)
    {
        if (_isHandlingSelection) return; // break infinite loop
        _isHandlingSelection = true;

        try
        {
            _assembler?.SelectRoom(roomId);
            var room = _home?.Rooms.Find(r => r.RoomId == roomId);
            if (room != null)
                _ui?.ShowRoomInfo(roomId, room.Name);
            // TTS already handled by HomeMapAccessibilityManager.ApplyFocus()
        }
        finally
        {
            _isHandlingSelection = false;
        }
    }

    private void OnAccessibilityDeviceFocused(string deviceId)
    {
        if (_isHandlingSelection) return;
        _isHandlingSelection = true;

        try
        {
            var device = _home?.Devices.Find(d => d.DeviceId == deviceId);
            if (device != null)
                _ui?.ShowDevicePopup(device);
            // TTS already handled by HomeMapAccessibilityManager.ApplyFocus()
        }
        finally
        {
            _isHandlingSelection = false;
        }
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
