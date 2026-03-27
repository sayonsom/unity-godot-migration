// =============================================================================
// HomeMapAssembler.cs — Orchestrates the full 3D home map from SmartHome data
// Generates rooms, manages selection state, and wires scene components
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// Assembles the complete 3D Home Map from SmartHome data.
/// Generates room meshes, places device pins, configures lighting.
/// </summary>
public partial class HomeMapAssembler : GodotNative.Node3D
{
    private SmartHome? _home;
    private string? _selectedRoomId;
    private readonly Dictionary<string, GodotNative.Node3D> _roomNodes = new();
    private readonly Dictionary<string, GodotNative.ShaderMaterial> _floorMaterials = new();

    private DevicePinManager? _pinManager;
    private IsometricCameraController? _camera;
    private HomeMapUI? _ui;

    /// <summary>Fired when a room is selected.</summary>
    [GodotNative.Signal] public delegate void RoomSelectedEventHandler(string roomId, string roomName);

    public override void _Ready()
    {
        _pinManager = GetNodeOrNull<DevicePinManager>("../DevicePins");
        _camera = GetNodeOrNull<IsometricCameraController>("../IsometricCamera");
        _ui = GetNodeOrNull<HomeMapUI>("../UIOverlay/HomeMapUI");

        // Wire camera signals
        if (_camera != null)
        {
            _camera.ScreenTapped += OnScreenTapped;
        }

        if (_pinManager != null)
        {
            _pinManager.DevicePinTapped += OnDevicePinTapped;
        }

        GodotNative.GD.Print("[HomeMapAssembler] Ready.");
    }

    /// <summary>Build the 3D home from SmartHome data.</summary>
    public void BuildHome(SmartHome home)
    {
        _home = home;
        ClearHome();

        foreach (var room in home.Rooms)
        {
            var roomNode = RoomMeshGenerator.GenerateRoom(room);
            AddChild(roomNode);
            _roomNodes[room.RoomId] = roomNode;

            // Track floor material for selection highlighting
            var floorInstance = roomNode.GetNodeOrNull<GodotNative.MeshInstance3D>("Floor");
            if (floorInstance?.MaterialOverride is GodotNative.ShaderMaterial mat)
            {
                _floorMaterials[room.RoomId] = mat;
            }

            // Add floating room name label
            AddRoomLabel(roomNode, room);
        }

        // Spawn device pins
        _pinManager?.SpawnPins(home);

        // Center camera on home
        CenterCamera();

        GodotNative.GD.Print($"[HomeMapAssembler] Built home '{home.Name}' with {home.Rooms.Count} rooms.");
    }

    /// <summary>Select a room (highlight it, deselect previous).</summary>
    public void SelectRoom(string? roomId)
    {
        // Deselect previous
        if (_selectedRoomId != null && _floorMaterials.TryGetValue(_selectedRoomId, out var prevMat))
        {
            prevMat.SetShaderParameter("is_selected", false);
        }

        _selectedRoomId = roomId;

        // Select new
        if (roomId != null && _floorMaterials.TryGetValue(roomId, out var newMat))
        {
            newMat.SetShaderParameter("is_selected", true);

            var room = _home?.Rooms.Find(r => r.RoomId == roomId);
            if (room != null)
            {
                EmitSignal(SignalName.RoomSelected, room.RoomId, room.Name);
            }
        }
    }

    /// <summary>Clear all generated room geometry.</summary>
    public void ClearHome()
    {
        foreach (var node in _roomNodes.Values)
            node.QueueFree();
        _roomNodes.Clear();
        _floorMaterials.Clear();
        _pinManager?.ClearPins();
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnScreenTapped(GodotNative.Vector2 screenPos)
    {
        // Raycast from camera to find which room/device was tapped
        if (_camera == null) return;

        var from = _camera.ProjectRayOrigin(screenPos);
        var to = from + _camera.ProjectRayNormal(screenPos) * 100.0f;

        var spaceState = _camera.GetWorld3D().DirectSpaceState;
        var query = GodotNative.PhysicsRayQueryParameters3D.Create(from, to);
        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var collider = result["collider"].AsGodotObject();
            if (collider is GodotNative.Node3D node && node.HasMeta("room_id"))
            {
                string roomId = node.GetMeta("room_id").AsString();
                SelectRoom(roomId);
                return;
            }
        }

        // Tapped empty space — deselect
        SelectRoom(null);
        _ui?.HidePopups();
    }

