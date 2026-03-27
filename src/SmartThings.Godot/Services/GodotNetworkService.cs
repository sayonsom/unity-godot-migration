// =============================================================================
// GodotNetworkService.cs — Godot 4.5 implementation of INetworkService
// HTTP (SmartThings Cloud), MQTT (MQTTnet), CoAP (UDP), connectivity
// =============================================================================

using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using SmartThings.Abstraction.Interfaces;
using GodotNative = Godot;

namespace SmartThings.Godot.Services;

/// <summary>
/// Godot backend for INetworkService.
/// Uses pure C# networking (HttpClient, MQTTnet, UDP sockets) — no Godot HTTPRequest needed.
/// This works identically on Windows, Android, and Linux.
/// </summary>
public partial class GodotNetworkService : GodotNative.Node, INetworkService
{
    private SmartThingsHttpClient? _http;
    private GodotMqttClient? _mqtt;
    private GodotCoapClient? _coap;
    private bool _isNetworkAvailable = true;
    private readonly System.Timers.Timer _connectivityTimer;

    public ISmartThingsHttpClient Http => _http ??= new SmartThingsHttpClient();
    public IMqttClient Mqtt => _mqtt ??= new GodotMqttClient();
    public ICoapClient Coap => _coap ??= new GodotCoapClient();
    public bool IsNetworkAvailable => _isNetworkAvailable;

    public event Action<NetworkStatus>? OnNetworkStatusChanged;

    public GodotNetworkService()
    {
        _connectivityTimer = new System.Timers.Timer(30000); // Check every 30s
        _connectivityTimer.Elapsed += async (_, _) => await CheckConnectivityAsync();
        _connectivityTimer.AutoReset = true;
    }

    public override void _Ready()
    {
        _connectivityTimer.Start();
        _ = CheckConnectivityAsync();
    }

    public override void _ExitTree()
    {
        _connectivityTimer.Stop();
        _connectivityTimer.Dispose();
        (_http as IDisposable)?.Dispose();
        _ = _mqtt?.DisconnectAsync();
    }

    private async Task CheckConnectivityAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync("https://api.smartthings.com/v1/health");
            var wasAvailable = _isNetworkAvailable;
            _isNetworkAvailable = response.IsSuccessStatusCode;

            if (wasAvailable != _isNetworkAvailable)
            {
                var status = new NetworkStatus(_isNetworkAvailable, NetworkType.WiFi, -50);
                OnNetworkStatusChanged?.Invoke(status);
            }
        }
        catch
        {
            if (_isNetworkAvailable)
            {
                _isNetworkAvailable = false;
                OnNetworkStatusChanged?.Invoke(new NetworkStatus(false, NetworkType.None, -100));
            }
        }
    }
}

// =============================================================================
// SmartThingsHttpClient — REST API client for SmartThings Cloud
// =============================================================================

