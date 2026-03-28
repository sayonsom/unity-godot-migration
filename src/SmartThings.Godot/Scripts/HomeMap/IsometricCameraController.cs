// =============================================================================
// IsometricCameraController.cs — Production-grade isometric camera
// Matches the smooth, reliable touch of Unity SmartThings home map.
// 1-finger: pan only. 2-finger: simultaneous pinch zoom + twist rotate.
// =============================================================================

using GodotNative = Godot;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// Orthographic isometric camera designed for portrait-mode phones.
///
/// DESIGN RULES (fixing all previous issues):
///   1. Single finger ONLY ever pans. Never zooms. Period.
///   2. Pinch zoom + twist rotate happen simultaneously (like Google Maps).
///   3. Finger tracking uses dictionary, not brittle index promotion.
///   4. Zoom is additive-proportional (not ratio-based) — works at all levels.
///   5. Max zoom-out auto-calculated from house bounds + portrait aspect.
///   6. Pan momentum on release for natural feel.
///   7. Robust against missed touch events (Android system gesture interference).
/// </summary>
public partial class IsometricCameraController : GodotNative.Camera3D
{
    // ── Exports ──────────────────────────────────────────────────────────────

    [GodotNative.Export] public float MinOrthoSize { get; set; } = 3.0f;
    [GodotNative.Export] public float MaxOrthoSize { get; set; } = 25.0f;
    [GodotNative.Export] public float PanSpeed { get; set; } = 0.005f;
    [GodotNative.Export] public float ZoomSensitivity { get; set; } = 0.008f;
    [GodotNative.Export] public float RotateSensitivity { get; set; } = 1.0f;
    [GodotNative.Export] public float SmoothFactor { get; set; } = 6.0f;
    [GodotNative.Export] public float IsometricAngle { get; set; } = 55.0f;
    [GodotNative.Export] public float MomentumDecay { get; set; } = 0.90f;

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

    private GodotNative.Vector3 _panMomentum;
    private bool _isTouching;

    // ── Bounds ────────────────────────────────────────────────────────────────

    private GodotNative.Vector3 _boundsMin;
    private GodotNative.Vector3 _boundsMax;
    private bool _hasBounds;
    private float _autoMaxOrthoSize = 25.0f;  // auto-calculated from bounds

    // ── Touch state — dictionary-based for robustness ─────────────────────────

    private readonly Dictionary<int, GodotNative.Vector2> _fingers = new();

    // Two-finger gesture state
    private float _prevPinchDist;
    private float _prevTwistAngle;
    private bool _twoFingerActive;

    // Tap detection (single finger, no movement)
    private GodotNative.Vector2 _touchStartPos;
    private double _touchStartTime;
    private bool _hasDragged;
    private GodotNative.Vector2 _lastDragDelta; // for momentum calculation

    private const float CameraDistance = 30.0f;
    private const float TapDistanceThreshold = 12.0f;  // px — tight for reliable taps
    private const float TapTimeThreshold = 0.3f;        // seconds
    private const float PanStartThreshold = 6.0f;       // px dead zone before pan begins
    private const float MinPinchDist = 50.0f;            // ignore if fingers too close

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
        float t = 1.0f - GodotNative.Mathf.Exp(-SmoothFactor * dt);

        // Smooth interpolation
        _currentOrthoSize = GodotNative.Mathf.Lerp(_currentOrthoSize, _targetOrthoSize, t);
        _currentLookAt = _currentLookAt.Lerp(_targetLookAt, t);
        _currentYaw = GodotNative.Mathf.LerpAngle(_currentYaw, _targetYaw, t);

        // Apply pan momentum when no fingers are touching
        if (!_isTouching && _panMomentum.LengthSquared() > 0.00005f)
        {
            _targetLookAt += _panMomentum;
            _panMomentum *= MomentumDecay;
            if (_panMomentum.LengthSquared() < 0.00001f)
                _panMomentum = GodotNative.Vector3.Zero;
        }

