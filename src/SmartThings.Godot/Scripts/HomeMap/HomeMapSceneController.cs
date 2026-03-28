// =============================================================================
// HomeMapSceneController.cs — Root controller for the Home Map scene
// Production version: wires assembler, camera, UI, accessibility, voice, IoT
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;
using SmartThings.Godot.Autoload;
using SmartThings.Godot.Scripts.Accessibility;
using SmartThings.Godot.Scripts.IoT;
using SmartThings.Godot.Scripts.Voice;
using SmartThings.Godot.Services;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// Root controller for the HomeMapScene. Initializes all subsystems
/// and wires their signals together for production use:
///   - Push-to-talk voice commands
///   - TalkBack/screen reader accessibility
///   - Real-time IoT device events via MQTT
///   - Device detail modal for device control
///
/// To load home data, call <see cref="LoadHome"/> with a SmartHome
/// obtained from the SmartThings Cloud API or DI container.
/// </summary>
public partial class HomeMapSceneController : GodotNative.Node3D
{
    private HomeMapAssembler? _assembler;
    private DevicePinManager? _pinManager;
    private IsometricCameraController? _camera;
    private HomeMapUI? _ui;

    // Accessibility + Voice + IoT
    private PushToTalkController? _pttController;
    private PushToTalkUI? _pttUI;
    private HomeMapAccessibilityManager? _a11yManager;
    private AccessibleDeviceAnnouncer? _deviceAnnouncer;
    private SmartThingsEventBus? _eventBus;
    private DeviceDetailModal? _deviceModal;

    // Services
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

        // Load home data from DI container or SmartThings API
        _home = ResolveHomeData();

        if (_home != null)
        {
            _ui.SetHome(_home);
            _assembler.BuildHome(_home);

            // Initialize all subsystems
            InitializeAccessibility();
            InitializePushToTalk();
            InitializeEventBus();
            RegisterAccessibleElements();
            InitializeDeviceModal();

            GodotNative.GD.Print($"[HomeMapScene] Production ready. {_home.Rooms.Count} rooms, {_home.Devices.Count} devices.");
        }
        else
        {
            GodotNative.GD.PushError("[HomeMapScene] No home data available. Call LoadHome() or register ISmartHomeProvider.");
        }
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Load or reload home data at runtime (e.g., after API fetch).
    /// Rebuilds the 3D scene and re-registers all accessibility elements.
    /// </summary>
    public void LoadHome(SmartHome home)
    {
        _home = home;
        _ui?.SetHome(home);
        _assembler?.BuildHome(home);
        _a11yManager?.Initialize(_a11yService!, home);
        _pttController?.SetHome(home);
        _eventBus?.Initialize(
            GameBootstrap.Resolve<INetworkService>() as GodotNetworkService ?? new GodotNetworkService(), home);
        RegisterAccessibleElements();

        GodotNative.GD.Print($"[HomeMapScene] Reloaded: {home.Rooms.Count} rooms, {home.Devices.Count} devices.");
    }

    // ── Data Resolution ─────────────────────────────────────────────────────

    /// <summary>
    /// Resolves home data from the DI container.
    /// Override this method or register an ISmartHomeProvider to supply real API data.
    /// </summary>
    private SmartHome? ResolveHomeData()
    {
        // Try to resolve from DI container (production: registered by app layer)
        try
        {
            var provider = GameBootstrap.TryResolve<ISmartHomeProvider>();
            if (provider != null)
            {
                GodotNative.GD.Print("[HomeMapScene] Loading home from ISmartHomeProvider...");
                return provider.GetCurrentHome();
            }
        }
        catch (InvalidOperationException)
        {
            // GameBootstrap not initialized yet — ok, fall through
        }

        GodotNative.GD.PushWarning("[HomeMapScene] No ISmartHomeProvider registered. Register one in GameBootstrap or call LoadHome().");
        return null;
    }

    // ── Device Detail Modal ─────────────────────────────────────────────────

