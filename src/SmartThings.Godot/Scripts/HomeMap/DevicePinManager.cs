// =============================================================================
// DevicePinManager.cs — Google Maps-style 3D location pins for IoT devices
// Real 3D geometry: colored sphere head + thin cylinder stick + label
// Clickable via raycast, occluded by walls, visible from isometric view
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// Manages Google Maps-style 3D device pins in the Home Map.
///
/// Each pin is built from real 3D meshes (no shaders needed):
///   - Colored sphere "head" with category letter
///   - Thin dark cylinder "stick" connecting head to floor
///   - Billboard Label3D for device name
///   - StaticBody3D collision for tap detection via raycast
///
/// Pins are occluded by walls (proper depth testing).
/// </summary>
public partial class DevicePinManager : GodotNative.Node3D
{
    private readonly Dictionary<string, GodotNative.Node3D> _pins = new();
    private readonly Dictionary<string, GodotNative.StandardMaterial3D> _headMaterials = new();
    private readonly Dictionary<string, GodotNative.Label3D> _iconLabels = new();
    private readonly Dictionary<string, GodotNative.Label3D> _nameLabels = new();

    // ── Pin geometry sizes ──
    private const float HeadRadius = 0.28f;      // Sphere head radius
    private const float StickRadius = 0.04f;      // Thin cylinder
    private const float StickHeight = 0.7f;       // Height of stick from floor
    private const float HeadCenterY = StickHeight + HeadRadius; // Center of sphere
    private const float TotalHeight = StickHeight + HeadRadius * 2; // Full pin height
    private const float CollisionRadius = 0.5f;   // Tap target — generous for phone

    private const float IconFontSize = 80;        // Category letter on sphere
    private const float LabelFontSize = 28;       // Device name below

    /// <summary>Fired when a device pin is tapped.</summary>
    [GodotNative.Signal] public delegate void DevicePinTappedEventHandler(string deviceId);

    public override void _Ready()
    {
        GodotNative.GD.Print("[DevicePinManager] Ready — 3D pin style.");
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

        GodotNative.GD.Print($"[DevicePinManager] Spawned {_pins.Count} 3D pins.");
    }

