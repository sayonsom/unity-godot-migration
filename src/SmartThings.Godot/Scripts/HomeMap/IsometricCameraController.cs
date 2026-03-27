// =============================================================================
// IsometricCameraController.cs — Stable isometric camera for 3D Home Map
// Orthographic, constrained bounds, 1-finger pan, pinch zoom, 2-finger rotate
// =============================================================================

using GodotNative = Godot;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// Orthographic isometric camera for the 3D Home Map View.
/// 1-finger: pan. 2-finger: pinch zoom + twist rotate around Y axis.
/// Mouse: left-drag pan, scroll zoom, right-drag rotate.
/// </summary>
public partial class IsometricCameraController : GodotNative.Camera3D
{
    [GodotNative.Export] public float MinOrthoSize { get; set; } = 4.0f;
    [GodotNative.Export] public float MaxOrthoSize { get; set; } = 18.0f;
    [GodotNative.Export] public float PanSpeed { get; set; } = 0.005f;
    [GodotNative.Export] public float SmoothFactor { get; set; } = 8.0f;
    [GodotNative.Export] public float IsometricAngle { get; set; } = 55.0f;

    // ── Targets (smoothly interpolated) ─────────────────────────────────────

    private GodotNative.Vector3 _targetLookAt;
    private float _targetOrthoSize = 12.0f;
    private float _targetYaw;

    // ── Current (interpolated values) ───────────────────────────────────────

    private GodotNative.Vector3 _currentLookAt;
    private float _currentOrthoSize;
    private float _currentYaw;
    private float _pitchRad;

    // ── Bounds ──────────────────────────────────────────────────────────────

    private GodotNative.Vector3 _boundsMin;
    private GodotNative.Vector3 _boundsMax;
    private bool _hasBounds;

    // ── Touch state ─────────────────────────────────────────────────────────

    // Track each finger's current position
    private GodotNative.Vector2 _finger0Pos;
    private GodotNative.Vector2 _finger1Pos;
    private int _finger0Id = -1;
    private int _finger1Id = -1;
    private int _fingerCount;

    // Two-finger gesture previous state
    private float _prevPinchDist;
    private float _prevTwistAngle;

    // Tap detection
    private bool _hasMoved;
    private GodotNative.Vector2 _touchStartPos;

    private const float CameraDistance = 30.0f;
    private const float TapThreshold = 20.0f;

    [GodotNative.Signal] public delegate void ScreenTappedEventHandler(GodotNative.Vector2 screenPos);

    // ── Lifecycle ───────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal;
        Size = _targetOrthoSize;

        _targetYaw = GodotNative.Mathf.DegToRad(-30.0f);
        _pitchRad = GodotNative.Mathf.DegToRad(IsometricAngle);

        _currentOrthoSize = _targetOrthoSize;
        _currentLookAt = _targetLookAt;
        _currentYaw = _targetYaw;

        UpdateCameraTransform();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float t = 1.0f - GodotNative.Mathf.Exp(-SmoothFactor * dt);

        _currentOrthoSize = GodotNative.Mathf.Lerp(_currentOrthoSize, _targetOrthoSize, t);
        _currentLookAt = _currentLookAt.Lerp(_targetLookAt, t);
        _currentYaw = GodotNative.Mathf.LerpAngle(_currentYaw, _targetYaw, t);

        if (_hasBounds)
        {
            _currentLookAt.X = GodotNative.Mathf.Clamp(_currentLookAt.X, _boundsMin.X, _boundsMax.X);
            _currentLookAt.Z = GodotNative.Mathf.Clamp(_currentLookAt.Z, _boundsMin.Z, _boundsMax.Z);
        }

