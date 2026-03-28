using SmartThings.Abstraction;
using SmartThings.Abstraction.Interfaces;

namespace SmartThings.Tests;

/// <summary>
/// Tests that all abstraction interfaces and their supporting types
/// are well-formed and usable without any engine dependency.
/// </summary>
public class InterfaceContractTests
{
    // --- ISceneService contracts ---

    [Fact]
    public void SceneTransition_HasExpectedValues()
    {
        Assert.Equal(0, (int)SceneTransition.Instant);
        Assert.Equal(1, (int)SceneTransition.Fade);
        Assert.Equal(2, (int)SceneTransition.CrossFade);
        Assert.Equal(3, (int)SceneTransition.SlideLeft);
        Assert.Equal(4, (int)SceneTransition.Custom);
    }

    [Fact]
    public void SceneTransitionEvent_CanBeCreated()
    {
        var evt = new SceneTransitionEvent("scene_a", "scene_b",
            SceneTransition.Fade, SceneTransitionPhase.Starting);

        Assert.Equal("scene_a", evt.FromScene);
        Assert.Equal("scene_b", evt.ToScene);
        Assert.Equal(SceneTransition.Fade, evt.Transition);
        Assert.Equal(SceneTransitionPhase.Starting, evt.Phase);
    }

    [Fact]
    public void ProcessMode_HasExpectedValues()
    {
        Assert.Equal(0, (int)ProcessMode.Idle);
        Assert.Equal(1, (int)ProcessMode.Physics);
    }

    // --- IInputService contracts ---

    [Fact]
    public void InputBinding_CanBeCreated()
    {
        var binding = new InputBinding(InputDeviceType.Keyboard, "Space");
        Assert.Equal(InputDeviceType.Keyboard, binding.Device);
        Assert.Equal("Space", binding.Key);
    }

    [Fact]
    public void InputActionEvent_CanBeCreated()
    {
        var evt = new InputActionEvent("select", InputActionPhase.Started, 1.0f);
        Assert.Equal("select", evt.ActionName);
        Assert.Equal(InputActionPhase.Started, evt.Phase);
        Assert.Equal(1.0f, evt.Strength);
    }

    [Fact]
    public void PointerEvent_CanBeCreated_WithDefaultPointerId()
    {
        var evt = new PointerEvent(new Vector2(100, 200), PointerEventType.Down);
        Assert.Equal(100, evt.Position.X);
        Assert.Equal(200, evt.Position.Y);
        Assert.Equal(PointerEventType.Down, evt.Type);
        Assert.Equal(0, evt.PointerId);
    }

    [Fact]
    public void PointerEvent_CanBeCreated_WithMultiTouch()
    {
        var evt = new PointerEvent(new Vector2(50, 60), PointerEventType.Move, 2);
        Assert.Equal(2, evt.PointerId);
    }

    [Fact]
    public void RaycastResult_CanBeCreated()
    {
        var result = new RaycastResult(
            new Vector3(1, 2, 3),
            new Vector3(0, 1, 0),
            null,
            5.0f
        );
        Assert.Equal(5.0f, result.Distance);
        Assert.Null(result.HitNode);
    }

    // --- IAudioService contracts ---

    [Fact]
    public void MicrophoneConfig_HasSensibleDefaults()
    {
        var config = new MicrophoneConfig();
        Assert.Equal(16000, config.SampleRate);
        Assert.Equal(1, config.ChannelCount);
        Assert.True(config.EnableVAD);
        Assert.Equal(0.5f, config.VADThreshold);
        Assert.Equal(1500f, config.SilenceTimeoutMs);
    }

    [Fact]
    public void AudioBuffer_CanBeCreated()
    {
        var samples = new float[] { 0.1f, 0.2f, -0.1f, 0.05f };
        var buffer = new AudioBuffer(samples, 16000, 1, TimeSpan.FromSeconds(0.00025));
        Assert.Equal(4, buffer.Samples.Length);
        Assert.Equal(16000, buffer.SampleRate);
        Assert.Equal(1, buffer.ChannelCount);
    }

