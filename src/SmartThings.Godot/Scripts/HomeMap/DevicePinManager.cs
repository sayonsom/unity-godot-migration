// =============================================================================
// DevicePinManager.cs — Samsung SmartThings-style device map pins
// Large circular billboard pins with emoji icons, status rings, and labels
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// Manages SmartThings-style device pin markers in the 3D Home Map View.
/// Each pin is a white circle billboard with an emoji icon overlay,
/// colored status ring (shader-based), and a text label underneath.
/// </summary>
public partial class DevicePinManager : GodotNative.Node3D
{
    private GodotNative.Shader? _pinShader;
    private readonly Dictionary<string, GodotNative.Node3D> _pins = new();
    private readonly Dictionary<string, GodotNative.ShaderMaterial> _pinMaterials = new();
    private readonly Dictionary<string, GodotNative.Label3D> _iconLabels = new();
    private readonly Dictionary<string, GodotNative.Label3D> _nameLabels = new();
    private readonly Dictionary<string, GodotNative.MeshInstance3D> _bgDiscs = new();

    private const float PinDiameter = 1.2f;
    private const float PinRadius = PinDiameter / 2f;
    private const float PinHeight = 1.8f;
    private const float CollisionRadius = 0.6f;
    private const float IconFontSize = 96;
    private const float LabelFontSize = 28;

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

        GodotNative.GD.Print($"[DevicePinManager] Spawned {_pins.Count} SmartThings-style device pins.");
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

    /// <summary>Get the Node3D for a specific device pin (for accessibility registration).</summary>
    public GodotNative.Node3D? GetPinNode(string deviceId)
    {
        return _pins.TryGetValue(deviceId, out var pin) ? pin : null;
    }

    /// <summary>Update a device's visual state from an IoT event.</summary>
    public void UpdateDeviceState(string deviceId, string capability, string value)
    {
        if (!_pinMaterials.TryGetValue(deviceId, out var mat)) return;

        bool isActive = value is "on" or "online" or "active" or "locked" or "open";
        mat.SetShaderParameter("is_active", isActive);

        if (value is "error" or "offline")
        {
            mat.SetShaderParameter("status_color",
                new GodotNative.Color(0.96f, 0.26f, 0.21f, 1.0f)); // Red
        }
        else if (isActive)
        {
            mat.SetShaderParameter("status_color",
                new GodotNative.Color(0.3f, 0.69f, 0.31f, 1.0f)); // Green
        }
        else
        {
            mat.SetShaderParameter("status_color",
                new GodotNative.Color(0.62f, 0.62f, 0.62f, 1.0f)); // Gray
        }
    }

