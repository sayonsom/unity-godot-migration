// =============================================================================
// Phase4Tests.cs — Tests for Phase 4: Accessibility, Voice, IoT
// Tests intent parsing, voice command matching, accessible element registration
// =============================================================================

using SmartThings.Abstraction;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;
using Xunit;

namespace SmartThings.Tests;

// =============================================================================
// Intent Parser Tests
// =============================================================================

public class IntentParserTests
{
    private static SmartHome CreateTestHome()
    {
        var devices = new List<SmartDevice>
        {
            new() { DeviceId = "d1", Name = "d1", Label = "Kitchen Light", Category = DeviceCategory.Light, RoomId = "r1", Status = DeviceStatus.Online },
            new() { DeviceId = "d2", Name = "d2", Label = "Smart TV", Category = DeviceCategory.Television, RoomId = "r2", Status = DeviceStatus.Online },
            new() { DeviceId = "d3", Name = "d3", Label = "Front Door Lock", Category = DeviceCategory.Lock, RoomId = "r3", Status = DeviceStatus.Online },
            new() { DeviceId = "d4", Name = "d4", Label = "Bedroom Light", Category = DeviceCategory.Light, RoomId = "r4", Status = DeviceStatus.Offline },
            new() { DeviceId = "d5", Name = "d5", Label = "Thermostat", Category = DeviceCategory.Thermostat, RoomId = "r2", Status = DeviceStatus.Online },
            new() { DeviceId = "d6", Name = "d6", Label = "Smart Blinds", Category = DeviceCategory.Switch, RoomId = "r4", Status = DeviceStatus.Online },
        };

        var rooms = new List<SmartRoom>
        {
            new("r1", "Kitchen", devices.Where(d => d.RoomId == "r1").ToList(), RoomType: RoomType.Kitchen),
            new("r2", "Living room", devices.Where(d => d.RoomId == "r2").ToList(), RoomType: RoomType.LivingRoom),
            new("r3", "Hallway", devices.Where(d => d.RoomId == "r3").ToList(), RoomType: RoomType.Hallway),
            new("r4", "Bedroom", devices.Where(d => d.RoomId == "r4").ToList(), RoomType: RoomType.Bedroom),
        };

        return new SmartHome
        {
            Id = "test_home",
            Name = "Test Home",
            Rooms = rooms,
            Devices = devices
        };
    }

    [Fact]
    public void Parse_TurnOnDevice_ReturnsDeviceControl()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        var result = parser.Parse("turn on kitchen light");