    [Fact]
    public void VoiceActivityEvent_CanBeCreated()
    {
        var evt = new VoiceActivityEvent(
            VoiceActivityState.SpeechStarted, 0.95f, TimeSpan.FromMilliseconds(500));
        Assert.Equal(VoiceActivityState.SpeechStarted, evt.State);
        Assert.Equal(0.95f, evt.Confidence);
    }

    [Fact]
    public void AudioFrame_CanBeCreated()
    {
        var frame = new AudioFrame(new float[] { 0.5f }, 16000, 0.5f, 0.35f);
        Assert.Equal(0.5f, frame.PeakAmplitude);
        Assert.Equal(0.35f, frame.RmsLevel);
    }

    // --- INetworkService contracts ---

    [Fact]
    public void MqttConfig_HasSensibleDefaults()
    {
        var config = new MqttConfig("mqtt.smartthings.com");
        Assert.Equal("mqtt.smartthings.com", config.BrokerHost);
        Assert.Equal(1883, config.BrokerPort);
        Assert.Null(config.Username);
        Assert.False(config.UseTls);
        Assert.Equal("smartthings-godot", config.ClientId);
    }

    [Fact]
    public void MqttMessage_CanBeCreated()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("{\"switch\":\"on\"}");
        var msg = new MqttMessage("devices/001/status", payload,
            MqttQoS.AtLeastOnce, DateTimeOffset.UtcNow);
        Assert.Equal("devices/001/status", msg.Topic);
        Assert.Equal(MqttQoS.AtLeastOnce, msg.QoS);
    }

    [Fact]
    public void CoapResponse_CanBeCreated()
    {
        var resp = new CoapResponse(205, new byte[] { 1, 2, 3 }, "application/json");
        Assert.Equal(205, resp.StatusCode);
        Assert.Equal(3, resp.Payload.Length);
    }

    [Fact]
    public void CoapDevice_CanBeCreated()
    {
        var device = new CoapDevice(
            "coap://192.168.1.50:5683",
            "Smart Bulb",
            new[] { "oic.r.light", "oic.r.switch" });
        Assert.Equal("Smart Bulb", device.Name);
        Assert.Equal(2, device.ResourceTypes.Length);
    }

    [Fact]
    public void NetworkStatus_CanBeCreated()
    {
        var status = new NetworkStatus(true, NetworkType.WiFi, -45);
        Assert.True(status.IsAvailable);
        Assert.Equal(NetworkType.WiFi, status.Type);
        Assert.Equal(-45, status.SignalStrengthDbm);
    }

    // --- IAccessibilityService contracts ---

    [Fact]
    public void AccessibleInfo_CanBeCreated()
    {
        var info = new AccessibleInfo(
            "Living Room Light",
            "Smart light, currently on at 80% brightness",
            AccessibleRole.Device3D,
            "80%");
        Assert.Equal("Living Room Light", info.Name);
        Assert.Equal(AccessibleRole.Device3D, info.Role);
        Assert.Equal("80%", info.Value);
    }

    [Fact]
    public void VoiceCommandPattern_CanBeCreated()
    {
        var pattern = new VoiceCommandPattern(
            "turn_on",
            new[] { "turn on {device}", "switch on {device}", "enable {device}" },
            "Turn on a device");
        Assert.Equal("turn_on", pattern.Id);
        Assert.Equal(3, pattern.Templates.Length);
    }

    [Fact]
    public void VoiceCommandResult_CanBeCreated()
    {
        var result = new VoiceCommandResult(
            "turn_on",
            "turn on living room light",
            new Dictionary<string, string> { ["device"] = "living room light" },
            0.85f);
        Assert.Equal("turn_on", result.CommandId);
        Assert.Equal("living room light", result.Parameters["device"]);
    }

    [Fact]
    public void AccessibilityPreferencesChanged_CanBeCreated()
    {
        var prefs = new AccessibilityPreferencesChanged(true, false, true, 1.5f);
        Assert.True(prefs.ScreenReaderActive);
        Assert.False(prefs.ReducedMotion);
        Assert.True(prefs.HighContrast);
        Assert.Equal(1.5f, prefs.TextScale);
    }
}
