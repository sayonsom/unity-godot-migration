// =============================================================================
// HomeMapAccessibilityManager.cs — TalkBack/screen reader support for Home Map
// Labels all 3D elements, manages focus ring, announces state changes.
// Navigation via on-screen buttons (AccessibilityTestPanel), keyboard (Tab),
// or Android TalkBack native gestures (handled by OS, not here).
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.Accessibility;

/// <summary>
/// Manages accessibility for the 3D Home Map scene:
///   - Registers all rooms and device pins as accessible elements
///   - Provides sequential focus navigation via public methods
///   - Shows a visible focus ring around the focused element
///   - Announces room info and device status on focus
///
/// NOTE: Touch swipe gestures are NOT handled here (they conflict with camera pan).
/// Instead, navigation happens via:
///   1. The A11Y test panel buttons (Prev/Next/Activate)
///   2. Keyboard Tab/Shift+Tab (desktop)
///   3. Android TalkBack native gestures (OS-level, no code needed)
///   4. Volume button shortcuts (see below)
/// </summary>
public partial class HomeMapAccessibilityManager : GodotNative.Node
{
    private IAccessibilityService? _a11y;
    private SmartHome? _home;

    // Focus navigation state
    private readonly List<AccessibleElement> _elements = new();
    private int _focusIndex = -1;
    private GodotNative.Node3D? _focusRing;

    // Focus ring visual
    private GodotNative.MeshInstance3D? _focusRingMesh;
    private float _focusRingPhase;

    /// <summary>Fired when a room is focused via accessibility navigation.</summary>
    [GodotNative.Signal] public delegate void RoomFocusedEventHandler(string roomId);

    /// <summary>Fired when a device is focused via accessibility navigation.</summary>
    [GodotNative.Signal] public delegate void DeviceFocusedEventHandler(string deviceId);

    public override void _Ready()
    {
        BuildFocusRing();
    }

    /// <summary>Set the accessibility service and home data.</summary>
    public void Initialize(IAccessibilityService a11y, SmartHome home)
    {
        _a11y = a11y;
        _home = home;

        _a11y.Announce(
            $"Home Map View. {home.Name} with {home.Rooms.Count} rooms and {home.Devices.Count} devices. " +
            "Use the accessibility panel to navigate.",
            AnnouncePriority.High);
    }

    /// <summary>Register a 3D room node as an accessible element.</summary>
    public void RegisterRoom(GodotNative.Node3D node, SmartRoom room)
    {
        int deviceCount = room.DeviceIds?.Count ?? room.Devices.Count;
        var info = new AccessibleInfo(
            Name: room.Name,
            Description: $"{room.RoomType} with {deviceCount} device{(deviceCount != 1 ? "s" : "")}",
            Role: AccessibleRole.Container,
            Value: deviceCount.ToString());

        var element = new AccessibleElement(
            Node: node,
            Info: info,
            ElementType: AccessibleElementType.Room,
            Id: room.RoomId);

        _elements.Add(element);

        node.SetMeta("a11y_name", info.Name);
        node.SetMeta("a11y_desc", info.Description);
        node.SetMeta("a11y_role", "room");
    }

    /// <summary>Register a device pin as an accessible element.</summary>
    public void RegisterDevice(GodotNative.Node3D node, SmartDevice device)
    {
        var info = new AccessibleInfo(
            Name: device.Label,
            Description: $"{device.Category} device, status: {device.Status}",
            Role: AccessibleRole.Device3D,
            Value: device.Status.ToString());

        var element = new AccessibleElement(
            Node: node,
            Info: info,
            ElementType: AccessibleElementType.Device,
            Id: device.DeviceId);

        _elements.Add(element);

        node.SetMeta("a11y_name", info.Name);
        node.SetMeta("a11y_desc", info.Description);
        node.SetMeta("a11y_role", "device");
    }

    /// <summary>Navigate to the next accessible element.</summary>
    public void FocusNext()
    {
        if (_elements.Count == 0) return;
        _focusIndex = (_focusIndex + 1) % _elements.Count;
        ApplyFocus();
    }

    /// <summary>Navigate to the previous accessible element.</summary>
    public void FocusPrevious()
    {
        if (_elements.Count == 0) return;
        _focusIndex = (_focusIndex - 1 + _elements.Count) % _elements.Count;
        ApplyFocus();
    }

    /// <summary>Activate the currently focused element.</summary>
    public void ActivateFocused()
    {
        if (_focusIndex < 0 || _focusIndex >= _elements.Count) return;

        var element = _elements[_focusIndex];
        switch (element.ElementType)
        {
            case AccessibleElementType.Room:
                EmitSignal(SignalName.RoomFocused, element.Id);
                _a11y?.Announce($"Selected {element.Info.Name}. {element.Info.Description}", AnnouncePriority.Normal);
                break;

            case AccessibleElementType.Device:
                EmitSignal(SignalName.DeviceFocused, element.Id);
                _a11y?.Announce($"Selected {element.Info.Name}. {element.Info.Description}", AnnouncePriority.Normal);
                break;
        }
    }

    /// <summary>Focus a specific element by its ID (with TTS announcement).</summary>
    public void FocusElement(string id)
    {
        var idx = _elements.FindIndex(e => e.Id == id);
        if (idx >= 0)
        {
            _focusIndex = idx;
            ApplyFocus();
        }
    }