internal class SmartThingsHttpClient : ISmartThingsHttpClient, IDisposable
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public SmartThingsHttpClient()
    {
        _client = new HttpClient
        {
            BaseAddress = new Uri("https://api.smartthings.com/v1/")
        };
        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void SetAuthToken(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        var response = await _client.GetAsync(endpoint, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string endpoint, TRequest body, CancellationToken ct = default)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(body, JsonOptions),
            Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(endpoint, content, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<TResponse>(json, JsonOptions);
    }

    public async Task<bool> PutAsync<T>(string endpoint, T body, CancellationToken ct = default)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(body, JsonOptions),
            Encoding.UTF8, "application/json");
        var response = await _client.PutAsync(endpoint, content, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(string endpoint, CancellationToken ct = default)
    {
        var response = await _client.DeleteAsync(endpoint, ct);
        return response.IsSuccessStatusCode;
    }

    public void Dispose() => _client.Dispose();
}

// =============================================================================
// GodotMqttClient — MQTT client wrapping MQTTnet for real-time device messaging
// =============================================================================

internal class GodotMqttClient : Abstraction.Interfaces.IMqttClient
{
    private MQTTnet.Client.IMqttClient? _client;
    private MqttClientOptions? _options;

    public bool IsConnected => _client?.IsConnected ?? false;

    public event Action<MqttMessage>? OnMessageReceived;
    public event Action<MqttConnectionEvent>? OnConnectionChanged;

    public async Task ConnectAsync(MqttConfig config, CancellationToken ct = default)
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(config.BrokerHost, config.BrokerPort)
            .WithClientId(config.ClientId);

        if (!string.IsNullOrEmpty(config.Username))
            builder.WithCredentials(config.Username, config.Password);

        if (config.UseTls)
            builder.WithTlsOptions(o => o.UseTls());

        _options = builder.Build();

        // Wire up events
        _client.ApplicationMessageReceivedAsync += e =>
        {
            OnMessageReceived?.Invoke(new MqttMessage(
                e.ApplicationMessage.Topic,
                e.ApplicationMessage.PayloadSegment.ToArray(),
                (MqttQoS)(int)e.ApplicationMessage.QualityOfServiceLevel,
                DateTimeOffset.UtcNow));
            return Task.CompletedTask;
        };

        _client.ConnectedAsync += _ =>
        {
            OnConnectionChanged?.Invoke(new MqttConnectionEvent(MqttConnectionState.Connected));
            return Task.CompletedTask;
        };

        _client.DisconnectedAsync += e =>
        {
            OnConnectionChanged?.Invoke(new MqttConnectionEvent(
                MqttConnectionState.Disconnected, e.Reason.ToString()));

            // Auto-reconnect
            if (e.ClientWasConnected && _options != null)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000, ct);
                    if (!ct.IsCancellationRequested)
                    {
                        OnConnectionChanged?.Invoke(
                            new MqttConnectionEvent(MqttConnectionState.Reconnecting));
                        try { await _client.ConnectAsync(_options, ct); }
                        catch { /* Will retry on next disconnect */ }
                    }
                }, ct);
            }
            return Task.CompletedTask;
        };

        await _client.ConnectAsync(_options, ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_client?.IsConnected == true)
        {
            await _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), ct);
        }
    }

    public async Task SubscribeAsync(string topic, MqttQoS qos = MqttQoS.AtLeastOnce, CancellationToken ct = default)
    {
        if (_client == null) throw new InvalidOperationException("MQTT client not connected.");

        var options = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(topic, (MQTTnet.Protocol.MqttQualityOfServiceLevel)(int)qos)
            .Build();

        await _client.SubscribeAsync(options, ct);
    }

    public async Task PublishAsync(string topic, byte[] payload, MqttQoS qos = MqttQoS.AtLeastOnce, CancellationToken ct = default)
    {
        if (_client == null) throw new InvalidOperationException("MQTT client not connected.");

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)(int)qos)
            .Build();

        await _client.PublishAsync(message, ct);
    }
}

// =============================================================================
// GodotCoapClient — Lightweight CoAP over UDP for local device control
// =============================================================================

internal class GodotCoapClient : ICoapClient
{
    private const int CoapDefaultPort = 5683;

    // CoAP message types
    private const byte CoapVersion = 1;
    private const byte TypeConfirmable = 0;
    private const byte MethodGet = 1;
    private const byte MethodPost = 2;
    private const byte MethodPut = 3;

    private ushort _messageId;

    public async Task<CoapResponse> GetAsync(string uri, CancellationToken ct = default)
    {
        return await SendCoapRequestAsync(uri, MethodGet, null, ct);
    }

    public async Task<CoapResponse> PostAsync(string uri, byte[] payload, CancellationToken ct = default)
    {
        return await SendCoapRequestAsync(uri, MethodPost, payload, ct);
    }

    public async Task<CoapResponse> PutAsync(string uri, byte[] payload, CancellationToken ct = default)
    {
        return await SendCoapRequestAsync(uri, MethodPut, payload, ct);
    }

