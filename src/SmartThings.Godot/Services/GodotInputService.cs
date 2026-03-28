// =============================================================================
// GodotInputService.cs — Godot 4.5 implementation of IInputService
// Wraps Godot InputMap, Input, and PhysicsDirectSpaceState3D
// =============================================================================

using SmartThings.Abstraction;
using SmartThings.Abstraction.Interfaces;
using GodotNative = Godot;

namespace SmartThings.Godot.Services;

/// <summary>
/// Godot backend for IInputService.
///
/// Key mappings:
///   Unity Input.GetKeyDown()     → Godot Input.IsActionJustPressed()
///   Unity Input.GetAxis()        → Godot Input.GetActionStrength()
///   Unity Physics.Raycast()      → Godot PhysicsDirectSpaceState3D.IntersectRay()
///   Unity InputSystem             → Godot InputMap (action-based, configured at runtime)
///
/// Android touch: Godot natively handles touch as InputEventScreenTouch/Drag.
/// These are mapped to pointer events automatically.
/// </summary>
public partial class GodotInputService : GodotNative.Node, IInputService
{
    private readonly Dictionary<string, InputBinding[]> _registeredActions = new();

    public event Action<InputActionEvent>? OnInputAction;
    public event Action<PointerEvent>? OnPointerEvent;

    // --- Action Registration ---

    public void RegisterAction(string actionName, InputBinding[] bindings)
    {
        // Remove existing if re-registering
        if (GodotNative.InputMap.HasAction(actionName))
        {
            GodotNative.InputMap.EraseAction(actionName);
        }

        GodotNative.InputMap.AddAction(actionName);

        foreach (var binding in bindings)
        {
            var inputEvent = CreateInputEvent(binding);
            if (inputEvent != null)
            {
                GodotNative.InputMap.ActionAddEvent(actionName, inputEvent);
            }
        }

        _registeredActions[actionName] = bindings;
    }

    // --- Action Queries ---

    public bool IsActionJustPressed(string actionName) =>
        GodotNative.Input.IsActionJustPressed(actionName);

    public bool IsActionPressed(string actionName) =>
        GodotNative.Input.IsActionPressed(actionName);

    public bool IsActionJustReleased(string actionName) =>
        GodotNative.Input.IsActionJustReleased(actionName);

    public float GetActionStrength(string actionName) =>
        GodotNative.Input.GetActionStrength(actionName);

    // --- Pointer ---

    public Vector2 GetPointerPosition()
    {
        var pos = GetViewport().GetMousePosition();
        return new Vector2(pos.X, pos.Y);
    }

    // --- Raycasting ---