        Assert.NotNull(result);
        Assert.Equal("DeviceControl", result.Type);
        Assert.Equal("d1", result.DeviceId);
        Assert.Equal("on", result.CommandValue);
        Assert.True(result.Confidence > 0.5f);
    }

    [Fact]
    public void Parse_TurnOffDevice_ReturnsDeviceControl()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        var result = parser.Parse("turn off bedroom light");

        Assert.NotNull(result);
        Assert.Equal("DeviceControl", result.Type);
        Assert.Equal("d4", result.DeviceId);
        Assert.Equal("off", result.CommandValue);
    }

    [Fact]
    public void Parse_SetTemperature_ReturnsDeviceControl()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        var result = parser.Parse("set thermostat to 72 degrees");

        Assert.NotNull(result);
        Assert.Equal("DeviceControl", result.Type);
        Assert.Contains("72", result.CommandValue);
    }

    [Fact]
    public void Parse_RoomNavigation_ReturnsRoomNav()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        var result = parser.Parse("go to kitchen");

        Assert.NotNull(result);
        Assert.Equal("RoomNavigation", result.Type);
        Assert.Equal("r1", result.RoomId);
    }

    [Fact]
    public void Parse_ShowRoom_ReturnsRoomNav()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        var result = parser.Parse("show me the bedroom");

        Assert.NotNull(result);
        Assert.Equal("RoomNavigation", result.Type);
        Assert.Equal("r4", result.RoomId);
    }

    [Fact]
    public void Parse_StatusQuery_ReturnsStatusQuery()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        var result = parser.Parse("what's the temperature");

        Assert.NotNull(result);
        Assert.Equal("StatusQuery", result.Type);
        Assert.Equal("d5", result.DeviceId); // Thermostat
    }

    [Fact]
    public void Parse_GoodNight_ReturnsSceneActivation()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        var result = parser.Parse("good night");

        Assert.NotNull(result);
        Assert.Equal("SceneActivation", result.Type);
        Assert.Equal("Goodnight", result.SceneName);
    }

    [Fact]
    public void Parse_MovieTime_ReturnsSceneActivation()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        var result = parser.Parse("movie time");

        Assert.NotNull(result);
        Assert.Equal("SceneActivation", result.Type);
        Assert.Equal("Movie Time", result.SceneName);
    }

    [Fact]
    public void Parse_LockDoor_ReturnsDeviceControl()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        var result = parser.Parse("lock front door lock");

        Assert.NotNull(result);
        Assert.Equal("DeviceControl", result.Type);
        Assert.Equal("locked", result.CommandValue);
    }

    [Fact]
    public void Parse_OpenBlinds_ReturnsDeviceControl()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        var result = parser.Parse("open smart blinds");

        Assert.NotNull(result);
        Assert.Equal("DeviceControl", result.Type);
        Assert.Equal("open", result.CommandValue);
    }

    [Fact]
    public void Parse_ImLeaving_ReturnsSceneActivation()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        var result = parser.Parse("i'm leaving");

        Assert.NotNull(result);
        Assert.Equal("SceneActivation", result.Type);
        Assert.Equal("Goodbye", result.SceneName);
    }

    [Fact]
    public void Parse_UnknownCommand_ReturnsNull()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        var result = parser.Parse("what is the meaning of life");
        Assert.Null(result);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        Assert.Null(parser.Parse(""));
        Assert.Null(parser.Parse("   "));
        Assert.Null(parser.Parse(null!));
    }

    [Fact]
    public void Parse_CategoryKeyword_FindsDevice()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        // "turn on the tv" should find Smart TV by category
        var result = parser.Parse("turn on the tv");
        Assert.NotNull(result);
        Assert.Equal("d2", result.DeviceId);
    }

    [Fact]
    public void Parse_WhatsOn_ReturnsStatusQuery()
    {
        var parser = new TestableIntentParser();
        parser.SetHome(CreateTestHome());

        var result = parser.Parse("what's on");
        Assert.NotNull(result);
        Assert.Equal("StatusQuery", result.Type);
    }
}

// =============================================================================
// Voice Command Processor Tests
// =============================================================================

public class VoiceCommandProcessorTests
{
    [Fact]
    public void ProcessUtterance_MatchesTemplate()
    {
        var processor = new TestableVoiceCommandProcessor();
        processor.RegisterCommand(new VoiceCommandPattern(
            "turn_on", new[] { "turn on {device}" }, "Turn on device"));

        var result = processor.ProcessUtterance("turn on living room light");

        Assert.NotNull(result);
        Assert.Equal("turn_on", result.CommandId);
        Assert.Equal("living room light", result.Parameters["device"]);
    }

    [Fact]
    public void ProcessUtterance_MatchesMultipleTemplates()
    {
        var processor = new TestableVoiceCommandProcessor();
        processor.RegisterCommand(new VoiceCommandPattern(
            "turn_on", new[] { "turn on {device}", "switch on {device}", "enable {device}" },
            "Turn on device"));

        var result1 = processor.ProcessUtterance("switch on bedroom light");
        Assert.NotNull(result1);
        Assert.Equal("bedroom light", result1.Parameters["device"]);

        var result2 = processor.ProcessUtterance("enable thermostat");
        Assert.NotNull(result2);
        Assert.Equal("thermostat", result2.Parameters["device"]);
    }

    [Fact]
    public void ProcessUtterance_NoMatch_ReturnsNull()
    {
        var processor = new TestableVoiceCommandProcessor();
        processor.RegisterCommand(new VoiceCommandPattern(
            "turn_on", new[] { "turn on {device}" }, "Turn on device"));

        var result = processor.ProcessUtterance("what time is it");
        Assert.Null(result);
    }

