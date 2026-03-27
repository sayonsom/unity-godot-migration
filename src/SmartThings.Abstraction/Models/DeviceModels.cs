// =============================================================================
// DeviceModels.cs — SmartThings IoT device models
// Pure C# — these are portable across any engine backend
// =============================================================================

namespace SmartThings.Abstraction.Models;

/// <summary>Represents a SmartThings IoT device with its current state.</summary>
public record SmartDevice
{
    public required string DeviceId { get; init; }
    public required string Name { get; init; }
    public required string Label { get; init; }
    public required DeviceCategory Category { get; init; }
    public required string RoomId { get; init; }
    public string? RoomName { get; init; }
    public string? ManufacturerName { get; init; }
    public string? ModelName { get; init; }

    /// <summary>Current device capabilities and their states.</summary>
    public Dictionary<string, DeviceCapabilityState> Capabilities { get; init; } = new();

    /// <summary>3D model resource path for visualization (glTF).</summary>
    public string? Model3DPath { get; init; }

    /// <summary>Device online/offline status.</summary>
    public DeviceStatus Status { get; init; } = DeviceStatus.Online;
}

public enum DeviceCategory
{
    Light,
    Thermostat,
    Lock,
    Switch,
    Sensor,
    Camera,
    Speaker,
    Television,
    Appliance,
    Hub,
    Other
}

public enum DeviceStatus { Online, Offline, Updating, Error }

public record DeviceCapabilityState(
    string CapabilityId,        // e.g., "switch", "thermostatMode", "colorControl"
    string AttributeName,       // e.g., "switch", "thermostatMode", "hue"
    object? Value,              // e.g., "on", 72, 180
    string? Unit = null         // e.g., "F", "%"
);

/// <summary>Command to send to a SmartThings device.</summary>
public record DeviceCommand(
    string DeviceId,
    string CapabilityId,
    string CommandName,         // e.g., "on", "off", "setThermostatMode"
    object[]? Arguments = null  // e.g., ["heat"], [72]
);

/// <summary>Room containing devices, for spatial layout in 3D scene.</summary>
public record SmartRoom(
    string RoomId,
    string Name,
    IReadOnlyList<SmartDevice> Devices,
    RoomLayout? Layout = null,
    RoomType RoomType = RoomType.Custom,
    IReadOnlyList<Vector2>? FloorPolygon = null,
    float FloorY = 0f,
    float WallHeight = 2.8f,
    IReadOnlyList<WallSegment>? WallSegments = null,
    IReadOnlyList<string>? DeviceIds = null
);

/// <summary>Spatial layout metadata for 3D room visualization.</summary>
public record RoomLayout(
    Vector3 Center,
    Vector3 Size,               // Room dimensions in meters
    Dictionary<string, Vector3> DevicePositions  // DeviceId -> position
);

/// <summary>Event fired when a device state changes (from MQTT or polling).</summary>
public record DeviceStateChangedEvent(
    string DeviceId,
    string CapabilityId,
    string AttributeName,
    object? OldValue,
    object? NewValue,
    DateTimeOffset Timestamp
);
