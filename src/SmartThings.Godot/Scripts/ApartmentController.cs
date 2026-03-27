// =============================================================================
// ApartmentController.cs — First-person controller for 3D apartment exploration
// WASD/arrow movement, mouse look, touch controls for mobile
// =============================================================================

using GodotNative = Godot;

namespace SmartThings.Godot.Scripts;

/// <summary>
/// First-person player controller for exploring a 3D apartment.
/// Root node must be a CharacterBody3D with a Camera3D child at eye level.
///
/// Controls:
///   Desktop: WASD/Arrows to move, mouse to look, Escape to release cursor
///   Mobile:  Left half of screen = virtual joystick (move), Right half = look
/// </summary>
public partial class ApartmentController : GodotNative.CharacterBody3D
{
    // ── Exports ──────────────────────────────────────────────────────────────

    [GodotNative.Export] public float MoveSpeed { get; set; } = 4.0f;
    [GodotNative.Export] public float Acceleration { get; set; } = 10.0f;
    [GodotNative.Export] public float Deceleration { get; set; } = 12.0f;
    [GodotNative.Export] public float MouseSensitivity { get; set; } = 0.002f;
    [GodotNative.Export] public float TouchLookSensitivity { get; set; } = 0.004f;
    [GodotNative.Export] public float Gravity { get; set; } = 9.8f;

    // ── Internals ────────────────────────────────────────────────────────────

    private GodotNative.Camera3D? _camera;
    private float _cameraRotationX; // pitch (up/down)
    private float _cameraRotationY; // yaw (left/right)

    // Touch tracking
    private int _moveTouchIndex = -1;
    private GodotNative.Vector2 _moveTouchStart;
    private GodotNative.Vector2 _moveTouchCurrent;

    private int _lookTouchIndex = -1;
    private GodotNative.Vector2 _lookTouchPrevious;

    private const float TouchMoveDeadzone = 20.0f;
    private const float TouchMoveMaxRadius = 120.0f;
    private const float PitchClampDeg = 89.0f;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _camera = GetNode<GodotNative.Camera3D>("Camera3D");

        // Capture mouse on desktop
        GodotNative.Input.MouseMode = GodotNative.Input.MouseModeEnum.Captured;

