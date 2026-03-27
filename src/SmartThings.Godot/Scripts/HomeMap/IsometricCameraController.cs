// =============================================================================
// IsometricCameraController.cs — Isometric camera with pan/zoom/rotate
// Touch: 1-finger pan, pinch zoom, 2-finger rotate
// Mouse: scroll zoom, middle-click pan, right-click rotate
// =============================================================================

using GodotNative = Godot;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// Isometric camera controller for the 3D Home Map View.
/// Provides smooth pan, zoom, and rotate with touch and mouse support.
/// </summary>
public partial class IsometricCameraController : GodotNative.Camera3D
{
    // ── Exports ──────────────────────────────────────────────────────────────

    [GodotNative.Export] public float MinZoom { get; set; } = 5.0f;
    [GodotNative.Export] public float MaxZoom { get; set; } = 30.0f;
    [GodotNative.Export] public float ZoomSpeed { get; set; } = 2.0f;
    [GodotNative.Export] public float PanSpeed { get; set; } = 0.02f;
    [GodotNative.Export] public float RotateSpeed { get; set; } = 0.005f;
    [GodotNative.Export] public float SmoothFactor { get; set; } = 8.0f;
    [GodotNative.Export] public float DefaultAngle { get; set; } = 45.0f;

    // ── State ────────────────────────────────────────────────────────────────

    private GodotNative.Vector3 _targetLookAt = GodotNative.Vector3.Zero;
    private float _targetDistance = 15.0f;
    private float _targetYaw = GodotNative.Mathf.DegToRad(45.0f);
    private float _targetPitch;

    private float _currentDistance;
    private float _currentYaw;
    private float _currentPitch;
    private GodotNative.Vector3 _currentLookAt;

    // Touch tracking
    private readonly Dictionary<int, GodotNative.Vector2> _touchPositions = new();
    private float _lastPinchDistance;
    private float _lastTwoFingerAngle;
    private bool _isPanning;
    private GodotNative.Vector2 _lastPanPosition;

    // Double-tap detection
    private float _lastTapTime;
    private GodotNative.Vector2 _lastTapPosition;
    private const float DoubleTapTime = 0.3f;
    private const float DoubleTapDistance = 30.0f;

    /// <summary>Fired when a room is tapped. Payload is room_id string.</summary>
    [GodotNative.Signal] public delegate void RoomTappedEventHandler(string roomId);

    /// <summary>Fired when a room is double-tapped for zoom-to-fit.</summary>
    [GodotNative.Signal] public delegate void RoomDoubleTappedEventHandler(string roomId);

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _targetPitch = GodotNative.Mathf.DegToRad(DefaultAngle);
        _currentDistance = _targetDistance;
        _currentYaw = _targetYaw;
        _currentPitch = _targetPitch;
        _currentLookAt = _targetLookAt;

        UpdateCameraPosition();
        GodotNative.GD.Print("[IsometricCamera] Ready — isometric view active.");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float lerp = 1.0f - GodotNative.Mathf.Exp(-SmoothFactor * dt);

        // Smooth interpolation toward targets
        _currentDistance = GodotNative.Mathf.Lerp(_currentDistance, _targetDistance, lerp);
        _currentYaw = GodotNative.Mathf.LerpAngle(_currentYaw, _targetYaw, lerp);
        _currentPitch = GodotNative.Mathf.Lerp(_currentPitch, _targetPitch, lerp);
        _currentLookAt = _currentLookAt.Lerp(_targetLookAt, lerp);

