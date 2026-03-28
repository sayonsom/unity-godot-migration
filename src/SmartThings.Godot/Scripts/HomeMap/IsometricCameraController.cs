// =============================================================================
// IsometricCameraController.cs — Smooth isometric camera for 3D Home Map
// Designed to match the Unity SmartThings experience: buttery pinch-zoom,
// gentle pan with momentum, controlled rotate. Feels like a premium map app.
// =============================================================================

using GodotNative = Godot;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// Orthographic isometric camera matching the Unity SmartThings touch feel:
///   - 1 finger:  pan with momentum (deceleration after release)
///   - 2 fingers: pinch zoom only (no accidental rotate)
///                twist rotate only when angle > 15° threshold
///   - Tap:       room/device selection (no drag → emit signal)
///   - Mouse:     left-drag pan, scroll zoom, right-drag rotate
///
/// Key design decisions vs previous version:
///   1. Zoom uses ratio-based scaling (not pixel delta) for consistent feel
///   2. Rotate has a dead-zone — won't fire during normal pinch
///   3. Momentum on pan release for natural feel
///   4. Lower smooth factor (5.0 vs 8.0) for visible, pleasant easing
///   5. Minimum gesture distance before pan starts (avoids jitter on tap)
/// </summary>
public partial class IsometricCameraController : GodotNative.Camera3D
{
    // ── Exports ──────────────────────────────────────────────────────────────

    [GodotNative.Export] public float MinOrthoSize { get; set; } = 5.0f;
    [GodotNative.Export] public float MaxOrthoSize { get; set; } = 20.0f;
    [GodotNative.Export] public float PanSpeed { get; set; } = 0.004f;
    [GodotNative.Export] public float SmoothFactor { get; set; } = 5.0f;
    [GodotNative.Export] public float IsometricAngle { get; set; } = 55.0f;
    [GodotNative.Export] public float MomentumDecay { get; set; } = 0.92f;

    // ── Smooth targets ───────────────────────────────────────────────────────

    private GodotNative.Vector3 _targetLookAt;
    private float _targetOrthoSize = 12.0f;
    private float _targetYaw;

    // ── Current interpolated values ──────────────────────────────────────────

    private GodotNative.Vector3 _currentLookAt;
    private float _currentOrthoSize;
    private float _currentYaw;
    private float _pitchRad;

    // ── Pan momentum ─────────────────────────────────────────────────────────

    private GodotNative.Vector3 _panVelocity;
    private bool _isPanning;

    // ── Bounds ────────────────────────────────────────────────────────────────

    private GodotNative.Vector3 _boundsMin;
    private GodotNative.Vector3 _boundsMax;
    private bool _hasBounds;

    // ── Touch state ──────────────────────────────────────────────────────────

    private GodotNative.Vector2 _finger0Pos;
    private GodotNative.Vector2 _finger1Pos;
    private int _finger0Id = -1;
    private int _finger1Id = -1;
    private int _fingerCount;

    // Two-finger gesture tracking
    private float _prevPinchDist;
    private float _prevTwistAngle;
    private float _initialPinchDist;      // distance when 2nd finger landed
    private float _initialTwistAngle;     // angle when 2nd finger landed
    private bool _zoomLocked;             // true once pinch confirmed
    private bool _rotateLocked;           // true once twist confirmed
    private float _accumulatedPinchDelta; // total pinch movement since gesture start
    private float _accumulatedTwistDelta; // total twist since gesture start

    // Thresholds for gesture discrimination
    private const float PinchActivateThreshold = 20.0f;   // px movement before zoom starts
    private const float TwistActivateThreshold = 0.22f;   // ~12.5° before rotate starts
    private const float MinPinchDistance = 40.0f;          // ignore when fingers very close

    // Tap detection
    private bool _hasMoved;
    private GodotNative.Vector2 _touchStartPos;
    private double _touchStartTime;
    private GodotNative.Vector2 _lastPanDelta;  // for momentum

    private const float CameraDistance = 30.0f;
    private const float TapThreshold = 15.0f;
    private const float TapMaxTime = 0.35f;       // seconds
    private const float PanDeadZone = 4.0f;        // px before pan starts

    [GodotNative.Signal] public delegate void ScreenTappedEventHandler(GodotNative.Vector2 screenPos);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

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

        // Smooth interpolation with gentler factor than before
        float t = 1.0f - GodotNative.Mathf.Exp(-SmoothFactor * dt);

        _currentOrthoSize = GodotNative.Mathf.Lerp(_currentOrthoSize, _targetOrthoSize, t);
        _currentLookAt = _currentLookAt.Lerp(_targetLookAt, t);
        _currentYaw = GodotNative.Mathf.LerpAngle(_currentYaw, _targetYaw, t);

