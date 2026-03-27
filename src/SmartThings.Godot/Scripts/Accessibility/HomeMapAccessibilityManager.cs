// =============================================================================
// HomeMapAccessibilityManager.cs — TalkBack/screen reader support for Home Map
// Labels all 3D elements, manages focus ring, announces state changes
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.Accessibility;

/// <summary>
/// Manages accessibility for the 3D Home Map scene:
///   - Registers all rooms and device pins as accessible elements
///   - Provides sequential focus navigation (swipe-based on Android TalkBack)
///   - Shows a visible focus ring around the focused element
///   - Announces room info and device status on focus
///   - Handles "Explore by Touch" for TalkBack users
///
/// This is the bridge between the 3D scene and platform screen readers.
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

    // Touch swipe gesture detection (for phone a11y without TalkBack)
    private GodotNative.Vector2 _touchStartPos;
    private bool _touchActive;
    private double _touchStartTime;
    private const float SwipeThreshold = 80f;  // minimum pixels for a swipe
    private const float SwipeMaxTime = 0.5f;   // seconds — fast flick only
    private const float DoubleTapMaxTime = 0.4f;
    private double _lastTapTime;
    private int _tapCount;

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

        // Announce scene on load
        _a11y.Announce(
            $"Home Map View. {home.Name} with {home.Rooms.Count} rooms and {home.Devices.Count} devices. " +
            "Swipe right to navigate between rooms and devices.",
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

        // Set metadata for TalkBack
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

    /// <summary>Navigate to the next accessible element (TalkBack swipe-right).</summary>
    public void FocusNext()
    {
        if (_elements.Count == 0) return;
        _focusIndex = (_focusIndex + 1) % _elements.Count;
        ApplyFocus();
    }

    /// <summary>Navigate to the previous accessible element (TalkBack swipe-left).</summary>
    public void FocusPrevious()
    {
        if (_elements.Count == 0) return;
        _focusIndex = (_focusIndex - 1 + _elements.Count) % _elements.Count;
        ApplyFocus();
    }

    /// <summary>Activate the currently focused element (TalkBack double-tap).</summary>
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

    /// <summary>Focus a specific element by its ID.</summary>
    public void FocusElement(string id)
    {
        var idx = _elements.FindIndex(e => e.Id == id);
        if (idx >= 0)
        {
            _focusIndex = idx;
            ApplyFocus();
        }
    }

    /// <summary>Announce a device state change to the screen reader.</summary>
    public void AnnounceDeviceChange(SmartDevice device, string change)
    {
        _a11y?.Announce($"{device.Label}: {change}", AnnouncePriority.Normal);

        // Update the registered element info
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

    // ── Input handling for accessibility gestures ────────────────────────────

    public override void _UnhandledInput(GodotNative.InputEvent @event)
    {
        // Keyboard navigation for accessibility testing
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

        // Touch swipe gestures for phone accessibility navigation
        // Swipe right → next, Swipe left → previous, Double-tap → activate
        if (@event is GodotNative.InputEventScreenTouch touch)
        {
            if (touch.Pressed)
            {
                _touchStartPos = touch.Position;
                _touchActive = true;
                _touchStartTime = GodotNative.Time.GetTicksMsec() / 1000.0;
            }
            else if (_touchActive)
            {
                _touchActive = false;
                var elapsed = GodotNative.Time.GetTicksMsec() / 1000.0 - _touchStartTime;
                var delta = touch.Position - _touchStartPos;
                var absX = MathF.Abs(delta.X);
                var absY = MathF.Abs(delta.Y);

                if (absX > SwipeThreshold && absX > absY * 1.5f && elapsed < SwipeMaxTime)
                {
                    // Horizontal swipe detected
                    if (delta.X > 0)
                    {
                        FocusNext();
                        GodotNative.GD.Print("[A11y] Swipe right → next");
                    }
                    else
                    {
                        FocusPrevious();
                        GodotNative.GD.Print("[A11y] Swipe left → previous");
                    }
                    GetViewport().SetInputAsHandled();
                }
                else if (absX < 30 && absY < 30 && elapsed < 0.3)
                {
                    // Tap detected — check for double-tap
                    var now = GodotNative.Time.GetTicksMsec() / 1000.0;
                    if (now - _lastTapTime < DoubleTapMaxTime)
                    {
                        _tapCount++;
                        if (_tapCount >= 2)
                        {
                            ActivateFocused();
                            _tapCount = 0;
                            GodotNative.GD.Print("[A11y] Double-tap → activate");
                            GetViewport().SetInputAsHandled();
                        }
                    }
                    else
                    {
                        _tapCount = 1;
                    }
                    _lastTapTime = now;
                }
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

        // Create a torus-like ring using a TorusMesh
        _focusRingMesh = new GodotNative.MeshInstance3D();
        var torusMesh = new GodotNative.TorusMesh();
        torusMesh.InnerRadius = 0.8f;
        torusMesh.OuterRadius = 1.0f;
        torusMesh.Rings = 16;
        torusMesh.RingSegments = 16;
        _focusRingMesh.Mesh = torusMesh;

        // Bright orange unlit material for focus ring
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

        // Gentle rotation animation on focus ring
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

        // Move focus ring to element position
        ShowFocusRing(element.Node.GlobalPosition);

        // Announce to screen reader
        var announcement = element.ElementType == AccessibleElementType.Room
            ? $"Room: {element.Info.Name}. {element.Info.Description}"
            : $"Device: {element.Info.Name}. {element.Info.Description}";

        _a11y?.Announce(announcement, AnnouncePriority.Normal);

        GodotNative.GD.Print($"[A11y] Focus: {announcement}");
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