        // Clamp to bounds
        if (_hasBounds)
        {
            _targetLookAt = ClampToBounds(_targetLookAt);
            _currentLookAt = ClampToBounds(_currentLookAt);
        }

        Size = _currentOrthoSize;
        UpdateCameraTransform();
    }

    public override void _UnhandledInput(GodotNative.InputEvent @event)
    {
        // ── Mouse (desktop testing) ──
        if (@event is GodotNative.InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == GodotNative.MouseButton.WheelUp)
                _targetOrthoSize = GodotNative.Mathf.Max(MinOrthoSize, _targetOrthoSize - 0.5f);
            else if (mb.ButtonIndex == GodotNative.MouseButton.WheelDown)
                _targetOrthoSize = GodotNative.Mathf.Min(_autoMaxOrthoSize, _targetOrthoSize + 0.5f);
        }

        if (@event is GodotNative.InputEventMouseMotion mm)
        {
            if ((mm.ButtonMask & GodotNative.MouseButtonMask.Left) != 0)
                ApplyPan(mm.Relative);
            if ((mm.ButtonMask & GodotNative.MouseButtonMask.Right) != 0)
                _targetYaw -= mm.Relative.X * 0.004f;
        }

        // ── Touch ──
        if (@event is GodotNative.InputEventScreenTouch touch)
            HandleTouch(touch);
        if (@event is GodotNative.InputEventScreenDrag drag)
            HandleDrag(drag);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Set scene bounds — also calculates max zoom for portrait fit.</summary>
    public void SetBounds(GodotNative.Vector3 min, GodotNative.Vector3 max)
    {
        float pad = 4.0f;
        _boundsMin = new GodotNative.Vector3(min.X - pad, 0, min.Z - pad);
        _boundsMax = new GodotNative.Vector3(max.X + pad, 0, max.Z + pad);
        _hasBounds = true;

        // Calculate max zoom-out that fits the house in portrait mode
        CalculateMaxZoomFromBounds();
    }

    public void FocusOn(GodotNative.Vector3 center, float radius = 0)
    {
        _targetLookAt = new GodotNative.Vector3(center.X, 0, center.Z);
        if (radius > 0)
            _targetOrthoSize = GodotNative.Mathf.Clamp(radius * 1.2f, MinOrthoSize, _autoMaxOrthoSize);
        _panMomentum = GodotNative.Vector3.Zero;
    }

    public void ResetView()
    {
        if (_hasBounds)
        {
            _targetLookAt = (_boundsMin + _boundsMax) * 0.5f;
            // Default view: slightly zoomed in from max
            _targetOrthoSize = _autoMaxOrthoSize * 0.85f;
        }
        _targetYaw = GodotNative.Mathf.DegToRad(-30.0f);
        _panMomentum = GodotNative.Vector3.Zero;
    }

    // ── Touch handling (dictionary-based, robust) ─────────────────────────────

    private void HandleTouch(GodotNative.InputEventScreenTouch touch)
    {
        if (touch.Pressed)
        {
            // Finger down
            _fingers[touch.Index] = touch.Position;

            if (_fingers.Count == 1)
            {
                // First finger — prepare for tap or pan
                _touchStartPos = touch.Position;
                _touchStartTime = GodotNative.Time.GetTicksMsec() / 1000.0;
                _hasDragged = false;
                _isTouching = true;
                _panMomentum = GodotNative.Vector3.Zero; // kill momentum
                _lastDragDelta = GodotNative.Vector2.Zero;
            }
            else if (_fingers.Count == 2)
            {
                // Second finger — initialize pinch/twist
                _hasDragged = true; // not a tap anymore
                InitTwoFingerGesture();
            }
        }
        else
        {
            // Finger up
            if (_fingers.Count == 1 && _fingers.ContainsKey(touch.Index))
            {
                // Last finger lifting — check for tap
                double elapsed = GodotNative.Time.GetTicksMsec() / 1000.0 - _touchStartTime;
                float dist = touch.Position.DistanceTo(_touchStartPos);

                if (!_hasDragged && dist < TapDistanceThreshold && elapsed < TapTimeThreshold)
                {
                    EmitSignal(SignalName.ScreenTapped, touch.Position);
                }
                else if (_hasDragged && _fingers.Count == 1)
                {
                    // Release pan momentum
                    _panMomentum = ScreenDeltaToWorld(_lastDragDelta) * 0.4f;
                }

                _isTouching = false;
            }

            // Remove finger
            _fingers.Remove(touch.Index);

            // If we went from 2 → 1 finger, DON'T start panning from the
            // remaining finger's current position (would cause a jump).
            // Reset the drag reference.
            if (_fingers.Count == 1)
            {
                _twoFingerActive = false;
                // Mark as dragged so we don't accidentally trigger a tap
                _hasDragged = true;
            }
            else if (_fingers.Count == 0)
            {
                _twoFingerActive = false;
            }

            // Safety: if somehow we have negative count (missed events), reset
            if (_fingers.Count < 0)
            {
                _fingers.Clear();
                _twoFingerActive = false;
                _isTouching = false;
            }
        }
    }

    private void HandleDrag(GodotNative.InputEventScreenDrag drag)
    {
        // Update stored position
        if (!_fingers.ContainsKey(drag.Index)) return;
        _fingers[drag.Index] = drag.Position;

        if (_fingers.Count == 1)
        {
            // ── SINGLE FINGER: PAN ONLY (never zoom) ──

            // Dead zone — don't pan on tiny movements (prevents jitter on tap)
            float totalMoved = drag.Position.DistanceTo(_touchStartPos);
            if (totalMoved < PanStartThreshold && !_hasDragged)
                return;

            _hasDragged = true;
            ApplyPan(drag.Relative);
            _lastDragDelta = drag.Relative;
            GetViewport().SetInputAsHandled();
        }
        else if (_fingers.Count == 2 && _twoFingerActive)
        {
            // ── TWO FINGERS: PINCH ZOOM + TWIST ROTATE (simultaneous) ──

            var positions = GetTwoFingerPositions();
            if (positions == null) return;

            var (f0, f1) = positions.Value;
            float newDist = f0.DistanceTo(f1);
            float newAngle = (f1 - f0).Angle();

            // ── ZOOM (additive-proportional) ──
            if (newDist > MinPinchDist && _prevPinchDist > MinPinchDist)
            {
                float pinchDelta = newDist - _prevPinchDist;

                // Scale sensitivity by current zoom level so it feels consistent
                // At ortho 5, small finger movement = big zoom change
                // At ortho 20, same movement = proportionally same visual change
                float zoomAmount = pinchDelta * ZoomSensitivity * (_currentOrthoSize / 10.0f);

                // Fingers apart (positive delta) → zoom IN (smaller ortho)
                _targetOrthoSize -= zoomAmount;
                _targetOrthoSize = GodotNative.Mathf.Clamp(
                    _targetOrthoSize, MinOrthoSize, _autoMaxOrthoSize);
            }

            // ── ROTATE (always allowed, simultaneous with zoom) ──
            float angleDelta = ShortAngleDiff(newAngle, _prevTwistAngle);

            // Only apply if angle change is meaningful (filter noise)
            if (MathF.Abs(angleDelta) > 0.01f && MathF.Abs(angleDelta) < 1.0f)
            {
                _targetYaw += angleDelta * RotateSensitivity;
            }

            _prevPinchDist = newDist;
            _prevTwistAngle = newAngle;
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Two-finger gesture initialization ─────────────────────────────────────

    private void InitTwoFingerGesture()
    {
        var positions = GetTwoFingerPositions();
        if (positions == null) return;

        var (f0, f1) = positions.Value;
        _prevPinchDist = f0.DistanceTo(f1);
        _prevTwistAngle = (f1 - f0).Angle();
        _twoFingerActive = true;
    }

    private (GodotNative.Vector2, GodotNative.Vector2)? GetTwoFingerPositions()
    {
        if (_fingers.Count < 2) return null;

        // Get first two finger positions from dictionary
        GodotNative.Vector2? f0 = null, f1 = null;
        foreach (var pos in _fingers.Values)
        {
            if (f0 == null) f0 = pos;
            else if (f1 == null) { f1 = pos; break; }
        }

        if (f0 == null || f1 == null) return null;
        return (f0.Value, f1.Value);
    }

    // ── Max zoom calculation for portrait mode ────────────────────────────────

    private void CalculateMaxZoomFromBounds()
    {
        float houseWidth = _boundsMax.X - _boundsMin.X;
        float houseDepth = _boundsMax.Z - _boundsMin.Z;

        // For orthographic camera, Size = half the visible vertical extent.
        // Visible height = Size * 2
        // Visible width  = Size * 2 * aspect_ratio
        //
        // To fit the house:
        //   Size >= houseDepth / 2   (to fit vertically)
        //   Size >= houseWidth / (2 * aspect)  (to fit horizontally)
        //
        // On portrait phone (9:20 ≈ 0.45 aspect), width is the limiting factor.
        // We use the isometric angle to account for the tilted view.

        float cosAngle = GodotNative.Mathf.Cos(_pitchRad);

        // In isometric view, the "depth" maps to screen Y scaled by cos(pitch)
        float effectiveDepth = houseDepth * cosAngle;
        float sizeForHeight = (effectiveDepth + 2.0f) / 2.0f; // +2 padding

        // Estimate a conservative portrait aspect ratio
        float aspect = 0.5f; // conservative; real phone might be 0.46
        var viewport = GetViewport();
        if (viewport != null)
        {
            var vpSize = viewport.GetVisibleRect().Size;
            if (vpSize.Y > 0)
                aspect = vpSize.X / vpSize.Y;
        }

        float sizeForWidth = (houseWidth + 2.0f) / (2.0f * aspect);

        // Max zoom = whichever is larger (tighter constraint) + small padding
        _autoMaxOrthoSize = MathF.Max(sizeForHeight, sizeForWidth) + 1.5f;

        // Reasonable bounds
        _autoMaxOrthoSize = GodotNative.Mathf.Clamp(_autoMaxOrthoSize, 10.0f, MaxOrthoSize);

        GodotNative.GD.Print($"[Camera] Auto max zoom: {_autoMaxOrthoSize:F1} " +
            $"(house: {houseWidth:F1}x{houseDepth:F1}, aspect: {aspect:F2})");
    }

    // ── Pan ───────────────────────────────────────────────────────────────────

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
        if (_hasBounds) _targetLookAt = ClampToBounds(_targetLookAt);
    }

    private GodotNative.Vector3 ClampToBounds(GodotNative.Vector3 pos)
    {
        return new GodotNative.Vector3(
            GodotNative.Mathf.Clamp(pos.X, _boundsMin.X, _boundsMax.X),
            pos.Y,
            GodotNative.Mathf.Clamp(pos.Z, _boundsMin.Z, _boundsMax.Z));
    }

    // ── Camera transform ──────────────────────────────────────────────────────

    private void UpdateCameraTransform()
    {
        var offset = new GodotNative.Vector3(
            CameraDistance * GodotNative.Mathf.Cos(_pitchRad) * GodotNative.Mathf.Sin(_currentYaw),
            CameraDistance * GodotNative.Mathf.Sin(_pitchRad),
            CameraDistance * GodotNative.Mathf.Cos(_pitchRad) * GodotNative.Mathf.Cos(_currentYaw));

        GlobalPosition = _currentLookAt + offset;
        LookAt(_currentLookAt, GodotNative.Vector3.Up);
    }

    private static float ShortAngleDiff(float a, float b)
    {
        float diff = a - b;
        while (diff > GodotNative.Mathf.Pi) diff -= GodotNative.Mathf.Tau;
        while (diff < -GodotNative.Mathf.Pi) diff += GodotNative.Mathf.Tau;
        return diff;
    }
}
