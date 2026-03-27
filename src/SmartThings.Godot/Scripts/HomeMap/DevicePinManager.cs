// =============================================================================
// DevicePinManager.cs — Spawns and manages billboard device pin markers
// Each pin uses device_pin.gdshader with status color and pulse animation
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// Manages device pin markers in the 3D Home Map View.
/// Spawns billboard quads at device positions with status-colored rings.
/// </summary>
public partial class DevicePinManager : GodotNative.Node3D
{
    private GodotNative.Shader? _pinShader;
    private readonly Dictionary<string, GodotNative.MeshInstance3D> _pins = new();
    private readonly Dictionary<string, GodotNative.ShaderMaterial> _pinMaterials = new();

    /// <summary>Fired when a device pin is tapped.</summary>
    [GodotNative.Signal] public delegate void DevicePinTappedEventHandler(string deviceId);

    public override void _Ready()
    {
        _pinShader = GodotNative.GD.Load<GodotNative.Shader>("res://Shaders/device_pin.gdshader");
        GodotNative.GD.Print("[DevicePinManager] Ready.");
    }

    /// <summary>Spawn pins for all device placements in a home.</summary>
    public void SpawnPins(SmartHome home)
    {
        ClearPins();

        foreach (var placement in home.DevicePlacements)
        {
            var device = home.Devices.Find(d => d.DeviceId == placement.DeviceId);
            if (device == null) continue;

            SpawnPin(placement, device);
        }

        GodotNative.GD.Print($"[DevicePinManager] Spawned {_pins.Count} device pins.");
    }

    /// <summary>Update a single pin's status color and activity.</summary>
    public void UpdatePinStatus(string deviceId, DeviceStatus status, bool isActive)
    {
        if (!_pinMaterials.TryGetValue(deviceId, out var mat)) return;

        var color = GetStatusColor(status);
        mat.SetShaderParameter("status_color", new GodotNative.Color(color.R, color.G, color.B, color.A));
        mat.SetShaderParameter("is_active", isActive);
    }

    /// <summary>Show or hide pins for a specific room.</summary>
    public void SetRoomPinsVisible(string roomId, bool visible, SmartHome home)
    {
        foreach (var placement in home.DevicePlacements)
        {
            if (placement.RoomId != roomId) continue;
            if (_pins.TryGetValue(placement.DeviceId, out var pin))
                pin.Visible = visible;
        }
    }

    /// <summary>Remove all pins.</summary>
    public void ClearPins()
    {
        foreach (var pin in _pins.Values)
            pin.QueueFree();
        _pins.Clear();
        _pinMaterials.Clear();
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private void SpawnPin(DevicePlacement placement, SmartDevice device)
    {
        var mesh = new GodotNative.QuadMesh();
        mesh.Size = new GodotNative.Vector2(0.8f, 0.8f) * placement.IconScale;

        var mat = CreatePinMaterial(device);
        var instance = new GodotNative.MeshInstance3D();
        instance.Mesh = mesh;
        instance.MaterialOverride = mat;
        instance.CastShadow = GodotNative.GeometryInstance3D.ShadowCastingSetting.Off;
        instance.Name = $"Pin_{device.DeviceId}";

        // Position well above the floor so it's visible from isometric view
        instance.Position = new GodotNative.Vector3(
            placement.Position.X,
            placement.Position.Y + 1.5f,  // Float well above walls
            placement.Position.Z);

        // Add Area3D for tap detection
        var area = new GodotNative.Area3D();
        area.SetMeta("device_id", device.DeviceId);
        var colShape = new GodotNative.CollisionShape3D();
        var sphere = new GodotNative.SphereShape3D();
        sphere.Radius = 0.4f * placement.IconScale;
        colShape.Shape = sphere;
        area.AddChild(colShape);
        area.InputEvent += (camera, inputEvent, position, normal, shapeIdx) =>
        {
            if (inputEvent is GodotNative.InputEventMouseButton mb && mb.Pressed
                && mb.ButtonIndex == GodotNative.MouseButton.Left)
            {
                EmitSignal(SignalName.DevicePinTapped, device.DeviceId);
            }
            if (inputEvent is GodotNative.InputEventScreenTouch touch && touch.Pressed)
            {
                EmitSignal(SignalName.DevicePinTapped, device.DeviceId);
            }
        };
        instance.AddChild(area);

        AddChild(instance);
        _pins[device.DeviceId] = instance;
        _pinMaterials[device.DeviceId] = mat;
    }

    private GodotNative.ShaderMaterial CreatePinMaterial(SmartDevice device)
    {
        var mat = new GodotNative.ShaderMaterial();
        if (_pinShader != null) mat.Shader = _pinShader;

        var statusColor = GetStatusColor(device.Status);
        var iconColor = GetCategoryColor(device.Category);

        mat.SetShaderParameter("status_color",
            new GodotNative.Color(statusColor.R, statusColor.G, statusColor.B, statusColor.A));
        mat.SetShaderParameter("icon_color",
            new GodotNative.Color(iconColor.R, iconColor.G, iconColor.B, iconColor.A));
        mat.SetShaderParameter("is_active", device.Status == DeviceStatus.Online);
        mat.SetShaderParameter("pulse_speed", 2.0f);
        mat.SetShaderParameter("ring_width", 0.08f);

        return mat;
    }

    private static Abstraction.Color GetStatusColor(DeviceStatus status) => status switch
    {
        DeviceStatus.Online => DeviceStatusColors.Online,
        DeviceStatus.Offline => DeviceStatusColors.Offline,
        DeviceStatus.Error => DeviceStatusColors.Error,
        DeviceStatus.Updating => DeviceStatusColors.Updating,
        _ => DeviceStatusColors.Offline
    };

    private static Abstraction.Color GetCategoryColor(DeviceCategory category) => category switch
    {
        DeviceCategory.Light => new Abstraction.Color(1.0f, 0.92f, 0.23f),     // Yellow
        DeviceCategory.Thermostat => new Abstraction.Color(0.13f, 0.59f, 0.95f), // Blue
        DeviceCategory.Lock => new Abstraction.Color(0.61f, 0.15f, 0.69f),       // Purple
        DeviceCategory.Camera => new Abstraction.Color(0.3f, 0.69f, 0.31f),      // Green
        DeviceCategory.Sensor => new Abstraction.Color(0.0f, 0.74f, 0.83f),      // Teal
        DeviceCategory.Switch => new Abstraction.Color(1.0f, 0.6f, 0.0f),        // Orange
        _ => new Abstraction.Color(0.75f, 0.75f, 0.75f)                          // Gray
    };
}