    /// <summary>Update a single pin's head color based on status.</summary>
    public void UpdatePinStatus(string deviceId, DeviceStatus status, bool isActive)
    {
        if (!_headMaterials.TryGetValue(deviceId, out var mat)) return;
        var c = GetStatusIndicatorColor(status);
        mat.EmissionEnabled = isActive;
        if (isActive)
        {
            mat.Emission = new GodotNative.Color(c.R, c.G, c.B);
            mat.EmissionEnergyMultiplier = 0.3f;
        }
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

    /// <summary>Get the Node3D for a specific device pin.</summary>
    public GodotNative.Node3D? GetPinNode(string deviceId)
    {
        return _pins.TryGetValue(deviceId, out var pin) ? pin : null;
    }

    /// <summary>Update a device's visual state from an IoT event.</summary>
    public void UpdateDeviceState(string deviceId, string capability, string value)
    {
        if (!_headMaterials.TryGetValue(deviceId, out var mat)) return;

        bool isActive = value is "on" or "online" or "active" or "locked" or "open";
        mat.EmissionEnabled = isActive;
        if (isActive)
        {
            mat.Emission = new GodotNative.Color(0.3f, 0.9f, 0.3f);
            mat.EmissionEnergyMultiplier = 0.4f;
        }

        if (value is "error" or "offline")
        {
            mat.AlbedoColor = new GodotNative.Color(0.85f, 0.2f, 0.2f);
        }
    }

    /// <summary>Remove all pins.</summary>
    public void ClearPins()
    {
        foreach (var pin in _pins.Values)
            pin.QueueFree();
        _pins.Clear();
        _headMaterials.Clear();
        _iconLabels.Clear();
        _nameLabels.Clear();
    }

    // ── Pin Construction ─────────────────────────────────────────────────────

    private void SpawnPin(DevicePlacement placement, SmartDevice device)
    {
        var pinRoot = new GodotNative.Node3D();
        pinRoot.Name = $"Pin_{device.DeviceId}";
        // Pin base sits on the floor at device position
        pinRoot.Position = new GodotNative.Vector3(
            placement.Position.X,
            placement.Position.Y,
            placement.Position.Z);
        pinRoot.SetMeta("device_id", device.DeviceId);

        float s = placement.IconScale;

        // 1. Stick (thin cylinder from floor up)
        var stick = CreateStick(s);
        pinRoot.AddChild(stick);

        // 2. Sphere head (colored, at top of stick)
        var head = CreateHead(device, s);
        pinRoot.AddChild(head);

        // 3. Category letter on sphere
        var icon = CreateIconLabel(device.Category, s);
        pinRoot.AddChild(icon);
        _iconLabels[device.DeviceId] = icon;

        // 4. Device name label (below stick base)
        var nameLabel = CreateNameLabel(device.Label, s);
        pinRoot.AddChild(nameLabel);
        _nameLabels[device.DeviceId] = nameLabel;

        // 5. Collision body for raycast tap detection
        var body = CreateCollisionBody(device.DeviceId, s);
        pinRoot.AddChild(body);

        AddChild(pinRoot);
        _pins[device.DeviceId] = pinRoot;
    }

    private GodotNative.MeshInstance3D CreateStick(float scale)
    {
        var cylinder = new GodotNative.CylinderMesh();
        cylinder.TopRadius = StickRadius * scale;
        cylinder.BottomRadius = StickRadius * scale;
        cylinder.Height = StickHeight * scale;
        cylinder.RadialSegments = 8;

        var mat = new GodotNative.StandardMaterial3D();
        mat.AlbedoColor = new GodotNative.Color(0.3f, 0.3f, 0.35f);
        mat.ShadingMode = GodotNative.BaseMaterial3D.ShadingModeEnum.Unshaded;

        var instance = new GodotNative.MeshInstance3D();
        instance.Mesh = cylinder;
        instance.MaterialOverride = mat;
        instance.CastShadow = GodotNative.GeometryInstance3D.ShadowCastingSetting.Off;
        instance.Name = "Stick";
        // Cylinder is centered on its Y, so offset up by half height
        instance.Position = new GodotNative.Vector3(0, StickHeight * scale * 0.5f, 0);

        return instance;
    }

    private GodotNative.MeshInstance3D CreateHead(SmartDevice device, float scale)
    {
        var sphere = new GodotNative.SphereMesh();
        sphere.Radius = HeadRadius * scale;
        sphere.Height = HeadRadius * 2 * scale;
        sphere.RadialSegments = 16;
        sphere.Rings = 8;

        var catColor = GetCategoryColor(device.Category);
        var mat = new GodotNative.StandardMaterial3D();
        mat.AlbedoColor = new GodotNative.Color(catColor.R, catColor.G, catColor.B);
        mat.ShadingMode = GodotNative.BaseMaterial3D.ShadingModeEnum.PerPixel;
        mat.Roughness = 0.4f;
        mat.Metallic = 0.1f;

        // Active devices get a subtle glow
        if (device.Status == DeviceStatus.Online)
        {
            mat.EmissionEnabled = true;
            mat.Emission = new GodotNative.Color(catColor.R * 0.5f, catColor.G * 0.5f, catColor.B * 0.5f);
            mat.EmissionEnergyMultiplier = 0.2f;
        }

        _headMaterials[device.DeviceId] = mat;

        var instance = new GodotNative.MeshInstance3D();
        instance.Mesh = sphere;
        instance.MaterialOverride = mat;
        instance.CastShadow = GodotNative.GeometryInstance3D.ShadowCastingSetting.Off;
        instance.Name = "Head";
        instance.Position = new GodotNative.Vector3(0, HeadCenterY * scale, 0);

        return instance;
    }

    private GodotNative.Label3D CreateIconLabel(DeviceCategory category, float scale)
    {
        var label = new GodotNative.Label3D();
        label.Text = GetCategoryIcon(category);
        label.FontSize = (int)(IconFontSize * scale);
        label.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        label.VerticalAlignment = GodotNative.VerticalAlignment.Center;
        label.Billboard = GodotNative.BaseMaterial3D.BillboardModeEnum.Enabled;
        label.NoDepthTest = false;
        label.Shaded = false;
        label.DoubleSided = true;
        label.FixedSize = false;
        label.PixelSize = 0.003f;
        label.RenderPriority = 5;
        label.Modulate = new GodotNative.Color(1f, 1f, 1f, 1f);
        label.OutlineSize = 10;
        label.OutlineModulate = new GodotNative.Color(0, 0, 0, 0.6f);
        label.Name = "IconLabel";
        // Positioned at sphere center, slightly in front
        label.Position = new GodotNative.Vector3(0, HeadCenterY * scale, 0);

        return label;
    }

    private GodotNative.Label3D CreateNameLabel(string deviceLabel, float scale)
    {
        var label = new GodotNative.Label3D();
        label.Text = deviceLabel;
        label.FontSize = (int)(LabelFontSize * scale);
        label.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        label.VerticalAlignment = GodotNative.VerticalAlignment.Top;
        label.Billboard = GodotNative.BaseMaterial3D.BillboardModeEnum.Enabled;
        label.NoDepthTest = false;
        label.Shaded = false;
        label.DoubleSided = true;
        label.FixedSize = false;
        label.PixelSize = 0.003f;
        label.RenderPriority = 4;
        label.OutlineSize = 8;
        label.OutlineModulate = new GodotNative.Color(0, 0, 0, 0.7f);
        label.Modulate = new GodotNative.Color(1f, 1f, 1f, 0.95f);
        label.Name = "NameLabel";
        // Just below the floor level
        label.Position = new GodotNative.Vector3(0, -0.1f, 0);

        return label;
    }

    private GodotNative.StaticBody3D CreateCollisionBody(string deviceId, float scale)
    {
        var body = new GodotNative.StaticBody3D();
        body.SetMeta("device_id", deviceId);
        body.Name = "TapBody";
        body.InputRayPickable = true;

        // Layer 2 for device pins (rooms use layer 1)
        body.CollisionLayer = 2;
        body.CollisionMask = 0;

        var colShape = new GodotNative.CollisionShape3D();
        // Capsule covers the full pin height for easier tapping
        var capsule = new GodotNative.CapsuleShape3D();
        capsule.Radius = CollisionRadius * scale;
        capsule.Height = TotalHeight * scale;
        colShape.Shape = capsule;
        // Center the capsule on the pin's vertical center
        colShape.Position = new GodotNative.Vector3(0, TotalHeight * scale * 0.5f, 0);
        body.AddChild(colShape);

        return body;
    }

    // ── Category icon mapping ───────────────────────────────────────────────

    private static string GetCategoryIcon(DeviceCategory category) => category switch
    {
        DeviceCategory.Light       => "L",
        DeviceCategory.Thermostat  => "T",
        DeviceCategory.Lock        => "K",
        DeviceCategory.Camera      => "C",
        DeviceCategory.Sensor      => "S",
        DeviceCategory.Switch      => "SW",
        DeviceCategory.Television  => "TV",
        DeviceCategory.Speaker     => "SP",
        DeviceCategory.Appliance   => "A",
        DeviceCategory.Hub         => "H",
        _                          => "?"
    };

    // ── Color helpers ───────────────────────────────────────────────────────

    private static Abstraction.Color GetStatusIndicatorColor(DeviceStatus status) => status switch
    {
        DeviceStatus.Online => DeviceStatusColors.Online,
        DeviceStatus.Offline => DeviceStatusColors.Offline,
        DeviceStatus.Error => DeviceStatusColors.Error,
        DeviceStatus.Updating => DeviceStatusColors.Updating,
        _ => DeviceStatusColors.Offline
    };

    private static Abstraction.Color GetCategoryColor(DeviceCategory category) => category switch
    {
        DeviceCategory.Light      => new Abstraction.Color(0.95f, 0.75f, 0.0f),       // Gold
        DeviceCategory.Thermostat => new Abstraction.Color(0.20f, 0.55f, 0.90f),      // Blue
        DeviceCategory.Lock       => new Abstraction.Color(0.60f, 0.20f, 0.70f),      // Purple
        DeviceCategory.Camera     => new Abstraction.Color(0.20f, 0.65f, 0.30f),      // Green
        DeviceCategory.Sensor     => new Abstraction.Color(0.0f, 0.70f, 0.80f),       // Teal
        DeviceCategory.Switch     => new Abstraction.Color(0.95f, 0.50f, 0.0f),       // Orange
        DeviceCategory.Television => new Abstraction.Color(0.45f, 0.25f, 0.70f),      // Deep purple
        DeviceCategory.Speaker    => new Abstraction.Color(0.88f, 0.15f, 0.40f),      // Pink
        DeviceCategory.Appliance  => new Abstraction.Color(0.55f, 0.40f, 0.30f),      // Brown
        DeviceCategory.Hub        => new Abstraction.Color(0.20f, 0.55f, 0.90f),      // Blue
        _                         => new Abstraction.Color(0.55f, 0.55f, 0.55f)       // Gray
    };
}