    [Fact]
    public void ProcessUtterance_MultipleParams()
    {
        var processor = new TestableVoiceCommandProcessor();
        processor.RegisterCommand(new VoiceCommandPattern(
            "set_level", new[] { "set {device} to {value}" }, "Set level"));

        var result = processor.ProcessUtterance("set living room light to 50 percent");

        Assert.NotNull(result);
        Assert.Equal("living room light", result.Parameters["device"]);
        Assert.Equal("50 percent", result.Parameters["value"]);
    }

    [Fact]
    public void ProcessUtterance_FiresEvent()
    {
        var processor = new TestableVoiceCommandProcessor();
        processor.RegisterCommand(new VoiceCommandPattern(
            "turn_on", new[] { "turn on {device}" }, "Turn on"));

        VoiceCommandResult? firedResult = null;
        processor.OnCommandRecognized += r => firedResult = r;

        processor.ProcessUtterance("turn on kitchen light");

        Assert.NotNull(firedResult);
        Assert.Equal("turn_on", firedResult.CommandId);
    }

    [Fact]
    public void ProcessUtterance_CaseInsensitive()
    {
        var processor = new TestableVoiceCommandProcessor();
        processor.RegisterCommand(new VoiceCommandPattern(
            "turn_on", new[] { "turn on {device}" }, "Turn on"));

        var result = processor.ProcessUtterance("TURN ON KITCHEN LIGHT");
        Assert.NotNull(result);
        Assert.Equal("kitchen light", result.Parameters["device"]);
    }
}

// =============================================================================
// Accessibility Models Tests
// =============================================================================

public class AccessibilityModelsTests
{
    [Fact]
    public void AccessibleInfo_CreatedCorrectly()
    {
        var info = new AccessibleInfo(
            "Smart TV", "65 inch television, currently on",
            AccessibleRole.Device3D, "on");

        Assert.Equal("Smart TV", info.Name);
        Assert.Equal(AccessibleRole.Device3D, info.Role);
        Assert.Equal("on", info.Value);
    }

    [Theory]
    [InlineData(AccessibleRole.Button)]
    [InlineData(AccessibleRole.Slider)]
    [InlineData(AccessibleRole.Toggle)]
    [InlineData(AccessibleRole.Device3D)]
    [InlineData(AccessibleRole.StatusIndicator)]
    public void AccessibleRole_AllValuesValid(AccessibleRole role)
    {
        var info = new AccessibleInfo("Test", "Test desc", role);
        Assert.Equal(role, info.Role);
    }

    [Theory]
    [InlineData(AnnouncePriority.Low)]
    [InlineData(AnnouncePriority.Normal)]
    [InlineData(AnnouncePriority.High)]
    [InlineData(AnnouncePriority.Alert)]
    public void AnnouncePriority_AllValuesValid(AnnouncePriority priority)
    {
        Assert.True(Enum.IsDefined(priority));
    }

    [Fact]
    public void VoiceCommandPattern_CreatedCorrectly()
    {
        var pattern = new VoiceCommandPattern(
            "turn_on",
            new[] { "turn on {device}", "switch on {device}" },
            "Turn on a device");

        Assert.Equal("turn_on", pattern.Id);
        Assert.Equal(2, pattern.Templates.Length);
        Assert.Contains("{device}", pattern.Templates[0]);
    }

    [Fact]
    public void VoiceCommandResult_WithParameters()
    {
        var result = new VoiceCommandResult(
            "set_level",
            "set living room light to 80",
            new Dictionary<string, string>
            {
                { "device", "living room light" },
                { "value", "80" }
            },
            0.92f);

        Assert.Equal("set_level", result.CommandId);
        Assert.Equal(2, result.Parameters.Count);
        Assert.Equal("80", result.Parameters["value"]);
        Assert.Equal(0.92f, result.Confidence);
    }

    [Fact]
    public void AccessibilityPreferencesChanged_AllFields()
    {
        var prefs = new AccessibilityPreferencesChanged(
            ScreenReaderActive: true,
            ReducedMotion: false,
            HighContrast: true,
            TextScale: 1.5f);

        Assert.True(prefs.ScreenReaderActive);
        Assert.False(prefs.ReducedMotion);
        Assert.True(prefs.HighContrast);
        Assert.Equal(1.5f, prefs.TextScale);
    }
}