        GodotNative.GD.Print("[ApartmentController] Ready — exploring apartment.");
    }

    public override void _UnhandledInput(GodotNative.InputEvent @event)
    {
        // ── Mouse look ──────────────────────────────────────────────────
        if (@event is GodotNative.InputEventMouseMotion mouseMotion
            && GodotNative.Input.MouseMode == GodotNative.Input.MouseModeEnum.Captured)
        {
            _cameraRotationY -= mouseMotion.Relative.X * MouseSensitivity;
            _cameraRotationX -= mouseMotion.Relative.Y * MouseSensitivity;
            _cameraRotationX = GodotNative.Mathf.Clamp(
                _cameraRotationX,
                GodotNative.Mathf.DegToRad(-PitchClampDeg),
                GodotNative.Mathf.DegToRad(PitchClampDeg));

            ApplyRotation();
        }

        // ── Escape to release mouse ─────────────────────────────────────
        if (@event is GodotNative.InputEventKey keyEvent
            && keyEvent.Pressed
            && keyEvent.Keycode == GodotNative.Key.Escape)
        {
            if (GodotNative.Input.MouseMode == GodotNative.Input.MouseModeEnum.Captured)
                GodotNative.Input.MouseMode = GodotNative.Input.MouseModeEnum.Visible;
            else
                GodotNative.Input.MouseMode = GodotNative.Input.MouseModeEnum.Captured;
        }

        // ── Touch input ─────────────────────────────────────────────────
        if (@event is GodotNative.InputEventScreenTouch screenTouch)
        {
            HandleScreenTouch(screenTouch);
        }

        if (@event is GodotNative.InputEventScreenDrag screenDrag)
        {
            HandleScreenDrag(screenDrag);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;

        // ── Gravity ─────────────────────────────────────────────────────
        var velocity = Velocity;
        if (!IsOnFloor())
        {
            velocity.Y -= Gravity * dt;
        }

        // ── Movement direction ──────────────────────────────────────────
        var inputDir = GetMovementInput();

        // Transform input to world-space based on camera yaw
        var forward = new GodotNative.Vector3(
            GodotNative.Mathf.Sin(_cameraRotationY), 0,
            GodotNative.Mathf.Cos(_cameraRotationY));
        var right = new GodotNative.Vector3(
            GodotNative.Mathf.Cos(_cameraRotationY), 0,
            -GodotNative.Mathf.Sin(_cameraRotationY));

        var desiredHorizontal = (forward * inputDir.Y + right * inputDir.X) * MoveSpeed;

        // Smooth acceleration / deceleration
        float accel = inputDir.LengthSquared() > 0.01f ? Acceleration : Deceleration;
        velocity.X = GodotNative.Mathf.MoveToward(velocity.X, desiredHorizontal.X, accel * dt);
        velocity.Z = GodotNative.Mathf.MoveToward(velocity.Z, desiredHorizontal.Z, accel * dt);

        Velocity = velocity;
        MoveAndSlide();
    }

    // ── Input helpers ────────────────────────────────────────────────────────

    private GodotNative.Vector2 GetMovementInput()
    {
        var input = GodotNative.Vector2.Zero;

        // Keyboard / action-based input
        if (GodotNative.Input.IsActionPressed("move_forward"))
            input.Y += 1.0f;
        if (GodotNative.Input.IsActionPressed("move_backward"))
            input.Y -= 1.0f;
        if (GodotNative.Input.IsActionPressed("move_left"))
            input.X -= 1.0f;
        if (GodotNative.Input.IsActionPressed("move_right"))
            input.X += 1.0f;

        // Also support raw arrow keys as fallback
        if (GodotNative.Input.IsKeyPressed(GodotNative.Key.Up))
            input.Y += 1.0f;
        if (GodotNative.Input.IsKeyPressed(GodotNative.Key.Down))
            input.Y -= 1.0f;
        if (GodotNative.Input.IsKeyPressed(GodotNative.Key.Left))
            input.X -= 1.0f;
        if (GodotNative.Input.IsKeyPressed(GodotNative.Key.Right))
            input.X += 1.0f;

        // Touch virtual joystick
        if (_moveTouchIndex >= 0)
        {
            var touchDelta = _moveTouchCurrent - _moveTouchStart;
            if (touchDelta.Length() > TouchMoveDeadzone)
            {
                var normalized = touchDelta / TouchMoveMaxRadius;
                normalized = normalized.LimitLength(1.0f);
                input.X += normalized.X;
                input.Y -= normalized.Y; // Screen Y is inverted
            }
        }

        return input.LimitLength(1.0f);
    }

    private void ApplyRotation()
    {
        Rotation = new GodotNative.Vector3(0, _cameraRotationY, 0);
        if (_camera != null)
        {
            _camera.Rotation = new GodotNative.Vector3(_cameraRotationX, 0, 0);
        }
    }

    // ── Touch handling ───────────────────────────────────────────────────────

    private void HandleScreenTouch(GodotNative.InputEventScreenTouch touch)
    {
        var screenMidX = GetViewport().GetVisibleRect().Size.X * 0.5f;

        if (touch.Pressed)
        {
            if (touch.Position.X < screenMidX && _moveTouchIndex < 0)
            {
                // Left side — movement
                _moveTouchIndex = touch.Index;
                _moveTouchStart = touch.Position;
                _moveTouchCurrent = touch.Position;
            }
            else if (touch.Position.X >= screenMidX && _lookTouchIndex < 0)
            {
                // Right side — look
                _lookTouchIndex = touch.Index;
                _lookTouchPrevious = touch.Position;
            }
        }
        else
        {
            // Released
            if (touch.Index == _moveTouchIndex)
            {
                _moveTouchIndex = -1;
            }
            else if (touch.Index == _lookTouchIndex)
            {
                _lookTouchIndex = -1;
            }
        }
    }

    private void HandleScreenDrag(GodotNative.InputEventScreenDrag drag)
    {
        if (drag.Index == _moveTouchIndex)
        {
            _moveTouchCurrent = drag.Position;
        }
        else if (drag.Index == _lookTouchIndex)
        {
            var delta = drag.Position - _lookTouchPrevious;
            _lookTouchPrevious = drag.Position;

            _cameraRotationY -= delta.X * TouchLookSensitivity;
            _cameraRotationX -= delta.Y * TouchLookSensitivity;
            _cameraRotationX = GodotNative.Mathf.Clamp(
                _cameraRotationX,
                GodotNative.Mathf.DegToRad(-PitchClampDeg),
                GodotNative.Mathf.DegToRad(PitchClampDeg));

            ApplyRotation();
        }
    }
}
