// =============================================================================
// INetworkService.cs — Engine-agnostic networking abstraction
// Covers HTTP (SmartThings Cloud API), MQTT, CoAP, and Matter
// =============================================================================

namespace SmartThings.Abstraction.Interfaces;

/// <summary>
/// Network service covering all SmartThings communication protocols:
///   - HTTP/REST for SmartThings Cloud API
///   - MQTT for real-time device messaging
///   - CoAP for local network device control
///   - Matter for smart home standard protocol
/// </summary>
public interface INetworkService
{
    /// <summary>HTTP client for SmartThings Cloud REST API.</summary>
    ISmartThingsHttpClient Http { get; }

    /// <summary>MQTT client for real-time device pub/sub messaging.</summary>
    IMqttClient Mqtt { get; }

    /// <summary>CoAP client for local network device control.</summary>
    ICoapClient Coap { get; }

    /// <summary>Check if network is currently available.</summary>
    bool IsNetworkAvailable { get; }

    /// <summary>Fired on connectivity changes.</summary>
    event Action<NetworkStatus>? OnNetworkStatusChanged;
}

/// <summary>SmartThings Cloud API HTTP client.</summary>
public interface ISmartThingsHttpClient
{
    Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest body, CancellationToken ct = default);
    Task<bool> PutAsync<T>(string endpoint, T body, CancellationToken ct = default);
    Task<bool> DeleteAsync(string endpoint, CancellationToken ct = default);

    /// <summary>Set the SmartThings API bearer token.</summary>
    void SetAuthToken(string token);
}

/// <summary>
/// MQTT client for SmartThings real-time messaging.
/// Implementation: MQTTnet NuGet package (pure C#, works on all platforms).
/// </summary>
public interface IMqttClient
{
    bool IsConnected { get; }
    Task ConnectAsync(MqttConfig config, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task SubscribeAsync(string topic, MqttQoS qos = MqttQoS.AtLeastOnce, CancellationToken ct = default);
    Task PublishAsync(string topic, byte[] payload, MqttQoS qos = MqttQoS.AtLeastOnce, CancellationToken ct = default);

    event Action<MqttMessage>? OnMessageReceived;
    event Action<MqttConnectionEvent>? OnConnectionChanged;
}

/// <summary>CoAP client for local network IoT device control.</summary>
public interface ICoapClient
{
    Task<CoapResponse> GetAsync(string uri, CancellationToken ct = default);
    Task<CoapResponse> PostAsync(string uri, byte[] payload, CancellationToken ct = default);
    Task<CoapResponse> PutAsync(string uri, byte[] payload, CancellationToken ct = default);

    /// <summary>Observe a CoAP resource for real-time updates.</summary>
    Task<IDisposable> ObserveAsync(string uri, Action<CoapResponse> callback, CancellationToken ct = default);

    /// <summary>Discover devices on local network via multicast.</summary>
    Task<IReadOnlyList<CoapDevice>> DiscoverAsync(TimeSpan timeout, CancellationToken ct = default);
}

// --- Models ---

public record MqttConfig(
    string BrokerHost,
    int BrokerPort = 1883,
    string? Username = null,
    string? Password = null,
    bool UseTls = false,
    string ClientId = "smartthings-godot"
);

public enum MqttQoS { AtMostOnce = 0, AtLeastOnce = 1, ExactlyOnce = 2 }

public record MqttMessage(
    string Topic,
    byte[] Payload,
    MqttQoS QoS,
    DateTimeOffset Timestamp
);

public record MqttConnectionEvent(
    MqttConnectionState State,
    string? Reason = null
);

public enum MqttConnectionState { Connected, Disconnected, Reconnecting }

public record CoapResponse(
    int StatusCode,
    byte[] Payload,
    string ContentFormat
);

public record CoapDevice(
    string Uri,
    string Name,
    string[] ResourceTypes
);

public record NetworkStatus(
    bool IsAvailable,
    NetworkType Type,
    int SignalStrengthDbm
);

public enum NetworkType { None, WiFi, Cellular, Ethernet }
