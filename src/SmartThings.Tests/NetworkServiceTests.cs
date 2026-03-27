using System.Text;
using System.Text.Json;
using SmartThings.Abstraction.Interfaces;

namespace SmartThings.Tests;

/// <summary>
/// Tests for networking models and serialization logic.
/// Actual MQTT/CoAP connections require a running broker, so we test
/// the pure-C# parts: config, serialization, model behavior.
/// </summary>
public class NetworkServiceTests
{
    [Fact]
    public void MqttConfig_CustomValues_AreRetained()
    {
        var config = new MqttConfig(
            BrokerHost: "broker.example.com",
            BrokerPort: 8883,
            Username: "user",
            Password: "pass",
            UseTls: true,
            ClientId: "test-client");

        Assert.Equal("broker.example.com", config.BrokerHost);
        Assert.Equal(8883, config.BrokerPort);
        Assert.Equal("user", config.Username);
        Assert.Equal("pass", config.Password);
        Assert.True(config.UseTls);
        Assert.Equal("test-client", config.ClientId);
    }

    [Fact]
    public void MqttQoS_HasCorrectIntValues()
    {
        Assert.Equal(0, (int)MqttQoS.AtMostOnce);
        Assert.Equal(1, (int)MqttQoS.AtLeastOnce);
        Assert.Equal(2, (int)MqttQoS.ExactlyOnce);
    }

    [Fact]
    public void MqttConnectionEvent_RecordsDisconnectReason()
    {
        var evt = new MqttConnectionEvent(
            MqttConnectionState.Disconnected, "Server unavailable");
        Assert.Equal(MqttConnectionState.Disconnected, evt.State);
        Assert.Equal("Server unavailable", evt.Reason);
    }

    [Fact]
    public void MqttMessage_PayloadCanBeJsonSerialized()
    {
        var deviceState = new { Switch = "on", Brightness = 80 };
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(deviceState));

        var msg = new MqttMessage("devices/001", payload,
            MqttQoS.AtLeastOnce, DateTimeOffset.UtcNow);

        var deserialized = JsonSerializer.Deserialize<JsonElement>(
            Encoding.UTF8.GetString(msg.Payload));
        Assert.Equal("on", deserialized.GetProperty("Switch").GetString());
        Assert.Equal(80, deserialized.GetProperty("Brightness").GetInt32());
    }

    [Fact]
    public void CoapDevice_DiscoveryResult_HasExpectedFormat()
    {
        var device = new CoapDevice(
            "coap://192.168.1.100:5683",
            "SmartThings Hub",
            new[] { "oic.d.smartthings", "oic.r.hub" });

        Assert.Contains("192.168.1.100", device.Uri);
        Assert.Equal(2, device.ResourceTypes.Length);
        Assert.Contains("oic.d.smartthings", device.ResourceTypes);
    }

    [Fact]
    public void NetworkType_CoversAllExpectedTypes()
    {
        var types = Enum.GetValues<NetworkType>();
        Assert.Contains(NetworkType.None, types);
        Assert.Contains(NetworkType.WiFi, types);
        Assert.Contains(NetworkType.Cellular, types);
        Assert.Contains(NetworkType.Ethernet, types);
    }

    [Fact]
    public void NetworkStatus_WiFi_IsTypical()
    {
        var status = new NetworkStatus(true, NetworkType.WiFi, -55);
        Assert.True(status.IsAvailable);
        Assert.Equal(NetworkType.WiFi, status.Type);
        Assert.InRange(status.SignalStrengthDbm, -90, -20);
    }
}