// =============================================================================
// VAD Processing Tests
// =============================================================================

public class VADProcessingTests
{
    [Fact]
    public void VoiceActivityEvent_SpeechStarted()
    {
        var evt = new VoiceActivityEvent(
            VoiceActivityState.SpeechStarted, 0.85f, TimeSpan.FromMilliseconds(500));

        Assert.Equal(VoiceActivityState.SpeechStarted, evt.State);
        Assert.Equal(0.85f, evt.Confidence);
        Assert.Equal(500, evt.Timestamp.TotalMilliseconds);
    }

    [Fact]
    public void VoiceActivityEvent_SpeechEnded()
    {
        var evt = new VoiceActivityEvent(
            VoiceActivityState.SpeechEnded, 0.9f, TimeSpan.FromSeconds(3));

        Assert.Equal(VoiceActivityState.SpeechEnded, evt.State);
        Assert.Equal(3000, evt.Timestamp.TotalMilliseconds);
    }

    [Fact]
    public void MicrophoneConfig_Defaults()
    {
        var config = new MicrophoneConfig();

        Assert.Equal(16000, config.SampleRate);
        Assert.Equal(1, config.ChannelCount);
        Assert.True(config.EnableVAD);
        Assert.Equal(0.5f, config.VADThreshold);
        Assert.Equal(1500f, config.SilenceTimeoutMs);
    }

    [Fact]
    public void AudioBuffer_CreatedCorrectly()
    {
        var samples = new float[] { 0.1f, 0.2f, -0.1f, 0.3f };
        var buffer = new AudioBuffer(samples, 16000, 1, TimeSpan.FromMilliseconds(250));

        Assert.Equal(4, buffer.Samples.Length);
        Assert.Equal(16000, buffer.SampleRate);
        Assert.Equal(1, buffer.ChannelCount);
        Assert.Equal(250, buffer.Duration.TotalMilliseconds);
    }

    [Fact]
    public void AudioFrame_MetricsCorrect()
    {
        var samples = new float[] { 0.5f, -0.3f, 0.8f, -0.1f };
        var frame = new AudioFrame(samples, 16000, 0.8f, 0.5f);

        Assert.Equal(0.8f, frame.PeakAmplitude);
        Assert.Equal(0.5f, frame.RmsLevel);
    }
}

// =============================================================================
// IoT Event Models Tests
// =============================================================================

public class IoTEventTests
{
    [Fact]
    public void MqttConfig_CustomValues()
    {
        var config = new MqttConfig(
            BrokerHost: "mqtt.example.com",
            BrokerPort: 8883,
            ClientId: "godot-client",
            Username: "user",
            Password: "pass",
            UseTls: true);

        Assert.Equal("mqtt.example.com", config.BrokerHost);
        Assert.Equal(8883, config.BrokerPort);
        Assert.True(config.UseTls);
    }

    [Fact]
    public void DeviceCommand_WithArguments()
    {
        var cmd = new DeviceCommand("dev1", "switch", "on", new object[] { "on" });

        Assert.Equal("dev1", cmd.DeviceId);
        Assert.Equal("switch", cmd.CapabilityId);
        Assert.Equal("on", cmd.CommandName);
        Assert.Single(cmd.Arguments!);
    }

    [Fact]
    public void DeviceCommand_NoArguments()
    {
        var cmd = new DeviceCommand("dev1", "refresh", "refresh");

        Assert.Equal("refresh", cmd.CapabilityId);
        Assert.Null(cmd.Arguments);
    }
}

// =============================================================================
// Testable wrappers (pure C# implementations without Godot dependency)
// =============================================================================

/// <summary>
/// Pure C# intent parser matching the SmartHomeIntentParser logic.
/// Used in tests to avoid Godot engine dependency.
/// </summary>
internal class TestableIntentParser
{
    private SmartHome? _home;

    public void SetHome(SmartHome home) => _home = home;

