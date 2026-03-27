// =============================================================================
// HomeModels.cs — SmartThings Home & Room models for 3D Home Map View
// Pure C# — portable across any engine backend
// =============================================================================

namespace SmartThings.Abstraction.Models;

/// <summary>Represents a complete SmartThings home with rooms and devices.</summary>
public record SmartHome
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public List<SmartRoom> Rooms { get; init; } = new();
    public List<SmartDevice> Devices { get; init; } = new();
    public List<DevicePlacement> DevicePlacements { get; init; } = new();
}

/// <summary>Type of room for color palette and icon selection.</summary>
public enum RoomType
{
    LivingRoom,
    Bedroom,
    Kitchen,
    Bathroom,
    Hallway,
    Balcony,
    Office,
    Garage,
    Laundry,
    Custom
}

/// <summary>A wall segment with optional door cutout.</summary>
public record WallSegment(
    Vector2 Start,
    Vector2 End,
    float Height = 2.8f,
    bool HasDoor = false,
    float DoorWidth = 0.9f,
    float DoorHeight = 2.1f
);

/// <summary>Placement of a device pin in 3D space within a room.</summary>
public record DevicePlacement(
    string DeviceId,
    string RoomId,
    Vector3 Position,
    float IconScale = 1.0f
);
