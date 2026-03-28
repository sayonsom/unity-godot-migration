// =============================================================================
// VisualizationConfig.cs — Color palettes and rendering config for Home Map
// Pure C# — no engine dependencies
// =============================================================================

namespace SmartThings.Abstraction.Models;

/// <summary>Maps room types to color-coded visualization colors (SmartThings style).</summary>
public static class RoomColorPalette
{
    private static readonly Dictionary<RoomType, Color> _colors = new()
    {
        { RoomType.LivingRoom, Color.FromHex("90EE90") },  // Bright green (like SmartThings)
        { RoomType.Kitchen,    Color.FromHex("FFEB8A") },  // Warm yellow
        { RoomType.Bedroom,    Color.FromHex("FFB6C1") },  // Bright pink
        { RoomType.Bathroom,   Color.FromHex("87CEEB") },  // Sky blue
        { RoomType.Hallway,    Color.FromHex("C8E6C9") },  // Light green
        { RoomType.Balcony,    Color.FromHex("B39DDB") },  // Purple/deck
        { RoomType.Office,     Color.FromHex("80CBC4") },  // Teal
        { RoomType.Garage,     Color.FromHex("BCAAA4") },  // Warm gray
        { RoomType.Laundry,    Color.FromHex("90CAF9") },  // Blue
        { RoomType.Custom,     Color.FromHex("E0E0E0") },  // Default gray
    };

    /// <summary>Get the display color for a room type.</summary>
    public static Color GetColor(RoomType roomType)
        => _colors.TryGetValue(roomType, out var color) ? color : _colors[RoomType.Custom];

    /// <summary>Get a slightly darker shade for walls (80% brightness).</summary>
    public static Color GetWallColor(RoomType roomType)
    {
        var c = GetColor(roomType);
        return new Color(c.R * 0.8f, c.G * 0.8f, c.B * 0.8f, c.A);
    }
}

/// <summary>Device status colors for pin markers.</summary>
public static class DeviceStatusColors
{
    public static readonly Color Online = Color.FromHex("4CAF50");   // Green
    public static readonly Color Offline = Color.FromHex("9E9E9E");  // Gray
    public static readonly Color Active = Color.FromHex("2196F3");   // Blue
    public static readonly Color Error = Color.FromHex("F44336");    // Red
    public static readonly Color Updating = Color.FromHex("FF9800"); // Orange
}

/// <summary>Configuration for the 3D home map view.</summary>
public record HomeViewConfig
{
    // Camera
    public float DefaultCameraDistance { get; init; } = 15.0f;
    public float DefaultCameraAngle { get; init; } = 45.0f;    // Isometric angle in degrees
    public float MinZoom { get; init; } = 5.0f;
    public float MaxZoom { get; init; } = 30.0f;
    public float ZoomSpeed { get; init; } = 2.0f;
    public float PanSpeed { get; init; } = 0.02f;
    public float RotateSpeed { get; init; } = 0.005f;
    public float SmoothFactor { get; init; } = 8.0f;

    // Pins
    public float PinBaseScale { get; init; } = 0.3f;
    public float PinPulseSpeed { get; init; } = 2.0f;
    public float PinHoverScale { get; init; } = 1.3f;

    // Walls
    public float DefaultWallHeight { get; init; } = 2.8f;
    public float WallOpacity { get; init; } = 0.6f;
    public float WallFadeHeight { get; init; } = 0.7f;  // Fraction of wall height where fade starts

    // Rendering
    public float AmbientOcclusionIntensity { get; init; } = 0.3f;
    public float GridOpacity { get; init; } = 0.08f;
    public float EdgeGlowWidth { get; init; } = 0.05f;
}
