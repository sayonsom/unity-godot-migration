// =============================================================================
// IInputService.cs — Engine-agnostic input abstraction
// Covers touch, keyboard, mouse, and gamepad input
// =============================================================================

namespace SmartThings.Abstraction.Interfaces;

/// <summary>
/// Abstracts input handling across engines. Both Unity (Input System) and
/// Godot (InputEvent) have fundamentally different input models — this
/// normalizes them into an action-based system.
/// </summary>
public interface IInputService
{
    // --- Action-Based Input (preferred) ---

    /// <summary>Register a named input action (e.g., "select", "back", "zoom").</summary>
    void RegisterAction(string actionName, InputBinding[] bindings);

    /// <summary>Check if an action was just pressed this frame.</summary>
    bool IsActionJustPressed(string actionName);

    /// <summary>Check if an action is currently held.</summary>
    bool IsActionPressed(string actionName);

    /// <summary>Check if an action was just released this frame.</summary>
    bool IsActionJustReleased(string actionName);

    /// <summary>Get analog strength of an action (0.0 to 1.0).</summary>
    float GetActionStrength(string actionName);

    // --- Pointer / Touch ---

    /// <summary>Get current pointer (mouse/touch) position in viewport coordinates.</summary>
    Vector2 GetPointerPosition();

    /// <summary>Perform a raycast from screen position into 3D scene.</summary>
    RaycastResult? Raycast(Vector2 screenPosition, float maxDistance = 1000f, uint collisionMask = uint.MaxValue);

    // --- Events ---

    /// <summary>Fired when any input action is triggered.</summary>
    event Action<InputActionEvent>? OnInputAction;

    /// <summary>Fired on touch/click events for UI hit testing.</summary>
    event Action<PointerEvent>? OnPointerEvent;
}

public record InputBinding(
    InputDeviceType Device,
    string Key  // e.g., "Space", "MouseLeft", "TouchPrimary", "JoyA"
);

public enum InputDeviceType
{
    Keyboard,
    Mouse,
    Touch,
    Gamepad
}

public record InputActionEvent(
    string ActionName,
    InputActionPhase Phase,
    float Strength
);

public enum InputActionPhase { Started, Performed, Canceled }

public record PointerEvent(
    Vector2 Position,
    PointerEventType Type,
    int PointerId = 0
);

public enum PointerEventType
{
    Down,
    Up,
    Move,
    Cancel
}

public record RaycastResult(
    Vector3 Point,
    Vector3 Normal,
    INodeHandle? HitNode,
    float Distance
);
