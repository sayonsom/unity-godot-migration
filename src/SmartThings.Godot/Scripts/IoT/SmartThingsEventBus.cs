// =============================================================================
// SmartThingsEventBus.cs — Real-time device event subscription via MQTT
// Subscribes to SmartThings device events and dispatches to the scene
// =============================================================================

using System.Text;
using System.Text.Json;
using GodotNative = Godot;
using SmartThings.Abstraction;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.IoT;

/// <summary>
/// SmartThings real-time event bus using MQTT.
///
/// Subscribes to device state topics:
///   - smartthings/{homeId}/device/{deviceId}/status
///   - smartthings/{homeId}/device/+/event
///   - smartthings/{homeId}/scene/+/executed
///
/// Dispatches events to registered handlers for UI updates,
/// accessibility announcements, and device pin state changes.
/// </summary>
public partial class SmartThingsEventBus : GodotNative.Node
{
    private INetworkService? _network;
    private SmartHome? _home;
    private bool _isConnected;
    private readonly List<IDeviceEventHandler> _handlers = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Fired when a device state changes (for any listener).</summary>
    [GodotNative.Signal] public delegate void DeviceStateChangedEventHandler(
        string deviceId, string capability, string value);

    /// <summary>Fired when connection state changes.</summary>
    [GodotNative.Signal] public delegate void ConnectionStateChangedEventHandler(bool connected);

    public bool IsConnected => _isConnected;

    /// <summary>Initialize with network service and home data.</summary>
    public void Initialize(INetworkService network, SmartHome home)
    {
        _network = network;
        _home = home;

        // Listen for MQTT messages
        network.Mqtt.OnMessageReceived += OnMqttMessage;
        network.Mqtt.OnConnectionChanged += OnConnectionChanged;
    }

    /// <summary>Register a handler for device events.</summary>
    public void AddHandler(IDeviceEventHandler handler) => _handlers.Add(handler);

    /// <summary>
    /// Connect to MQTT broker and subscribe to device event topics.
    /// Call this when you have a valid SmartThings API token.
    /// </summary>
    public async Task ConnectAndSubscribeAsync(string brokerHost, int port = 1883,
        string? username = null, string? password = null)
    {
        if (_network == null || _home == null) return;

        try
        {
            var config = new MqttConfig(
                BrokerHost: brokerHost,
                BrokerPort: port,
                ClientId: $"godot-smartthings-{_home.Id}",
                Username: username ?? "",
                Password: password ?? "",
                UseTls: port == 8883);

            await _network.Mqtt.ConnectAsync(config);

            // Subscribe to all device events for this home
            await _network.Mqtt.SubscribeAsync($"smartthings/{_home.Id}/device/+/status");
            await _network.Mqtt.SubscribeAsync($"smartthings/{_home.Id}/device/+/event");
            await _network.Mqtt.SubscribeAsync($"smartthings/{_home.Id}/scene/+/executed");

            _isConnected = true;
            EmitSignal(SignalName.ConnectionStateChanged, true);

            GodotNative.GD.Print($"[EventBus] Connected to MQTT broker, subscribed to home '{_home.Id}'");
        }
        catch (Exception ex)
        {
            GodotNative.GD.PushWarning($"[EventBus] MQTT connection failed: {ex.Message}");
            _isConnected = false;
        }
    }

    /// <summary>
    /// Send a device command via MQTT.
    /// Used by push-to-talk to execute voice commands.
    /// </summary>
    public async Task SendDeviceCommandAsync(SmartDevice device, DeviceCommand command)
    {
        if (_network == null || _home == null || !_isConnected)
        {
            GodotNative.GD.Print($"[EventBus] Command (offline): {device.Label} → {command.CommandName}");
            // Even offline, notify handlers for UI update
            foreach (var handler in _handlers)
                handler.OnCommandSent(device, command);
            return;
        }

        var topic = $"smartthings/{_home.Id}/device/{device.DeviceId}/command";
        var payload = JsonSerializer.Serialize(new
        {
            deviceId = command.DeviceId,
            capability = command.CapabilityId,
            command = command.CommandName,
            arguments = command.Arguments
        }, JsonOpts);

        await _network.Mqtt.PublishAsync(topic, Encoding.UTF8.GetBytes(payload));

        foreach (var handler in _handlers)
            handler.OnCommandSent(device, command);

        GodotNative.GD.Print($"[EventBus] Sent command: {device.Label} → {command.CommandName}");
    }