    /// <summary>Remove all pins.</summary>
    public void ClearPins()
    {
        foreach (var pin in _pins.Values)
            pin.QueueFree();
        _pins.Clear();
        _pinMaterials.Clear();
        _iconLabels.Clear();
        _nameLabels.Clear();
        _bgDiscs.Clear();
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private void SpawnPin(DevicePlacement placement, SmartDevice device)
    {
        // Root Node3D container for the whole pin assembly
        var pinRoot = new GodotNative.Node3D();
        pinRoot.Name = $"Pin_{device.DeviceId}";
        pinRoot.Position = new GodotNative.Vector3(
            placement.Position.X,
            placement.Position.Y + PinHeight,
            placement.Position.Z);

        // ── 1. White circle background disc (StandardMaterial3D billboard) ──
        var bgDisc = CreateBackgroundDisc(placement.IconScale);
        pinRoot.AddChild(bgDisc);
        _bgDiscs[device.DeviceId] = bgDisc;

        // ── 2. Status ring overlay (shader-based, slightly larger) ──
        var ringMesh = CreateStatusRing(device, placement.IconScale);
        pinRoot.AddChild(ringMesh);

        // ── 3. Drop shadow disc (dark, below, offset back slightly) ──
        var shadow = CreateShadowDisc(placement.IconScale);
        pinRoot.AddChild(shadow);

        // ── 4. Emoji icon (Label3D centered on the pin) ──
        var iconLabel = CreateIconLabel(device.Category, placement.IconScale);
        pinRoot.AddChild(iconLabel);
        _iconLabels[device.DeviceId] = iconLabel;

        // ── 5. Device name label (Label3D below the pin) ──
        var nameLabel = CreateNameLabel(device.Label, placement.IconScale);
        pinRoot.AddChild(nameLabel);
        _nameLabels[device.DeviceId] = nameLabel;

        // ── 6. Tap collision area (large sphere for phone-friendly tapping) ──
        var area = CreateTapArea(device.DeviceId, placement.IconScale);
        pinRoot.AddChild(area);

        AddChild(pinRoot);
        _pins[device.DeviceId] = pinRoot;
    }

    private GodotNative.MeshInstance3D CreateBackgroundDisc(float scale)
    {
        // White circle using a QuadMesh with a StandardMaterial3D billboard
        var mesh = new GodotNative.QuadMesh();
        mesh.Size = new GodotNative.Vector2(PinDiameter * scale, PinDiameter * scale);

        var mat = new GodotNative.StandardMaterial3D();
        mat.AlbedoColor = new GodotNative.Color(1.0f, 1.0f, 1.0f, 0.95f);
        mat.Transparency = GodotNative.BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = GodotNative.BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.BillboardMode = GodotNative.BaseMaterial3D.BillboardModeEnum.Enabled;
        mat.CullMode = GodotNative.BaseMaterial3D.CullModeEnum.Disabled;
        mat.NoDepthTest = false;
        mat.RenderPriority = 1;

        var instance = new GodotNative.MeshInstance3D();
        instance.Mesh = mesh;
        instance.MaterialOverride = mat;
        instance.CastShadow = GodotNative.GeometryInstance3D.ShadowCastingSetting.Off;
        instance.Name = "BgDisc";
        instance.Position = GodotNative.Vector3.Zero;

        return instance;
    }

    private GodotNative.MeshInstance3D CreateStatusRing(SmartDevice device, float scale)
    {
        // Slightly larger quad with the device_pin shader for animated status ring
        var ringSize = (PinDiameter + 0.15f) * scale;
        var mesh = new GodotNative.QuadMesh();
        mesh.Size = new GodotNative.Vector2(ringSize, ringSize);

        var mat = CreateRingShaderMaterial(device);

        var instance = new GodotNative.MeshInstance3D();
        instance.Mesh = mesh;
        instance.MaterialOverride = mat;
        instance.CastShadow = GodotNative.GeometryInstance3D.ShadowCastingSetting.Off;
        instance.Name = "StatusRing";
        // Render slightly in front of background disc
        instance.Position = new GodotNative.Vector3(0, 0, -0.001f);

        _pinMaterials[device.DeviceId] = mat;
        return instance;
    }

    private GodotNative.MeshInstance3D CreateShadowDisc(float scale)
    {
        var shadowSize = PinDiameter * scale * 0.9f;
        var mesh = new GodotNative.QuadMesh();
        mesh.Size = new GodotNative.Vector2(shadowSize, shadowSize);

        var mat = new GodotNative.StandardMaterial3D();
        mat.AlbedoColor = new GodotNative.Color(0.0f, 0.0f, 0.0f, 0.2f);
        mat.Transparency = GodotNative.BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = GodotNative.BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.BillboardMode = GodotNative.BaseMaterial3D.BillboardModeEnum.Enabled;
        mat.CullMode = GodotNative.BaseMaterial3D.CullModeEnum.Disabled;
        mat.RenderPriority = 0;

        var instance = new GodotNative.MeshInstance3D();
        instance.Mesh = mesh;
        instance.MaterialOverride = mat;
        instance.CastShadow = GodotNative.GeometryInstance3D.ShadowCastingSetting.Off;
        instance.Name = "Shadow";
        // Offset slightly behind and below the main disc
        instance.Position = new GodotNative.Vector3(0.04f, -0.04f, 0.002f);

        return instance;
    }

    private GodotNative.Label3D CreateIconLabel(DeviceCategory category, float scale)
    {
        var label = new GodotNative.Label3D();
        label.Text = GetCategoryEmoji(category);
        label.FontSize = (int)(IconFontSize * scale);
        label.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        label.VerticalAlignment = GodotNative.VerticalAlignment.Center;
        label.Billboard = GodotNative.BaseMaterial3D.BillboardModeEnum.Enabled;
        label.NoDepthTest = false;
        label.Shaded = false;
        label.DoubleSided = true;
        label.FixedSize = false;
        label.PixelSize = 0.005f;
        label.RenderPriority = 3;
        label.Modulate = new GodotNative.Color(1.0f, 1.0f, 1.0f, 1.0f);
        label.Name = "IconLabel";
        label.Position = new GodotNative.Vector3(0, 0.02f, -0.002f);

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
        label.PixelSize = 0.005f;
        label.RenderPriority = 2;
        label.OutlineSize = 8;
        label.OutlineModulate = new GodotNative.Color(0, 0, 0, 0.6f);
        label.Modulate = new GodotNative.Color(1.0f, 1.0f, 1.0f, 0.95f);
        label.Name = "NameLabel";
        // Position below the pin circle
        label.Position = new GodotNative.Vector3(0, -(PinRadius * scale + 0.12f), -0.001f);

        return label;
    }

    private GodotNative.Area3D CreateTapArea(string deviceId, float scale)
    {
        var area = new GodotNative.Area3D();
        area.SetMeta("device_id", deviceId);
        area.Name = "TapArea";

        var colShape = new GodotNative.CollisionShape3D();
        var sphere = new GodotNative.SphereShape3D();
        sphere.Radius = CollisionRadius * scale;
        colShape.Shape = sphere;
        area.AddChild(colShape);

        area.InputEvent += (camera, inputEvent, position, normal, shapeIdx) =>
        {
            if (inputEvent is GodotNative.InputEventMouseButton mb && mb.Pressed
                && mb.ButtonIndex == GodotNative.MouseButton.Left)
            {
                EmitSignal(SignalName.DevicePinTapped, deviceId);
            }
            if (inputEvent is GodotNative.InputEventScreenTouch touch && touch.Pressed)
            {
                EmitSignal(SignalName.DevicePinTapped, deviceId);
            }
        };

        return area;
    }

    private GodotNative.ShaderMaterial CreateRingShaderMaterial(SmartDevice device)
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

    // ── Category emoji mapping ────────────────────────────────────────────

    private static string GetCategoryEmoji(DeviceCategory category) => category switch
    {
        DeviceCategory.Light => "\U0001F4A1",       // light bulb
        DeviceCategory.Thermostat => "\u2744\uFE0F", // snowflake
        DeviceCategory.Lock => "\U0001F512",          // lock
        DeviceCategory.Camera => "\U0001F4F7",        // camera
        DeviceCategory.Sensor => "\U0001F4E1",        // satellite antenna
        DeviceCategory.Switch => "\u2699\uFE0F",      // gear
        DeviceCategory.Television => "\U0001F4FA",    // TV
        DeviceCategory.Speaker => "\U0001F50A",       // speaker high volume
        DeviceCategory.Appliance => "\U0001F3E0",     // house
        DeviceCategory.Hub => "\U0001F310",           // globe with meridians
        _ => "\u2699\uFE0F"                           // gear (default)
    };

    // ── Status and category color helpers ─────────────────────────────────

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
        DeviceCategory.Television => new Abstraction.Color(0.4f, 0.23f, 0.72f),  // Deep purple
        DeviceCategory.Speaker => new Abstraction.Color(0.91f, 0.12f, 0.39f),    // Pink
        DeviceCategory.Appliance => new Abstraction.Color(0.47f, 0.33f, 0.28f),  // Brown
        DeviceCategory.Hub => new Abstraction.Color(0.13f, 0.59f, 0.95f),        // Blue
        _ => new Abstraction.Color(0.75f, 0.75f, 0.75f)                          // Gray
    };
}
