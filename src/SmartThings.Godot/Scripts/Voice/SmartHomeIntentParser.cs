// =============================================================================
// SmartHomeIntentParser.cs — NLU that maps voice text to SmartThings commands
// Handles device control, room queries, scene activation, and status checks
// =============================================================================

using SmartThings.Abstraction;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.Voice;

/// <summary>
/// Smart intent parser for SmartThings voice commands.
/// Processes STT output text and maps it to concrete DeviceCommands.
///
/// Supports:
///   - Device control: "turn on kitchen light", "set bedroom to 72 degrees"
///   - Room queries: "what's in the living room", "show bedroom devices"
///   - Status checks: "is the front door locked", "what's the temperature"
///   - Scene activation: "goodnight", "I'm leaving", "movie time"
///   - Navigation: "go to kitchen", "show balcony"
/// </summary>
public class SmartHomeIntentParser
{
    private SmartHome? _home;
    private readonly List<VoiceCommandPattern> _customPatterns = new();

    public event Action<ParsedIntent>? OnIntentParsed;

    public void SetHome(SmartHome home) => _home = home;

    /// <summary>
    /// Parse a voice utterance into a structured intent.
    /// Returns null if no intent could be determined.
    /// </summary>
    public ParsedIntent? Parse(string utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance)) return null;

        var normalized = utterance.Trim().ToLowerInvariant();

        // Try each intent type in order of specificity
        var intent =
            TryParseDeviceControl(normalized) ??
            TryParseStatusQuery(normalized) ??
            TryParseRoomNavigation(normalized) ??
            TryParseSceneActivation(normalized) ??
            TryParseRoomQuery(normalized);

        if (intent != null)
        {
            OnIntentParsed?.Invoke(intent);
        }

        return intent;
    }

    // ── Device Control ──────────────────────────────────────────────────────

    private ParsedIntent? TryParseDeviceControl(string text)
    {
        // "turn on/off {device}"
        if (TryExtractAfter(text, "turn on ", out var deviceOn))
            return CreateDeviceIntent(deviceOn, IntentType.DeviceControl, "switch", "on");
        if (TryExtractAfter(text, "turn off ", out var deviceOff))
            return CreateDeviceIntent(deviceOff, IntentType.DeviceControl, "switch", "off");

        // "switch on/off {device}"
        if (TryExtractAfter(text, "switch on ", out var switchOn))
            return CreateDeviceIntent(switchOn, IntentType.DeviceControl, "switch", "on");
        if (TryExtractAfter(text, "switch off ", out var switchOff))
            return CreateDeviceIntent(switchOff, IntentType.DeviceControl, "switch", "off");

        // "dim {device} to {value}" / "set {device} to {value}"
        if (TryExtractSetCommand(text, out var setDevice, out var setValue))
            return CreateDeviceIntent(setDevice, IntentType.DeviceControl, "setLevel", setValue);

        // "brighten/dim {device}"
        if (TryExtractAfter(text, "brighten ", out var brighten))
            return CreateDeviceIntent(brighten, IntentType.DeviceControl, "setLevel", "100");
        if (TryExtractAfter(text, "dim ", out var dim))
            return CreateDeviceIntent(dim, IntentType.DeviceControl, "setLevel", "30");

        // "lock/unlock {device}"
        if (TryExtractAfter(text, "lock ", out var lockDev))
            return CreateDeviceIntent(lockDev, IntentType.DeviceControl, "lock", "locked");
        if (TryExtractAfter(text, "unlock ", out var unlockDev))
            return CreateDeviceIntent(unlockDev, IntentType.DeviceControl, "lock", "unlocked");

        // "open/close {device}" (blinds, doors)
        if (TryExtractAfter(text, "open ", out var openDev))
            return CreateDeviceIntent(openDev, IntentType.DeviceControl, "windowShade", "open");
        if (TryExtractAfter(text, "close ", out var closeDev))
            return CreateDeviceIntent(closeDev, IntentType.DeviceControl, "windowShade", "close");

        return null;
    }

    // ── Status Queries ──────────────────────────────────────────────────────

    private ParsedIntent? TryParseStatusQuery(string text)
    {
        // "is the {device} on/off/locked/open"
        if (text.StartsWith("is the ") || text.StartsWith("is "))
        {
            var deviceText = text.StartsWith("is the ") ? text[7..] : text[3..];
            var device = FindDevice(deviceText);
            if (device != null)
            {
                return new ParsedIntent(
                    IntentType.StatusQuery,
                    $"Status of {device.Label}: {device.Status}",
                    Device: device,
                    Confidence: 0.85f);
            }
        }

        // "what's the temperature" / "how warm is it"
        if (text.Contains("temperature") || text.Contains("how warm") || text.Contains("how cold"))
        {
            var thermostat = _home?.Devices.FirstOrDefault(d => d.Category == DeviceCategory.Thermostat);
            if (thermostat != null)
            {
                return new ParsedIntent(
                    IntentType.StatusQuery,
                    $"Temperature from {thermostat.Label}",
                    Device: thermostat,
                    Confidence: 0.8f);
            }
        }

        // "what's on" / "what's running"
        if (text.Contains("what's on") || text.Contains("what is on") || text.Contains("what's running"))
        {
            return new ParsedIntent(
                IntentType.StatusQuery,
                "Active devices query",
                Confidence: 0.75f);
        }

        return null;
    }

    // ── Room Navigation ─────────────────────────────────────────────────────

    private ParsedIntent? TryParseRoomNavigation(string text)
    {
        // "go to {room}" / "show {room}" / "navigate to {room}"
        string? roomText = null;
        if (TryExtractAfter(text, "go to ", out roomText) ||
            TryExtractAfter(text, "show me ", out roomText) ||
            TryExtractAfter(text, "show ", out roomText) ||
            TryExtractAfter(text, "navigate to ", out roomText) ||
            TryExtractAfter(text, "zoom to ", out roomText))
        {
            var room = FindRoom(roomText);
            if (room != null)
            {
                return new ParsedIntent(
                    IntentType.RoomNavigation,
                    $"Navigate to {room.Name}",
                    Room: room,
                    Confidence: 0.9f);
            }
        }

        return null;
    }

    // ── Scene Activation ────────────────────────────────────────────────────

    private ParsedIntent? TryParseSceneActivation(string text)
    {
        // Map common phrases to SmartThings scenes
        var sceneMap = new Dictionary<string[], string>
        {
            { new[] { "good night", "goodnight", "bedtime", "going to sleep" }, "Goodnight" },
            { new[] { "good morning", "wake up", "i'm up" }, "Good Morning" },
            { new[] { "i'm leaving", "leaving home", "goodbye", "heading out" }, "Goodbye" },
            { new[] { "i'm home", "i'm back", "home" }, "I'm Back" },
            { new[] { "movie time", "movie mode", "watch a movie" }, "Movie Time" },
            { new[] { "party mode", "party time" }, "Party" },
        };

        foreach (var (triggers, sceneName) in sceneMap)
        {
            if (triggers.Any(t => text.Contains(t)))
            {
                return new ParsedIntent(
                    IntentType.SceneActivation,
                    $"Activate scene: {sceneName}",
                    SceneName: sceneName,
                    Confidence: 0.85f);
            }
        }

        return null;
    }

    // ── Room Queries ────────────────────────────────────────────────────────

    private ParsedIntent? TryParseRoomQuery(string text)
    {
        // "what's in the {room}" / "devices in {room}"
        if (_home == null) return null;

        foreach (var room in _home.Rooms)
        {
            var roomNameLower = room.Name.ToLowerInvariant();
            if (text.Contains(roomNameLower))
            {
                if (text.Contains("what") || text.Contains("device") || text.Contains("list"))
                {
                    return new ParsedIntent(
                        IntentType.RoomQuery,
                        $"Devices in {room.Name}",
                        Room: room,
                        Confidence: 0.8f);
                }
            }
        }

        return null;
    }

    // ── Device/Room Matching ────────────────────────────────────────────────

    private SmartDevice? FindDevice(string text)
    {
        if (_home == null) return null;

        // Try exact label match first
        var device = _home.Devices.FirstOrDefault(d =>
            text.Contains(d.Label.ToLowerInvariant()));
        if (device != null) return device;

        // Try fuzzy: room name + device category
        foreach (var room in _home.Rooms)
        {
            var roomNameLower = room.Name.ToLowerInvariant();
            if (!text.Contains(roomNameLower)) continue;

            // "bedroom light" → find Light in Bedroom
            foreach (var dev in _home.Devices.Where(d => d.RoomId == room.RoomId))
            {
                var categoryWord = dev.Category switch
                {
                    DeviceCategory.Light => "light",
                    DeviceCategory.Thermostat => "thermostat",
                    DeviceCategory.Lock => "lock",
                    DeviceCategory.Switch => "switch",
                    DeviceCategory.Sensor => "sensor",
                    DeviceCategory.Camera => "camera",
                    DeviceCategory.Speaker => "speaker",
                    DeviceCategory.Television => "tv",
                    DeviceCategory.Appliance => "appliance",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(categoryWord) && text.Contains(categoryWord))
                    return dev;
            }

            // If only room name mentioned, return first device in room
            if (room.Devices.Count > 0)
                return room.Devices[0];
        }

        // Try category match alone: "the light", "the tv"
        var categoryDevices = new (string keyword, DeviceCategory cat)[]
        {
            ("light", DeviceCategory.Light),
            ("lamp", DeviceCategory.Light),
            ("tv", DeviceCategory.Television),
            ("television", DeviceCategory.Television),
            ("thermostat", DeviceCategory.Thermostat),
            ("ac", DeviceCategory.Thermostat),
            ("air conditioner", DeviceCategory.Thermostat),
            ("camera", DeviceCategory.Camera),
            ("speaker", DeviceCategory.Speaker),
            ("fridge", DeviceCategory.Appliance),
            ("refrigerator", DeviceCategory.Appliance),
            ("washer", DeviceCategory.Appliance),
            ("blinds", DeviceCategory.Switch),
        };

        foreach (var (keyword, cat) in categoryDevices)
        {
            if (text.Contains(keyword))
                return _home.Devices.FirstOrDefault(d => d.Category == cat);
        }

        return null;
    }

    private SmartRoom? FindRoom(string text)
    {
        if (_home == null) return null;

        // Direct room name match
        foreach (var room in _home.Rooms)
        {
            if (text.Contains(room.Name.ToLowerInvariant()))
                return room;
        }

        // Room type keywords
        var roomKeywords = new Dictionary<string, RoomType>
        {
            { "bedroom", RoomType.Bedroom },
            { "living", RoomType.LivingRoom },
            { "kitchen", RoomType.Kitchen },
            { "bathroom", RoomType.Bathroom },
            { "balcony", RoomType.Balcony },
            { "hallway", RoomType.Hallway },
            { "office", RoomType.Office },
        };

        foreach (var (keyword, roomType) in roomKeywords)
        {
            if (text.Contains(keyword))
                return _home.Rooms.FirstOrDefault(r => r.RoomType == roomType);
        }

        return null;
    }

    private ParsedIntent? CreateDeviceIntent(string deviceText, IntentType type, string capability, string value)
    {
        var device = FindDevice(deviceText);
        if (device == null)
        {
            // Still return intent even without matched device for feedback
            return new ParsedIntent(
                type,
                $"Could not find device matching '{deviceText}'",
                Command: new DeviceCommand(device?.DeviceId ?? "", capability, value, new object[] { value }),
                Confidence: 0.3f);
        }

        return new ParsedIntent(
            type,
            $"{capability} {value} on {device.Label}",
            Device: device,
            Command: new DeviceCommand(device?.DeviceId ?? "", capability, value, new object[] { value }),
            Confidence: 0.9f);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool TryExtractAfter(string text, string prefix, out string remainder)
    {
        if (text.StartsWith(prefix))
        {
            remainder = text[prefix.Length..].Trim();
            return !string.IsNullOrEmpty(remainder);
        }

        var idx = text.IndexOf(prefix, StringComparison.Ordinal);
        if (idx >= 0)
        {
            remainder = text[(idx + prefix.Length)..].Trim();
            return !string.IsNullOrEmpty(remainder);
        }

        remainder = "";
        return false;
    }

    private static bool TryExtractSetCommand(string text, out string device, out string value)
    {
        device = ""; value = "";

        // "set {device} to {value}"
        string[] prefixes = { "set ", "adjust " };
        foreach (var prefix in prefixes)
        {
            if (!text.StartsWith(prefix)) continue;
            var rest = text[prefix.Length..];
            var toIdx = rest.IndexOf(" to ", StringComparison.Ordinal);
            if (toIdx > 0)
            {
                device = rest[..toIdx].Trim();
                value = rest[(toIdx + 4)..].Trim();

                // Extract numeric value: "72 degrees" → "72"
                var numMatch = System.Text.RegularExpressions.Regex.Match(value, @"\d+");
                if (numMatch.Success) value = numMatch.Value;

                return true;
            }
        }

        return false;
    }
}

// ── Intent Types ────────────────────────────────────────────────────────────

public enum IntentType
{
    DeviceControl,
    StatusQuery,
    RoomNavigation,
    RoomQuery,
    SceneActivation
}

public record ParsedIntent(
    IntentType Type,
    string Description,
    SmartDevice? Device = null,
    SmartRoom? Room = null,
    DeviceCommand? Command = null,
    string? SceneName = null,
    float Confidence = 0f
);