        // Apply momentum when not actively panning
        if (!_isPanning && _panVelocity.LengthSquared() > 0.0001f)
        {
            _targetLookAt += _panVelocity * dt * 60.0f; // normalize to ~60fps
            _panVelocity *= MomentumDecay;

            if (_panVelocity.LengthSquared() < 0.00001f)
                _panVelocity = GodotNative.Vector3.Zero;
        }

        // Clamp to bounds
        if (_hasBounds)
        {
            _targetLookAt.X = GodotNative.Mathf.Clamp(_targetLookAt.X, _boundsMin.X, _boundsMax.X);
            _targetLookAt.Z = GodotNative.Mathf.Clamp(_targetLookAt.Z, _boundsMin.Z, _boundsMax.Z);
            _currentLookAt.X = GodotNative.Mathf.Clamp(_currentLookAt.X, _boundsMin.X, _boundsMax.X);
            _currentLookAt.Z = GodotNative.Mathf.Clamp(_currentLookAt.Z, _boundsMin.Z, _boundsMax.Z);
        }

        Size = _currentOrthoSize;
        UpdateCameraTransform();
    }

    public override void _UnhandledInput(GodotNative.InputEvent @event)
    {
        // ── Mouse scroll zoom (desktop testing) ──
        if (@event is GodotNative.InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == GodotNative.MouseButton.WheelUp)
                _targetOrthoSize = GodotNative.Mathf.Max(MinOrthoSize, _targetOrthoSize - 0.6f);
            else if (mb.ButtonIndex == GodotNative.MouseButton.WheelDown)
                _targetOrthoSize = GodotNative.Mathf.Min(MaxOrthoSize, _targetOrthoSize + 0.6f);
        }

        // ── Mouse drag (desktop testing) ──
        if (@event is GodotNative.InputEventMouseMotion mm)
        {
            if ((mm.ButtonMask & GodotNative.MouseButtonMask.Left) != 0)
                ApplyPan(mm.Relative);
            if ((mm.ButtonMask & GodotNative.MouseButtonMask.Right) != 0)
                _targetYaw -= mm.Relative.X * 0.004f;
        }

        // ── Touch events ──
        if (@event is GodotNative.InputEventScreenTouch touch)
            OnTouch(touch);
        if (@event is GodotNative.InputEventScreenDrag drag)
            OnDrag(drag);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetBounds(GodotNative.Vector3 min, GodotNative.Vector3 max)
    {
        float pad = 4.0f;
        _boundsMin = new GodotNative.Vector3(min.X - pad, 0, min.Z - pad);
        _boundsMax = new GodotNative.Vector3(max.X + pad, 0, max.Z + pad);
        _hasBounds = true;
    }

    public void FocusOn(GodotNative.Vector3 center, float radius = 0)
    {
        _targetLookAt = new GodotNative.Vector3(center.X, 0, center.Z);
        if (radius > 0)
            _targetOrthoSize = GodotNative.Mathf.Clamp(radius * 1.5f, MinOrthoSize, MaxOrthoSize);
        _panVelocity = GodotNative.Vector3.Zero; // stop momentum
    }

    public void ResetView()
    {
        if (_hasBounds)
        {
            _targetLookAt = (_boundsMin + _boundsMax) * 0.5f;
            float span = GodotNative.Mathf.Max(
                _boundsMax.X - _boundsMin.X,
                _boundsMax.Z - _boundsMin.Z);
            _targetOrthoSize = GodotNative.Mathf.Clamp(span * 0.6f, MinOrthoSize, MaxOrthoSize);
        }
        _targetYaw = GodotNative.Mathf.DegToRad(-30.0f);
        _panVelocity = GodotNative.Vector3.Zero;
    }

    // ── Touch handling ────────────────────────────────────────────────────────

    private void OnTouch(GodotNative.InputEventScreenTouch touch)
    {
        if (touch.Pressed)
        {
            if (_finger0Id < 0)
            {
                // First finger down
                _finger0Id = touch.Index;
                _finger0Pos = touch.Position;
                _fingerCount = 1;
                _hasMoved = false;
                _isPanning = true;
                _panVelocity = GodotNative.Vector3.Zero; // kill momentum on new touch
                _touchStartPos = touch.Position;
                _touchStartTime = GodotNative.Time.GetTicksMsec() / 1000.0;
                _lastPanDelta = GodotNative.Vector2.Zero;
            }
            else if (_finger1Id < 0)
            {
                // Second finger down — start two-finger gesture
                _finger1Id = touch.Index;
                _finger1Pos = touch.Position;
                _fingerCount = 2;
                _hasMoved = true; // two fingers = not a tap

                // Record initial gesture state
                _initialPinchDist = _finger0Pos.DistanceTo(_finger1Pos);
                _initialTwistAngle = (_finger1Pos - _finger0Pos).Angle();
                _prevPinchDist = _initialPinchDist;
                _prevTwistAngle = _initialTwistAngle;

                // Reset gesture locks — neither confirmed yet
                _zoomLocked = false;
                _rotateLocked = false;
                _accumulatedPinchDelta = 0;
                _accumulatedTwistDelta = 0;
            }
        }
        else
        {
            // Finger up
            if (touch.Index == _finger0Id)
            {
                // Check for tap: no drag, quick release
                double elapsed = GodotNative.Time.GetTicksMsec() / 1000.0 - _touchStartTime;
                if (_fingerCount == 1 && !_hasMoved &&
                    touch.Position.DistanceTo(_touchStartPos) < TapThreshold &&
                    elapsed < TapMaxTime)
                {
                    EmitSignal(SignalName.ScreenTapped, touch.Position);
                }

                _finger0Id = -1;
                _isPanning = false;

                // Release momentum from last pan delta
                if (_fingerCount == 1 && _hasMoved)
                    _panVelocity = ScreenDeltaToWorld(_lastPanDelta) * 0.5f;

                // Promote finger1 → finger0
                if (_finger1Id >= 0)
                {
                    _finger0Id = _finger1Id;
                    _finger0Pos = _finger1Pos;
                    _finger1Id = -1;
                    _fingerCount = 1;
                    _isPanning = true;
                }
                else
                {
                    _fingerCount = 0;
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
            // ── Single finger: PAN with dead zone ──
            float totalMoved = drag.Position.DistanceTo(_touchStartPos);
            if (totalMoved > PanDeadZone)
                _hasMoved = true;

            if (_hasMoved)
            {
                ApplyPan(drag.Relative);
                _lastPanDelta = drag.Relative;
            }
            GetViewport().SetInputAsHandled();
        }
        else if (_fingerCount == 2 && _finger0Id >= 0 && _finger1Id >= 0)
        {
            // ── Two fingers: PINCH ZOOM / TWIST ROTATE ──
            float newDist = _finger0Pos.DistanceTo(_finger1Pos);
            float newAngle = (_finger1Pos - _finger0Pos).Angle();

            if (newDist < MinPinchDistance)
            {
                // Fingers too close — ignore to avoid noise
                _prevPinchDist = newDist;
                _prevTwistAngle = newAngle;
                GetViewport().SetInputAsHandled();
                return;
            }

            // Accumulate gesture movement to determine intent
            float pinchDelta = newDist - _prevPinchDist;
            float twistDelta = AngleDifference(newAngle, _prevTwistAngle);

            _accumulatedPinchDelta += MathF.Abs(pinchDelta);
            _accumulatedTwistDelta += MathF.Abs(twistDelta);

            // ── Gesture discrimination ──
            // Once one gesture is locked, the other won't activate for this touch
            if (!_zoomLocked && !_rotateLocked)
            {
                // Neither confirmed yet — check if thresholds met
                if (_accumulatedPinchDelta > PinchActivateThreshold)
                    _zoomLocked = true;
                if (_accumulatedTwistDelta > TwistActivateThreshold)
                    _rotateLocked = true;
            }

            // Apply ZOOM: ratio-based for consistent feel at all zoom levels
            if (_zoomLocked && _prevPinchDist > MinPinchDistance)
            {
                // Ratio: fingers moving apart → ratio > 1 → zoom in (smaller ortho)
                float ratio = _prevPinchDist / newDist;
                float newSize = _targetOrthoSize * ratio;

                // Gentle clamp with soft limits
                _targetOrthoSize = GodotNative.Mathf.Clamp(newSize, MinOrthoSize, MaxOrthoSize);
            }

            // Apply ROTATE: only if twist is the primary gesture
            if (_rotateLocked && !_zoomLocked)
            {
                _targetYaw += twistDelta;
            }

            _prevPinchDist = newDist;
            _prevTwistAngle = newAngle;
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private GodotNative.Vector3 ScreenDeltaToWorld(GodotNative.Vector2 screenDelta)
    {
        float scale = PanSpeed * _currentOrthoSize / 10.0f;

        var right = new GodotNative.Vector3(
            GodotNative.Mathf.Cos(_currentYaw), 0,
            -GodotNative.Mathf.Sin(_currentYaw));
        var forward = new GodotNative.Vector3(
            GodotNative.Mathf.Sin(_currentYaw), 0,
            GodotNative.Mathf.Cos(_currentYaw));

        return -(right * screenDelta.X * scale + forward * screenDelta.Y * scale);
    }

    private void ApplyPan(GodotNative.Vector2 screenDelta)
    {
        _targetLookAt += ScreenDeltaToWorld(screenDelta);

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

    /// <summary>Shortest angular difference, wrapping-safe.</summary>
    private static float AngleDifference(float a, float b)
    {
        float diff = a - b;
        while (diff > GodotNative.Mathf.Pi) diff -= GodotNative.Mathf.Tau;
        while (diff < -GodotNative.Mathf.Pi) diff += GodotNative.Mathf.Tau;
        return diff;
    }
}