    /// <summary>
    /// Simulate a device event (for testing without MQTT broker).
    /// </summary>
    public void SimulateDeviceEvent(string deviceId, string capability, string value)
    {
        DispatchDeviceEvent(deviceId, capability, value);
    }

    // ── MQTT Message Handler ────────────────────────────────────────────────

    private void OnMqttMessage(MqttMessage message)
    {
        try
        {
            var topic = message.Topic;
            var payload = Encoding.UTF8.GetString(message.Payload);

            // Parse topic: smartthings/{homeId}/device/{deviceId}/status
            var parts = topic.Split('/');
            if (parts.Length < 4) return;

            if (parts[2] == "device" && parts.Length >= 5)
            {
                var deviceId = parts[3];
                var eventType = parts[4];

                if (eventType == "status" || eventType == "event")
                {
                    var eventData = JsonSerializer.Deserialize<DeviceEventPayload>(payload, JsonOpts);
                    if (eventData != null)
                    {
                        // Dispatch on main thread
                        CallDeferred(nameof(DispatchDeviceEvent),
                            deviceId, eventData.Capability ?? "", eventData.Value ?? "");
                    }
                }
            }
            else if (parts[2] == "scene" && parts.Length >= 5 && parts[4] == "executed")
            {
                var sceneName = parts[3];
                CallDeferred(nameof(DispatchSceneEvent), sceneName);
            }
        }
        catch (Exception ex)
        {
            GodotNative.GD.PushWarning($"[EventBus] Message parse error: {ex.Message}");
        }
    }

    private void DispatchDeviceEvent(string deviceId, string capability, string value)
    {
        EmitSignal(SignalName.DeviceStateChanged, deviceId, capability, value);

        var device = _home?.Devices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (device == null) return;

        foreach (var handler in _handlers)
            handler.OnDeviceEvent(device, capability, value);

        GodotNative.GD.Print($"[EventBus] Device event: {device.Label} → {capability}={value}");
    }

    private void DispatchSceneEvent(string sceneName)
    {
        foreach (var handler in _handlers)
            handler.OnSceneExecuted(sceneName);

        GodotNative.GD.Print($"[EventBus] Scene executed: {sceneName}");
    }

    private void OnConnectionChanged(MqttConnectionEvent evt)
    {
        var wasConnected = _isConnected;
        _isConnected = evt.State == MqttConnectionState.Connected;

        if (wasConnected != _isConnected)
        {
            CallDeferred(nameof(EmitConnectionChange), _isConnected);
        }
    }

    private void EmitConnectionChange(bool connected)
    {
        EmitSignal(SignalName.ConnectionStateChanged, connected);
    }

    public override void _ExitTree()
    {
        if (_network != null)
        {
            _network.Mqtt.OnMessageReceived -= OnMqttMessage;
            _network.Mqtt.OnConnectionChanged -= OnConnectionChanged;
        }
    }
}

// ── Event payload DTOs ──────────────────────────────────────────────────────

internal class DeviceEventPayload
{
    public string? Capability { get; set; }
    public string? Attribute { get; set; }
    public string? Value { get; set; }
    public string? Unit { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
}

// ── Event handler interface ─────────────────────────────────────────────────

public interface IDeviceEventHandler
{
    void OnDeviceEvent(SmartDevice device, string capability, string value);
    void OnCommandSent(SmartDevice device, DeviceCommand command);
    void OnSceneExecuted(string sceneName);
}