        UpdateCameraPosition();
    }

    public override void _UnhandledInput(GodotNative.InputEvent @event)
    {
        // ── Mouse scroll zoom ──────────────────────────────────────────
        if (@event is GodotNative.InputEventMouseButton mouseBtn)
        {
            if (mouseBtn.ButtonIndex == GodotNative.MouseButton.WheelUp)
                _targetDistance = GodotNative.Mathf.Max(MinZoom, _targetDistance - ZoomSpeed);
            else if (mouseBtn.ButtonIndex == GodotNative.MouseButton.WheelDown)
                _targetDistance = GodotNative.Mathf.Min(MaxZoom, _targetDistance + ZoomSpeed);
        }

        // ── Mouse drag (middle=pan, right=rotate) ─────────────────────
        if (@event is GodotNative.InputEventMouseMotion mouseMotion)
        {
            if ((mouseMotion.ButtonMask & GodotNative.MouseButtonMask.Middle) != 0)
            {
                Pan(mouseMotion.Relative);
            }
            else if ((mouseMotion.ButtonMask & GodotNative.MouseButtonMask.Right) != 0)
            {
                _targetYaw -= mouseMotion.Relative.X * RotateSpeed;
                _targetPitch = GodotNative.Mathf.Clamp(
                    _targetPitch - mouseMotion.Relative.Y * RotateSpeed,
                    GodotNative.Mathf.DegToRad(15.0f),
                    GodotNative.Mathf.DegToRad(85.0f));
            }
        }

        // ── Touch input ────────────────────────────────────────────────
        if (@event is GodotNative.InputEventScreenTouch touch)
            HandleTouch(touch);
        if (@event is GodotNative.InputEventScreenDrag drag)
            HandleDrag(drag);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Reset camera to default isometric view.</summary>
    public void ResetView()
    {
        _targetDistance = 15.0f;
        _targetYaw = GodotNative.Mathf.DegToRad(45.0f);
        _targetPitch = GodotNative.Mathf.DegToRad(DefaultAngle);
        _targetLookAt = GodotNative.Vector3.Zero;
    }

    /// <summary>Zoom to fit a specific world-space bounding box.</summary>
    public void ZoomToFit(GodotNative.Vector3 center, float radius)
    {
        _targetLookAt = center;
        _targetDistance = GodotNative.Mathf.Clamp(radius * 2.5f, MinZoom, MaxZoom);
    }

    // ── Touch handling ───────────────────────────────────────────────────────

    private void HandleTouch(GodotNative.InputEventScreenTouch touch)
    {
        if (touch.Pressed)
        {
            _touchPositions[touch.Index] = touch.Position;

            if (_touchPositions.Count == 1)
            {
                _isPanning = true;
                _lastPanPosition = touch.Position;

                // Double-tap detection
                float now = (float)GodotNative.Time.GetTicksMsec() / 1000.0f;
                if (now - _lastTapTime < DoubleTapTime
                    && touch.Position.DistanceTo(_lastTapPosition) < DoubleTapDistance)
                {
                    TryRaycastRoom(touch.Position, isDoubleTap: true);
                }
                _lastTapTime = now;
                _lastTapPosition = touch.Position;
            }
            else if (_touchPositions.Count == 2)
            {
                _isPanning = false;
                var positions = new List<GodotNative.Vector2>(_touchPositions.Values);
                _lastPinchDistance = positions[0].DistanceTo(positions[1]);
                _lastTwoFingerAngle = (positions[1] - positions[0]).Angle();
            }
        }
        else
        {
            if (_touchPositions.Count == 1 && _isPanning)
            {
                // Single tap — raycast for room selection
                TryRaycastRoom(touch.Position, isDoubleTap: false);
            }
            _touchPositions.Remove(touch.Index);
            if (_touchPositions.Count < 2) _isPanning = _touchPositions.Count == 1;
        }
    }

    private void HandleDrag(GodotNative.InputEventScreenDrag drag)
    {
        _touchPositions[drag.Index] = drag.Position;

        if (_touchPositions.Count == 1 && _isPanning)
        {
            // Single finger pan
            Pan(drag.Relative);
        }
        else if (_touchPositions.Count == 2)
        {
            var positions = new List<GodotNative.Vector2>(_touchPositions.Values);
            float newDist = positions[0].DistanceTo(positions[1]);
            float newAngle = (positions[1] - positions[0]).Angle();

            // Pinch zoom
            float pinchDelta = newDist - _lastPinchDistance;
            _targetDistance = GodotNative.Mathf.Clamp(
                _targetDistance - pinchDelta * 0.05f, MinZoom, MaxZoom);
            _lastPinchDistance = newDist;

            // Two-finger rotate
            float angleDelta = newAngle - _lastTwoFingerAngle;
            _targetYaw += angleDelta;
            _lastTwoFingerAngle = newAngle;
        }
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private void Pan(GodotNative.Vector2 delta)
    {
        // Convert screen delta to world-space pan
        float panScale = PanSpeed * _currentDistance;
        var right = new GodotNative.Vector3(
            GodotNative.Mathf.Cos(_currentYaw), 0,
            -GodotNative.Mathf.Sin(_currentYaw));
        var forward = new GodotNative.Vector3(
            GodotNative.Mathf.Sin(_currentYaw), 0,
            GodotNative.Mathf.Cos(_currentYaw));

        _targetLookAt -= right * delta.X * panScale;
        _targetLookAt -= forward * delta.Y * panScale;
    }

    private void UpdateCameraPosition()
    {
        // Spherical coordinates around look-at point
        var offset = new GodotNative.Vector3(
            _currentDistance * GodotNative.Mathf.Sin(_currentPitch) * GodotNative.Mathf.Sin(_currentYaw),
            _currentDistance * GodotNative.Mathf.Cos(_currentPitch),
            _currentDistance * GodotNative.Mathf.Sin(_currentPitch) * GodotNative.Mathf.Cos(_currentYaw));

        GlobalPosition = _currentLookAt + offset;
        LookAt(_currentLookAt, GodotNative.Vector3.Up);
    }

    private void TryRaycastRoom(GodotNative.Vector2 screenPos, bool isDoubleTap)
    {
        var from = ProjectRayOrigin(screenPos);
        var to = from + ProjectRayNormal(screenPos) * 100.0f;

        var spaceState = GetWorld3D().DirectSpaceState;
        var query = GodotNative.PhysicsRayQueryParameters3D.Create(from, to);
        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var collider = result["collider"].AsGodotObject();
            if (collider is GodotNative.Node3D node && node.HasMeta("room_id"))
            {
                string roomId = node.GetMeta("room_id").AsString();
                if (isDoubleTap)
                    EmitSignal(SignalName.RoomDoubleTapped, roomId);
                else
                    EmitSignal(SignalName.RoomTapped, roomId);
            }
        }
    }
}