    private void InitializeDeviceModal()
    {
        _deviceModal = new DeviceDetailModal();
        var uiLayer = GetNode<GodotNative.CanvasLayer>("UIOverlay");
        uiLayer.AddChild(_deviceModal);

        // Wire device pin taps to open the modal (both Area3D and raycast)
        _pinManager!.DevicePinTapped += OnDevicePinTapped;
        _assembler!.DeviceTapped += OnDevicePinTapped;

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
            _a11yService?.Announce(
                $"{device.Label}. {device.Category}. Tap controls to operate.",
                AnnouncePriority.Normal);
        }
    }

    // ── Accessibility ───────────────────────────────────────────────────────

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
                _a11yManager.RegisterRoom(roomNode, room);
        }

        // Register each device
        foreach (var device in _home.Devices)
        {
            var pinNode = _pinManager?.GetPinNode(device.DeviceId);
            if (pinNode != null)
                _a11yManager.RegisterDevice(pinNode, device);
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
        if (_isHandlingSelection) return;
        _isHandlingSelection = true;

        try
        {
            _ui?.ShowRoomInfo(roomId, roomName);
            _a11yManager?.FocusElementSilent(roomId);

            // ONE clean TTS announcement — room name + device summary
            var room = _home?.Rooms.Find(r => r.RoomId == roomId);
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

    private void OnAccessibilityRoomFocused(string roomId)
    {
        if (_isHandlingSelection) return;
        _isHandlingSelection = true;

        try
        {
            _assembler?.SelectRoom(roomId);
            var room = _home?.Rooms.Find(r => r.RoomId == roomId);
            if (room != null)
                _ui?.ShowRoomInfo(roomId, room.Name);
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
        }
        finally
        {
            _isHandlingSelection = false;
        }
    }

    // ── Push-to-Talk ────────────────────────────────────────────────────────

    private void InitializePushToTalk()
    {
        _audioService = new GodotAudioService();
        AddChild(_audioService);

        _pttController = new PushToTalkController();
        AddChild(_pttController);
        _pttController.SetHome(_home!);
        _pttController.SetMicrophone(_audioService.Microphone);

        _pttUI = new PushToTalkUI();
        var uiLayer = GetNode<GodotNative.CanvasLayer>("UIOverlay");
        uiLayer.AddChild(_pttUI);
        _pttUI.SetController(_pttController);

        _pttController.CommandExecuted += OnVoiceCommandExecuted;
        _pttController.IntentParsed += OnVoiceIntentParsed;

        GodotNative.GD.Print("[PTT] Push-to-talk system initialized");
    }

    private void OnVoiceCommandExecuted(string deviceLabel, string action)
    {
        GodotNative.GD.Print($"[PTT] Voice command executed: {action} on {deviceLabel}");
        _a11yService?.Announce($"Executed: {action} on {deviceLabel}", AnnouncePriority.Normal);
    }

    private void OnVoiceIntentParsed(string description, float confidence)
    {
        if (description.StartsWith("Navigate to "))
        {
            var roomName = description["Navigate to ".Length..];
            var room = _home?.Rooms.Find(r =>
                r.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase));
            if (room != null)
                _assembler?.SelectRoom(room.RoomId);
        }
    }

    // ── IoT Event Bus ───────────────────────────────────────────────────────

    private void InitializeEventBus()
    {
        var networkService = new GodotNetworkService();
        AddChild(networkService);

        _eventBus = new SmartThingsEventBus();
        AddChild(_eventBus);
        _eventBus.Initialize(networkService, _home!);
        _eventBus.AddHandler(new SceneDeviceEventHandler(this));
        _eventBus.DeviceStateChanged += OnDeviceStateChanged;

        GodotNative.GD.Print("[IoT] Event bus initialized");
    }

    private void OnDeviceStateChanged(string deviceId, string capability, string value)
    {
        _pinManager?.UpdateDeviceState(deviceId, capability, value);

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