    public Task<IDisposable> ObserveAsync(string uri, Action<CoapResponse> callback, CancellationToken ct = default)
    {
        // CoAP Observe: send GET with Observe option = 0
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var observeTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var response = await GetAsync(uri, cts.Token);
                    callback(response);
                    await Task.Delay(5000, cts.Token); // Poll interval for simple implementation
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(10000, cts.Token); }
            }
        }, cts.Token);

        return Task.FromResult<IDisposable>(new CoapObserveHandle(cts));
    }

    public async Task<IReadOnlyList<CoapDevice>> DiscoverAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var devices = new List<CoapDevice>();

        try
        {
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;

            // CoAP multicast discovery: GET /.well-known/core
            var discoveryMessage = BuildCoapMessage(MethodGet, "/.well-known/core", null);
            var multicastEndpoint = new IPEndPoint(IPAddress.Parse("224.0.1.187"), CoapDefaultPort);

            await udp.SendAsync(discoveryMessage, discoveryMessage.Length, multicastEndpoint);

            var deadline = DateTime.UtcNow + timeout;
            udp.Client.ReceiveTimeout = (int)timeout.TotalMilliseconds;

            while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
            {
                try
                {
                    var result = await udp.ReceiveAsync(ct);
                    var payload = Encoding.UTF8.GetString(result.Buffer);
                    var device = ParseCoapDiscoveryResponse(result.RemoteEndPoint, payload);
                    if (device != null) devices.Add(device);
                }
                catch (SocketException) { break; }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (Exception ex)
        {
            GodotNative.GD.PushWarning($"CoAP discovery failed: {ex.Message}");
        }

        return devices.AsReadOnly();
    }

    private async Task<CoapResponse> SendCoapRequestAsync(
        string uri, byte method, byte[]? payload, CancellationToken ct)
    {
        var parsed = new Uri(uri.StartsWith("coap://") ? uri : $"coap://{uri}");
        var host = parsed.Host;
        var port = parsed.Port > 0 ? parsed.Port : CoapDefaultPort;
        var path = parsed.AbsolutePath;

        var message = BuildCoapMessage(method, path, payload);

        using var udp = new UdpClient();
        var endpoint = new IPEndPoint(IPAddress.Parse(host), port);

        await udp.SendAsync(message, message.Length, endpoint);

        udp.Client.ReceiveTimeout = 5000;
        var result = await udp.ReceiveAsync(ct);

        // Parse CoAP response (simplified)
        var responseCode = result.Buffer.Length > 1 ? result.Buffer[1] : 0;
        var responsePayload = result.Buffer.Length > 4
            ? result.Buffer[4..]
            : Array.Empty<byte>();

        return new CoapResponse(responseCode, responsePayload, "application/json");
    }

    private byte[] BuildCoapMessage(byte method, string path, byte[]? payload)
    {
        var messageId = Interlocked.Increment(ref _messageId);
        var token = BitConverter.GetBytes(messageId);

        // CoAP header: Version(2) | Type(2) | Token Length(4) | Code(8) | Message ID(16)
        var header = new byte[]
        {
            (byte)((CoapVersion << 6) | (TypeConfirmable << 4) | (token.Length & 0x0F)),
            method,
            (byte)(messageId >> 8),
            (byte)(messageId & 0xFF)
        };

        using var ms = new MemoryStream();
        ms.Write(header);
        ms.Write(token);

        // Add Uri-Path options
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var segBytes = Encoding.UTF8.GetBytes(segment);
            // Option delta=11 (Uri-Path), length
            ms.WriteByte((byte)(0xB0 | (segBytes.Length & 0x0F)));
            ms.Write(segBytes);
        }

        // Payload marker + payload
        if (payload != null && payload.Length > 0)
        {
            ms.WriteByte(0xFF); // Payload marker
            ms.Write(payload);
        }

        return ms.ToArray();
    }

    private static CoapDevice? ParseCoapDiscoveryResponse(IPEndPoint endpoint, string payload)
    {
        // Parse CoRE Link Format: </device>;rt="smart.device";ct=50
        var uri = $"coap://{endpoint.Address}:{endpoint.Port}";
        var resourceTypes = new List<string>();
        var name = endpoint.Address.ToString();

        foreach (var attr in payload.Split(';'))
        {
            var trimmed = attr.Trim();
            if (trimmed.StartsWith("rt="))
                resourceTypes.Add(trimmed[4..^1]);
            if (trimmed.StartsWith("title="))
                name = trimmed[7..^1];
        }

        return new CoapDevice(uri, name, resourceTypes.ToArray());
    }
}

internal class CoapObserveHandle : IDisposable
{
    private readonly CancellationTokenSource _cts;
    public CoapObserveHandle(CancellationTokenSource cts) => _cts = cts;
    public void Dispose() => _cts.Cancel();
}