    /// <summary>Move focus ring to element WITHOUT TTS (caller handles announcement).</summary>
    public void FocusElementSilent(string id)
    {
        var idx = _elements.FindIndex(e => e.Id == id);
        if (idx >= 0)
        {
            _focusIndex = idx;
            var element = _elements[_focusIndex];
            if (GodotNative.GodotObject.IsInstanceValid(element.Node))
                ShowFocusRing(element.Node.GlobalPosition);
        }
    }

    /// <summary>Announce a device state change to the screen reader.</summary>
    public void AnnounceDeviceChange(SmartDevice device, string change)
    {
        _a11y?.Announce($"{device.Label}: {change}", AnnouncePriority.Normal);

        var element = _elements.FirstOrDefault(e => e.Id == device.DeviceId);
        if (element != null)
        {
            var newInfo = element.Info with
            {
                Description = $"{device.Category} device, status: {device.Status}",
                Value = device.Status.ToString()
            };
            var idx = _elements.IndexOf(element);
            _elements[idx] = element with { Info = newInfo };
        }
    }

    /// <summary>Clear all registered elements.</summary>
    public void ClearElements()
    {
        _elements.Clear();
        _focusIndex = -1;
        HideFocusRing();
    }

    /// <summary>Get the current focused element info (for UI display).</summary>
    public (string name, string type, int index, int total)? GetFocusInfo()
    {
        if (_focusIndex < 0 || _focusIndex >= _elements.Count) return null;
        var el = _elements[_focusIndex];
        return (el.Info.Name, el.ElementType.ToString(), _focusIndex, _elements.Count);
    }

    // ── Keyboard input (desktop / Bluetooth keyboard) ─────────────────────────

    public override void _UnhandledInput(GodotNative.InputEvent @event)
    {
        if (@event is GodotNative.InputEventKey key && key.Pressed)
        {
            switch (key.Keycode)
            {
                case GodotNative.Key.Tab:
                    if (key.ShiftPressed) FocusPrevious();
                    else FocusNext();
                    GetViewport().SetInputAsHandled();
                    break;

                case GodotNative.Key.Enter:
                case GodotNative.Key.Space:
                    ActivateFocused();
                    GetViewport().SetInputAsHandled();
                    break;
            }
        }
    }

    // ── Focus ring visual ───────────────────────────────────────────────────

    private void BuildFocusRing()
    {
        _focusRing = new GodotNative.Node3D();
        _focusRing.Name = "AccessibilityFocusRing";
        _focusRing.Visible = false;
        AddChild(_focusRing);

        _focusRingMesh = new GodotNative.MeshInstance3D();
        var torusMesh = new GodotNative.TorusMesh();
        torusMesh.InnerRadius = 0.8f;
        torusMesh.OuterRadius = 1.0f;
        torusMesh.Rings = 16;
        torusMesh.RingSegments = 16;
        _focusRingMesh.Mesh = torusMesh;

        var mat = new GodotNative.StandardMaterial3D();
        mat.AlbedoColor = new GodotNative.Color(1.0f, 0.6f, 0.0f, 0.8f);
        mat.ShadingMode = GodotNative.BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.Transparency = GodotNative.BaseMaterial3D.TransparencyEnum.Alpha;
        mat.NoDepthTest = true;
        mat.RenderPriority = 10;
        _focusRingMesh.MaterialOverride = mat;

        _focusRing.AddChild(_focusRingMesh);
    }

    public override void _Process(double delta)
    {
        if (_focusRing == null || !_focusRing.Visible) return;

        // Gentle rotation animation
        _focusRingPhase += (float)delta;
        _focusRing.RotationDegrees = new GodotNative.Vector3(
            90, _focusRingPhase * 45f, 0);

        // Pulse scale
        var scale = 1.0f + 0.05f * MathF.Sin(_focusRingPhase * 3f);
        _focusRing.Scale = new GodotNative.Vector3(scale, scale, scale);
    }

    private void ApplyFocus()
    {
        if (_focusIndex < 0 || _focusIndex >= _elements.Count) return;

        var element = _elements[_focusIndex];
        if (!GodotNative.GodotObject.IsInstanceValid(element.Node)) return;

        ShowFocusRing(element.Node.GlobalPosition);

        var announcement = element.ElementType == AccessibleElementType.Room
            ? $"Room: {element.Info.Name}. {element.Info.Description}"
            : $"Device: {element.Info.Name}. {element.Info.Description}";

        _a11y?.Announce(announcement, AnnouncePriority.Normal);

        // Emit appropriate signal
        if (element.ElementType == AccessibleElementType.Room)
            EmitSignal(SignalName.RoomFocused, element.Id);
        else
            EmitSignal(SignalName.DeviceFocused, element.Id);

        GodotNative.GD.Print($"[A11y] Focus [{_focusIndex + 1}/{_elements.Count}]: {announcement}");
    }

    private void ShowFocusRing(GodotNative.Vector3 position)
    {
        if (_focusRing == null) return;
        _focusRing.GlobalPosition = position + new GodotNative.Vector3(0, 0.2f, 0);
        _focusRing.Visible = true;
        _focusRingPhase = 0;
    }

    private void HideFocusRing()
    {
        if (_focusRing != null) _focusRing.Visible = false;
    }
}

// ── Types ───────────────────────────────────────────────────────────────────

internal enum AccessibleElementType { Room, Device }

internal record AccessibleElement(
    GodotNative.Node3D Node,
    AccessibleInfo Info,
    AccessibleElementType ElementType,
    string Id);