    public RaycastResult? Raycast(Vector2 screenPosition, float maxDistance = 1000f, uint collisionMask = uint.MaxValue)
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null) return null;

        var from = camera.ProjectRayOrigin(new GodotNative.Vector2(screenPosition.X, screenPosition.Y));
        var to = from + camera.ProjectRayNormal(new GodotNative.Vector2(screenPosition.X, screenPosition.Y)) * maxDistance;

        var spaceState = camera.GetWorld3D().DirectSpaceState;
        var query = GodotNative.PhysicsRayQueryParameters3D.Create(from, to, collisionMask);
        var result = spaceState.IntersectRay(query);

        if (result == null || result.Count == 0) return null;

        var point = (GodotNative.Vector3)result["position"];
        var normal = (GodotNative.Vector3)result["normal"];
        var collider = result["collider"].AsGodotObject() as GodotNative.Node3D;

        INodeHandle? hitNode = collider != null ? new GodotNodeHandle(collider) : null;
        var distance = from.DistanceTo(point);

        return new RaycastResult(
            new Vector3(point.X, point.Y, point.Z),
            new Vector3(normal.X, normal.Y, normal.Z),
            hitNode,
            distance
        );
    }

    // --- Input Event Processing ---

    public override void _UnhandledInput(GodotNative.InputEvent @event)
    {
        // Process registered actions
        foreach (var actionName in _registeredActions.Keys)
        {
            if (@event.IsActionPressed(actionName))
            {
                OnInputAction?.Invoke(new InputActionEvent(
                    actionName, InputActionPhase.Started,
                    @event.IsAction(actionName) ? @event.GetActionStrength(actionName) : 1f));
            }
            else if (@event.IsActionReleased(actionName))
            {
                OnInputAction?.Invoke(new InputActionEvent(
                    actionName, InputActionPhase.Canceled, 0f));
            }
        }

        // Process pointer/touch events
        switch (@event)
        {
            case GodotNative.InputEventMouseButton mouseBtn:
                OnPointerEvent?.Invoke(new PointerEvent(
                    new Vector2(mouseBtn.Position.X, mouseBtn.Position.Y),
                    mouseBtn.Pressed ? PointerEventType.Down : PointerEventType.Up));
                break;

            case GodotNative.InputEventMouseMotion mouseMotion:
                OnPointerEvent?.Invoke(new PointerEvent(
                    new Vector2(mouseMotion.Position.X, mouseMotion.Position.Y),
                    PointerEventType.Move));
                break;

            case GodotNative.InputEventScreenTouch touch:
                OnPointerEvent?.Invoke(new PointerEvent(
                    new Vector2(touch.Position.X, touch.Position.Y),
                    touch.Pressed ? PointerEventType.Down : PointerEventType.Up,
                    touch.Index));
                break;

            case GodotNative.InputEventScreenDrag drag:
                OnPointerEvent?.Invoke(new PointerEvent(
                    new Vector2(drag.Position.X, drag.Position.Y),
                    PointerEventType.Move,
                    drag.Index));
                break;
        }
    }

    // --- Key Mapping ---

    internal static GodotNative.InputEvent? CreateInputEvent(InputBinding binding)
    {
        return binding.Device switch
        {
            InputDeviceType.Keyboard => CreateKeyEvent(binding.Key),
            InputDeviceType.Mouse => CreateMouseEvent(binding.Key),
            InputDeviceType.Touch => CreateTouchEvent(binding.Key),
            InputDeviceType.Gamepad => CreateJoyEvent(binding.Key),
            _ => null
        };
    }

    private static GodotNative.InputEvent? CreateKeyEvent(string key)
    {
        var keyEvent = new GodotNative.InputEventKey();
        if (TryParseKey(key, out var godotKey))
        {
            keyEvent.Keycode = godotKey;
            return keyEvent;
        }
        return null;
    }

    private static GodotNative.InputEvent? CreateMouseEvent(string key)
    {
        var mouseEvent = new GodotNative.InputEventMouseButton();
        mouseEvent.ButtonIndex = key.ToLowerInvariant() switch
        {
            "mouseleft" or "left" => GodotNative.MouseButton.Left,
            "mouseright" or "right" => GodotNative.MouseButton.Right,
            "mousemiddle" or "middle" => GodotNative.MouseButton.Middle,
            "wheelup" => GodotNative.MouseButton.WheelUp,
            "wheeldown" => GodotNative.MouseButton.WheelDown,
            _ => GodotNative.MouseButton.Left
        };
        mouseEvent.Pressed = true;
        return mouseEvent;
    }

    private static GodotNative.InputEvent? CreateTouchEvent(string key)
    {
        // Touch events are handled natively by Godot on Android
        // Register as screen touch with index
        var touchEvent = new GodotNative.InputEventScreenTouch();
        touchEvent.Index = key.ToLowerInvariant() switch
        {
            "touchprimary" or "touch0" => 0,
            "touchsecondary" or "touch1" => 1,
            _ => int.TryParse(key, out var idx) ? idx : 0
        };
        touchEvent.Pressed = true;
        return touchEvent;
    }

    private static GodotNative.InputEvent? CreateJoyEvent(string key)
    {
        var joyEvent = new GodotNative.InputEventJoypadButton();
        joyEvent.ButtonIndex = key.ToLowerInvariant() switch
        {
            "joya" or "a" => GodotNative.JoyButton.A,
            "joyb" or "b" => GodotNative.JoyButton.B,
            "joyx" or "x" => GodotNative.JoyButton.X,
            "joyy" or "y" => GodotNative.JoyButton.Y,
            "start" => GodotNative.JoyButton.Start,
            "back" or "select" => GodotNative.JoyButton.Back,
            "leftshoulder" or "lb" => GodotNative.JoyButton.LeftShoulder,
            "rightshoulder" or "rb" => GodotNative.JoyButton.RightShoulder,
            _ => GodotNative.JoyButton.A
        };
        joyEvent.Pressed = true;
        return joyEvent;
    }

    internal static bool TryParseKey(string key, out GodotNative.Key godotKey)
    {
        // Map common key names to Godot Key enum
        godotKey = key.ToLowerInvariant() switch
        {
            "space" => GodotNative.Key.Space,
            "enter" or "return" => GodotNative.Key.Enter,
            "escape" or "esc" => GodotNative.Key.Escape,
            "tab" => GodotNative.Key.Tab,
            "backspace" => GodotNative.Key.Backspace,
            "delete" or "del" => GodotNative.Key.Delete,
            "up" => GodotNative.Key.Up,
            "down" => GodotNative.Key.Down,
            "left" => GodotNative.Key.Left,
            "right" => GodotNative.Key.Right,
            "shift" => GodotNative.Key.Shift,
            "ctrl" or "control" => GodotNative.Key.Ctrl,
            "alt" => GodotNative.Key.Alt,
            "f1" => GodotNative.Key.F1,
            "f2" => GodotNative.Key.F2,
            "f3" => GodotNative.Key.F3,
            "f4" => GodotNative.Key.F4,
            "f5" => GodotNative.Key.F5,
            "f6" => GodotNative.Key.F6,
            "f7" => GodotNative.Key.F7,
            "f8" => GodotNative.Key.F8,
            "f9" => GodotNative.Key.F9,
            "f10" => GodotNative.Key.F10,
            "f11" => GodotNative.Key.F11,
            "f12" => GodotNative.Key.F12,
            _ => TryParseSingleChar(key)
        };

        return godotKey != GodotNative.Key.None;
    }

    private static GodotNative.Key TryParseSingleChar(string key)
    {
        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if (c >= 'A' && c <= 'Z')
                return (GodotNative.Key)c;
            if (c >= '0' && c <= '9')
                return (GodotNative.Key)c;
        }
        return GodotNative.Key.None;
    }
}