        Size = _currentOrthoSize;
        UpdateCameraTransform();
    }

    public override void _UnhandledInput(GodotNative.InputEvent @event)
    {
        // ── Mouse scroll zoom ──
        if (@event is GodotNative.InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == GodotNative.MouseButton.WheelUp)
                _targetOrthoSize = GodotNative.Mathf.Max(MinOrthoSize, _targetOrthoSize - 0.8f);
            else if (mb.ButtonIndex == GodotNative.MouseButton.WheelDown)
                _targetOrthoSize = GodotNative.Mathf.Min(MaxOrthoSize, _targetOrthoSize + 0.8f);
        }

        // ── Mouse drag ──
        if (@event is GodotNative.InputEventMouseMotion mm)
        {
            if ((mm.ButtonMask & GodotNative.MouseButtonMask.Left) != 0)
                ApplyPan(mm.Relative);
            if ((mm.ButtonMask & GodotNative.MouseButtonMask.Right) != 0)
                _targetYaw -= mm.Relative.X * 0.005f;
        }

        // ── Touch events ──
        if (@event is GodotNative.InputEventScreenTouch touch)
            OnTouch(touch);
        if (@event is GodotNative.InputEventScreenDrag drag)
            OnDrag(drag);
    }

    // ── Public API ──────────────────────────────────────────────────────────

    public void SetBounds(GodotNative.Vector3 min, GodotNative.Vector3 max)
    {
        float pad = 3.0f;
        _boundsMin = new GodotNative.Vector3(min.X - pad, 0, min.Z - pad);
        _boundsMax = new GodotNative.Vector3(max.X + pad, 0, max.Z + pad);
        _hasBounds = true;
    }

    public void FocusOn(GodotNative.Vector3 center, float radius = 0)
    {
        _targetLookAt = new GodotNative.Vector3(center.X, 0, center.Z);
        if (radius > 0)
            _targetOrthoSize = GodotNative.Mathf.Clamp(radius * 1.5f, MinOrthoSize, MaxOrthoSize);
    }

    public void ResetView()
    {
        if (_hasBounds)
        {
            _targetLookAt = (_boundsMin + _boundsMax) * 0.5f;
            float span = GodotNative.Mathf.Max(_boundsMax.X - _boundsMin.X, _boundsMax.Z - _boundsMin.Z);
            _targetOrthoSize = GodotNative.Mathf.Clamp(span * 0.6f, MinOrthoSize, MaxOrthoSize);
        }
        _targetYaw = GodotNative.Mathf.DegToRad(-30.0f);
    }

    // ── Touch handling ──────────────────────────────────────────────────────

    private void OnTouch(GodotNative.InputEventScreenTouch touch)
    {
        if (touch.Pressed)
        {
            // Finger down
            if (_finger0Id < 0)
            {
                _finger0Id = touch.Index;
                _finger0Pos = touch.Position;
                _fingerCount = 1;
                _hasMoved = false;
                _touchStartPos = touch.Position;
            }
            else if (_finger1Id < 0)
            {
                _finger1Id = touch.Index;
                _finger1Pos = touch.Position;
                _fingerCount = 2;
                // Initialize two-finger gesture state
                _prevPinchDist = _finger0Pos.DistanceTo(_finger1Pos);
                _prevTwistAngle = (_finger1Pos - _finger0Pos).Angle();
            }
        }
        else
        {
            // Finger up
            if (touch.Index == _finger0Id)
            {
                // If single tap (no drag, no second finger)
                if (_fingerCount == 1 && !_hasMoved &&
                    touch.Position.DistanceTo(_touchStartPos) < TapThreshold)
                {
                    EmitSignal(SignalName.ScreenTapped, touch.Position);
                }
                _finger0Id = -1;
                _fingerCount = _finger1Id >= 0 ? 1 : 0;

                // Promote finger1 to finger0 if still down
                if (_finger1Id >= 0)
                {
                    _finger0Id = _finger1Id;
                    _finger0Pos = _finger1Pos;
                    _finger1Id = -1;
                    _fingerCount = 1;
                }
            }
            else if (touch.Index == _finger1Id)
            {
                _finger1Id = -1;
                _fingerCount = _finger0Id >= 0 ? 1 : 0;
            }
        }
    }

    private void OnDrag(GodotNative.InputEventScreenDrag drag)
    {
        // Update stored position
        if (drag.Index == _finger0Id)
            _finger0Pos = drag.Position;
        else if (drag.Index == _finger1Id)
            _finger1Pos = drag.Position;
        else
            return;

        if (_fingerCount == 1 && drag.Index == _finger0Id)
        {
            // ── Single finger: PAN ──
            if (drag.Relative.Length() > 1.5f)
                _hasMoved = true;

            ApplyPan(drag.Relative);
            GetViewport().SetInputAsHandled();
        }
        else if (_fingerCount == 2 && _finger0Id >= 0 && _finger1Id >= 0)
        {
            // ── Two fingers: PINCH ZOOM + TWIST ROTATE ──
            _hasMoved = true;

            float newDist = _finger0Pos.DistanceTo(_finger1Pos);
            float newAngle = (_finger1Pos - _finger0Pos).Angle();

            // Pinch zoom
            if (_prevPinchDist > 10.0f) // Avoid division noise when fingers very close
            {
                float pinchDelta = newDist - _prevPinchDist;
                _targetOrthoSize = GodotNative.Mathf.Clamp(
                    _targetOrthoSize - pinchDelta * 0.015f,
                    MinOrthoSize, MaxOrthoSize);
            }

            // Twist rotate (Y axis)
            float angleDelta = AngleDifference(newAngle, _prevTwistAngle);
            _targetYaw += angleDelta;

            _prevPinchDist = newDist;
            _prevTwistAngle = newAngle;
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Internals ───────────────────────────────────────────────────────────

    private void ApplyPan(GodotNative.Vector2 screenDelta)
    {
        // Very gentle pan, scaled by zoom level
        float scale = PanSpeed * _currentOrthoSize / 10.0f;

        // Convert screen delta to world XZ using current yaw
        var right = new GodotNative.Vector3(
            GodotNative.Mathf.Cos(_currentYaw), 0,
            -GodotNative.Mathf.Sin(_currentYaw));
        var forward = new GodotNative.Vector3(
            GodotNative.Mathf.Sin(_currentYaw), 0,
            GodotNative.Mathf.Cos(_currentYaw));

        _targetLookAt -= right * screenDelta.X * scale;
        _targetLookAt -= forward * screenDelta.Y * scale;

        if (_hasBounds)
        {
            _targetLookAt.X = GodotNative.Mathf.Clamp(_targetLookAt.X, _boundsMin.X, _boundsMax.X);
            _targetLookAt.Z = GodotNative.Mathf.Clamp(_targetLookAt.Z, _boundsMin.Z, _boundsMax.Z);
        }
    }

    private void UpdateCameraTransform()
    {
        var offset = new GodotNative.Vector3(
            CameraDistance * GodotNative.Mathf.Cos(_pitchRad) * GodotNative.Mathf.Sin(_currentYaw),
            CameraDistance * GodotNative.Mathf.Sin(_pitchRad),
            CameraDistance * GodotNative.Mathf.Cos(_pitchRad) * GodotNative.Mathf.Cos(_currentYaw));

        GlobalPosition = _currentLookAt + offset;
        LookAt(_currentLookAt, GodotNative.Vector3.Up);
    }

    /// <summary>Shortest angular difference handling wrapping.</summary>
    private static float AngleDifference(float a, float b)
    {
        float diff = a - b;
        while (diff > GodotNative.Mathf.Pi) diff -= GodotNative.Mathf.Tau;
        while (diff < -GodotNative.Mathf.Pi) diff += GodotNative.Mathf.Tau;
        return diff;
    }
}