    private void OnDevicePinTapped(string deviceId)
    {
        var device = _home?.Devices.Find(d => d.DeviceId == deviceId);
        if (device != null)
        {
            GodotNative.GD.Print($"[HomeMap] Device tapped: {device.Label} ({device.Category})");
            _ui?.ShowDevicePopup(device);
        }
    }

    // ── Room labels ────────────────────────────────────────────────────────

    private void AddRoomLabel(GodotNative.Node3D roomNode, SmartRoom room)
    {
        if (room.FloorPolygon == null || room.FloorPolygon.Count < 3) return;

        // Calculate center of room polygon
        float cx = 0, cz = 0;
        foreach (var p in room.FloorPolygon)
        {
            cx += p.X;
            cz += p.Y;
        }
        cx /= room.FloorPolygon.Count;
        cz /= room.FloorPolygon.Count;

        // Create a Label3D floating above the floor
        var label = new GodotNative.Label3D();
        label.Text = room.Name;
        label.FontSize = 72;
        label.OutlineSize = 8;
        label.Modulate = new GodotNative.Color(0.2f, 0.2f, 0.25f, 1.0f);
        label.OutlineModulate = new GodotNative.Color(1, 1, 1, 0.7f);
        label.Position = new GodotNative.Vector3(cx, 0.05f, cz);
        // Face upward so it's readable from isometric view
        label.RotationDegrees = new GodotNative.Vector3(-90, 0, 0);
        label.Billboard = GodotNative.BaseMaterial3D.BillboardModeEnum.Disabled;
        label.PixelSize = 0.005f;
        label.NoDepthTest = true; // Always visible through walls
        label.FixedSize = false;

        roomNode.AddChild(label);

        // Also add device count below room name
        int deviceCount = room.DeviceIds?.Count ?? room.Devices.Count;
        if (deviceCount > 0)
        {
            var countLabel = new GodotNative.Label3D();
            countLabel.Text = $"{deviceCount} device{(deviceCount != 1 ? "s" : "")}";
            countLabel.FontSize = 48;
            countLabel.Modulate = new GodotNative.Color(0.4f, 0.4f, 0.45f, 0.8f);
            countLabel.Position = new GodotNative.Vector3(cx, 0.04f, cz + 0.5f);
            countLabel.RotationDegrees = new GodotNative.Vector3(-90, 0, 0);
            countLabel.Billboard = GodotNative.BaseMaterial3D.BillboardModeEnum.Disabled;
            countLabel.PixelSize = 0.004f;
            countLabel.NoDepthTest = true;
            countLabel.FixedSize = false;

            roomNode.AddChild(countLabel);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void CenterCamera()
    {
        if (_home == null || _home.Rooms.Count == 0) return;

        // Calculate bounding box of all rooms
        var minPos = new GodotNative.Vector3(float.MaxValue, 0, float.MaxValue);
        var maxPos = new GodotNative.Vector3(float.MinValue, 0, float.MinValue);

        foreach (var room in _home.Rooms)
        {
            if (room.FloorPolygon == null) continue;
            foreach (var p in room.FloorPolygon)
            {
                minPos.X = Math.Min(minPos.X, p.X);
                minPos.Z = Math.Min(minPos.Z, p.Y);
                maxPos.X = Math.Max(maxPos.X, p.X);
                maxPos.Z = Math.Max(maxPos.Z, p.Y);
            }
        }

        // Set camera bounds and center view
        _camera?.SetBounds(minPos, maxPos);

        var center = (minPos + maxPos) * 0.5f;
        float span = GodotNative.Mathf.Max(maxPos.X - minPos.X, maxPos.Z - minPos.Z);
        _camera?.FocusOn(center, span * 0.6f);
    }

    private static GodotNative.Aabb CalculateNodeBounds(GodotNative.Node3D node)
    {
        var aabb = new GodotNative.Aabb();
        bool first = true;

        foreach (var child in node.GetChildren())
        {
            if (child is GodotNative.MeshInstance3D meshInstance)
            {
                var meshAabb = meshInstance.GetAabb();
                if (first)
                {
                    aabb = meshAabb;
                    first = false;
                }
                else
                {
                    aabb = aabb.Merge(meshAabb);
                }
            }
        }

        return aabb;
    }
}