    public TestIntentResult? Parse(string? utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance)) return null;
        var text = utterance.Trim().ToLowerInvariant();

        return TryParseDeviceControl(text) ??
               TryParseStatusQuery(text) ??
               TryParseRoomNavigation(text) ??
               TryParseSceneActivation(text);
    }

    private TestIntentResult? TryParseDeviceControl(string text)
    {
        if (TryExtractAfter(text, "turn on ", out var devOn))
            return CreateDeviceResult("DeviceControl", devOn, "on");
        if (TryExtractAfter(text, "turn off ", out var devOff))
            return CreateDeviceResult("DeviceControl", devOff, "off");
        if (TryExtractAfter(text, "lock ", out var lockDev))
            return CreateDeviceResult("DeviceControl", lockDev, "locked");
        if (TryExtractAfter(text, "unlock ", out var unlockDev))
            return CreateDeviceResult("DeviceControl", unlockDev, "unlocked");
        if (TryExtractAfter(text, "open ", out var openDev))
            return CreateDeviceResult("DeviceControl", openDev, "open");
        if (TryExtractAfter(text, "close ", out var closeDev))
            return CreateDeviceResult("DeviceControl", closeDev, "close");

        // "set X to Y"
        if (text.StartsWith("set "))
        {
            var rest = text[4..];
            var toIdx = rest.IndexOf(" to ", StringComparison.Ordinal);
            if (toIdx > 0)
            {
                var device = rest[..toIdx].Trim();
                var value = rest[(toIdx + 4)..].Trim();
                var numMatch = System.Text.RegularExpressions.Regex.Match(value, @"\d+");
                if (numMatch.Success) value = numMatch.Value;
                return CreateDeviceResult("DeviceControl", device, value);
            }
        }

        return null;
    }

    private TestIntentResult? TryParseStatusQuery(string text)
    {
        if (text.Contains("temperature") || text.Contains("how warm") || text.Contains("how cold"))
        {
            var thermo = _home?.Devices.FirstOrDefault(d => d.Category == DeviceCategory.Thermostat);
            if (thermo != null)
                return new TestIntentResult("StatusQuery", thermo.DeviceId, null, null, null, 0.8f);
        }

        if (text.Contains("what's on") || text.Contains("what is on"))
            return new TestIntentResult("StatusQuery", null, null, null, null, 0.75f);

        return null;
    }

    private TestIntentResult? TryParseRoomNavigation(string text)
    {
        string? roomText = null;
        if (TryExtractAfter(text, "go to ", out roomText) ||
            TryExtractAfter(text, "show me the ", out roomText) ||
            TryExtractAfter(text, "show me ", out roomText) ||
            TryExtractAfter(text, "show ", out roomText) ||
            TryExtractAfter(text, "navigate to ", out roomText))
        {
            var room = FindRoom(roomText);
            if (room != null)
                return new TestIntentResult("RoomNavigation", null, room.RoomId, null, null, 0.9f);
        }
        return null;
    }

    private TestIntentResult? TryParseSceneActivation(string text)
    {
        var sceneMap = new Dictionary<string[], string>
        {
            { new[] { "good night", "goodnight", "bedtime" }, "Goodnight" },
            { new[] { "good morning", "wake up" }, "Good Morning" },
            { new[] { "i'm leaving", "leaving home", "goodbye" }, "Goodbye" },
            { new[] { "i'm home", "i'm back" }, "I'm Back" },
            { new[] { "movie time", "movie mode" }, "Movie Time" },
        };

        foreach (var (triggers, sceneName) in sceneMap)
        {
            if (triggers.Any(t => text.Contains(t)))
                return new TestIntentResult("SceneActivation", null, null, sceneName, null, 0.85f);
        }
        return null;
    }

    private TestIntentResult? CreateDeviceResult(string type, string deviceText, string value)
    {
        var device = FindDevice(deviceText);
        return new TestIntentResult(type, device?.DeviceId, null, null, value, device != null ? 0.9f : 0.3f);
    }

    private SmartDevice? FindDevice(string text)
    {
        if (_home == null) return null;

        var device = _home.Devices.FirstOrDefault(d => text.Contains(d.Label.ToLowerInvariant()));
        if (device != null) return device;

        var categoryDevices = new (string keyword, DeviceCategory cat)[]
        {
            ("light", DeviceCategory.Light), ("lamp", DeviceCategory.Light),
            ("tv", DeviceCategory.Television), ("television", DeviceCategory.Television),
            ("thermostat", DeviceCategory.Thermostat),
            ("camera", DeviceCategory.Camera), ("speaker", DeviceCategory.Speaker),
            ("blinds", DeviceCategory.Switch), ("lock", DeviceCategory.Lock),
        };

        foreach (var (keyword, cat) in categoryDevices)
        {
            if (text.Contains(keyword))
                return _home.Devices.FirstOrDefault(d => d.Category == cat);
        }

        // Try room + category
        foreach (var room in _home.Rooms)
        {
            if (!text.Contains(room.Name.ToLowerInvariant())) continue;
            return _home.Devices.FirstOrDefault(d => d.RoomId == room.RoomId);
        }

        return null;
    }

    private SmartRoom? FindRoom(string text)
    {
        if (_home == null) return null;

        foreach (var room in _home.Rooms)
            if (text.Contains(room.Name.ToLowerInvariant())) return room;

        var roomKeywords = new Dictionary<string, RoomType>
        {
            { "bedroom", RoomType.Bedroom }, { "living", RoomType.LivingRoom },
            { "kitchen", RoomType.Kitchen }, { "bathroom", RoomType.Bathroom },
            { "balcony", RoomType.Balcony },
        };

        foreach (var (kw, rt) in roomKeywords)
            if (text.Contains(kw))
                return _home.Rooms.FirstOrDefault(r => r.RoomType == rt);

        return null;
    }

    private static bool TryExtractAfter(string text, string prefix, out string remainder)
    {
        if (text.StartsWith(prefix)) { remainder = text[prefix.Length..].Trim(); return remainder.Length > 0; }
        var idx = text.IndexOf(prefix, StringComparison.Ordinal);
        if (idx >= 0) { remainder = text[(idx + prefix.Length)..].Trim(); return remainder.Length > 0; }
        remainder = ""; return false;
    }
}

