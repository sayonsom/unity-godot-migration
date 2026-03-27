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
            _camera.RoomTapped += OnRoomTapped;
            _camera.RoomDoubleTapped += OnRoomDoubleTapped;
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

    private void OnRoomTapped(string roomId)
    {
        SelectRoom(roomId);
    }

    private void OnRoomDoubleTapped(string roomId)
    {
        SelectRoom(roomId);

        // Zoom camera to fit the room
        if (_roomNodes.TryGetValue(roomId, out var roomNode))
        {
            var aabb = CalculateNodeBounds(roomNode);
            _camera?.ZoomToFit(aabb.GetCenter(), aabb.GetLongestAxisSize() * 0.5f);
        }
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

        var center = (minPos + maxPos) * 0.5f;
        float radius = (maxPos - minPos).Length() * 0.5f;
        _camera?.ZoomToFit(center, radius);
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