internal record TestIntentResult(
    string Type,
    string? DeviceId,
    string? RoomId,
    string? SceneName,
    string? CommandValue,
    float Confidence);

/// <summary>
/// Pure C# voice command processor matching GodotVoiceCommandProcessor logic.
/// </summary>
internal class TestableVoiceCommandProcessor : IVoiceCommandProcessor
{
    private readonly List<VoiceCommandPattern> _patterns = new();
    public event Action<VoiceCommandResult>? OnCommandRecognized;

    public void RegisterCommand(VoiceCommandPattern pattern) => _patterns.Add(pattern);

    public VoiceCommandResult? ProcessUtterance(string utterance)
    {
        var normalized = utterance.Trim().ToLowerInvariant();

        foreach (var pattern in _patterns)
        {
            foreach (var template in pattern.Templates)
            {
                var result = TryMatchTemplate(normalized, template.ToLowerInvariant(), pattern.Id);
                if (result != null) { OnCommandRecognized?.Invoke(result); return result; }
            }
        }
        return null;
    }

    private static VoiceCommandResult? TryMatchTemplate(string utterance, string template, string commandId)
    {
        var parameters = new Dictionary<string, string>();
        var parts = template.Split('{', '}');
        var remaining = utterance;

        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 0)
            {
                var literal = parts[i].Trim();
                if (string.IsNullOrEmpty(literal)) continue;
                var idx = remaining.IndexOf(literal, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return null;
                remaining = remaining[(idx + literal.Length)..].Trim();
            }
            else
            {
                var paramName = parts[i];
                var nextLiteral = i + 1 < parts.Length ? parts[i + 1].Trim() : "";
                string paramValue;

                if (string.IsNullOrEmpty(nextLiteral))
                {
                    paramValue = remaining.Trim();
                    remaining = "";
                }
                else
                {
                    var nextIdx = remaining.IndexOf(nextLiteral, StringComparison.OrdinalIgnoreCase);
                    if (nextIdx < 0) return null;
                    paramValue = remaining[..nextIdx].Trim();
                    remaining = remaining[nextIdx..];
                }

                if (!string.IsNullOrEmpty(paramValue)) parameters[paramName] = paramValue;
            }
        }

        return new VoiceCommandResult(commandId, utterance, parameters, 0.85f);
    }
}
