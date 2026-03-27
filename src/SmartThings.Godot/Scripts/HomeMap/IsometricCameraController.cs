// =============================================================================
// IsometricCameraController.cs — Stable isometric camera for 3D Home Map
// Orthographic projection, constrained bounds, gentle touch controls
// =============================================================================

using GodotNative = Godot;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// Orthographic isometric camera for the 3D Home Map View.
/// Designed for stability — the map never flies off screen.
/// Touch: 1-finger pan, pinch zoom. Mouse: scroll zoom, drag pan.
/// </summary>
public partial class IsometricCameraController : GodotNative.Camera3D
{
    // ── Configuration ────────────────────────────────────────────────────────

    [GodotNative.Export] public float MinOrthoSize { get; set; } = 4.0f;
    [GodotNative.Export] public float MaxOrthoSize { get; set; } = 18.0f;
    [GodotNative.Export] public float PanSpeed { get; set; } = 0.015f;
    [GodotNative.Export] public float SmoothFactor { get; set; } = 10.0f;
    [GodotNative.Export] public float IsometricAngle { get; set; } = 55.0f; // Degrees from horizontal

    // ── State ────────────────────────────────────────────────────────────────

    private GodotNative.Vector3 _targetLookAt;
    private float _targetOrthoSize = 12.0f;

    private GodotNative.Vector3 _currentLookAt;
    private float _currentOrthoSize;

    // Bounds (set by assembler)
    private GodotNative.Vector3 _boundsMin;
    private GodotNative.Vector3 _boundsMax;
    private bool _hasBounds;

    // Touch tracking
    private readonly Dictionary<int, GodotNative.Vector2> _touches = new();
    private float _lastPinchDist;
    private bool _isDragging;
    private GodotNative.Vector2 _dragStart;

    // Fixed camera direction (no rotation allowed — stable view)
    private float _yawRad;
    private float _pitchRad;
    private const float CameraDistance = 30.0f; // Far enough for ortho

    /// <summary>Fired when user taps (not drags) on screen.</summary>
    [GodotNative.Signal] public delegate void ScreenTappedEventHandler(GodotNative.Vector2 screenPos);

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        // Set up orthographic projection
        Projection = ProjectionType.Orthogonal;
        Size = _targetOrthoSize;

        _yawRad = GodotNative.Mathf.DegToRad(-30.0f);  // Slight rotation for 3D feel
        _pitchRad = GodotNative.Mathf.DegToRad(IsometricAngle);

        _currentOrthoSize = _targetOrthoSize;
        _currentLookAt = _targetLookAt;

        UpdateCameraTransform();
        GodotNative.GD.Print("[IsometricCamera] Ready — orthographic isometric view.");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float lerp = 1.0f - GodotNative.Mathf.Exp(-SmoothFactor * dt);

        // Smooth interpolation
        _currentOrthoSize = GodotNative.Mathf.Lerp(_currentOrthoSize, _targetOrthoSize, lerp);
        _currentLookAt = _currentLookAt.Lerp(_targetLookAt, lerp);

        // Clamp look-at to bounds
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
        // ── Mouse scroll zoom ──────────────────────────────────────────
        if (@event is GodotNative.InputEventMouseButton mouseBtn)
        {
            if (mouseBtn.ButtonIndex == GodotNative.MouseButton.WheelUp)
            {
                _targetOrthoSize = GodotNative.Mathf.Max(MinOrthoSize, _targetOrthoSize - 1.0f);
                GetViewport().SetInputAsHandled();
            }
            else if (mouseBtn.ButtonIndex == GodotNative.MouseButton.WheelDown)
            {
                _targetOrthoSize = GodotNative.Mathf.Min(MaxOrthoSize, _targetOrthoSize + 1.0f);
                GetViewport().SetInputAsHandled();
            }
        }

        // ── Mouse drag pan ─────────────────────────────────────────────
        if (@event is GodotNative.InputEventMouseMotion mouseMotion)
        {
            if ((mouseMotion.ButtonMask & GodotNative.MouseButtonMask.Left) != 0)
            {
                ApplyPan(mouseMotion.Relative);
                GetViewport().SetInputAsHandled();
            }
        }

        // ── Touch ──────────────────────────────────────────────────────
        if (@event is GodotNative.InputEventScreenTouch touch)
        {
            HandleTouch(touch);
        }
        if (@event is GodotNative.InputEventScreenDrag drag)
        {
            HandleDrag(drag);
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Set the home bounds so the camera stays within the map area.</summary>
    public void SetBounds(GodotNative.Vector3 min, GodotNative.Vector3 max)
    {
        // Add padding
        float pad = 3.0f;
        _boundsMin = new GodotNative.Vector3(min.X - pad, 0, min.Z - pad);
        _boundsMax = new GodotNative.Vector3(max.X + pad, 0, max.Z + pad);
        _hasBounds = true;
    }

    /// <summary>Center on a point and optionally set zoom to fit a radius.</summary>
    public void FocusOn(GodotNative.Vector3 center, float radius = 0)
    {
        _targetLookAt = new GodotNative.Vector3(center.X, 0, center.Z);
        if (radius > 0)
        {
            _targetOrthoSize = GodotNative.Mathf.Clamp(radius * 1.5f, MinOrthoSize, MaxOrthoSize);
        }
    }

    /// <summary>Reset to show entire home.</summary>
    public void ResetView()
    {
        if (_hasBounds)
        {
            _targetLookAt = (_boundsMin + _boundsMax) * 0.5f;
            float span = GodotNative.Mathf.Max(_boundsMax.X - _boundsMin.X, _boundsMax.Z - _boundsMin.Z);
            _targetOrthoSize = GodotNative.Mathf.Clamp(span * 0.6f, MinOrthoSize, MaxOrthoSize);
        }
    }

    // ── Touch handling ───────────────────────────────────────────────────────

    private void HandleTouch(GodotNative.InputEventScreenTouch touch)
    {
        if (touch.Pressed)
        {
            _touches[touch.Index] = touch.Position;

            if (_touches.Count == 1)
            {
                _isDragging = false;
                _dragStart = touch.Position;
            }
            else if (_touches.Count == 2)
            {
                var pts = GetTouchPositions();
                _lastPinchDist = pts[0].DistanceTo(pts[1]);
            }
        }
        else
        {
            // On release: if barely moved, it's a tap
            if (_touches.Count == 1 && !_isDragging)
            {
                if (touch.Position.DistanceTo(_dragStart) < 15.0f)
                {
                    EmitSignal(SignalName.ScreenTapped, touch.Position);
                }
            }
            _touches.Remove(touch.Index);
        }
    }

    private void HandleDrag(GodotNative.InputEventScreenDrag drag)
    {
        _touches[drag.Index] = drag.Position;

        if (_touches.Count == 1)
        {
            // Single finger — pan
            if (drag.Relative.Length() > 2.0f)
                _isDragging = true;

            ApplyPan(drag.Relative);
            GetViewport().SetInputAsHandled();
        }
        else if (_touches.Count == 2)
        {
            // Pinch zoom
            var pts = GetTouchPositions();
            float newDist = pts[0].DistanceTo(pts[1]);
            float delta = newDist - _lastPinchDist;

            _targetOrthoSize = GodotNative.Mathf.Clamp(
                _targetOrthoSize - delta * 0.02f,
                MinOrthoSize, MaxOrthoSize);

            _lastPinchDist = newDist;
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void ApplyPan(GodotNative.Vector2 screenDelta)
    {
        // Scale pan by ortho size so it feels consistent at any zoom
        float scale = PanSpeed * _currentOrthoSize / 10.0f;

        // Convert screen delta to world-space using camera orientation
        var right = new GodotNative.Vector3(
            GodotNative.Mathf.Cos(_yawRad), 0,
            -GodotNative.Mathf.Sin(_yawRad));
        var forward = new GodotNative.Vector3(
            GodotNative.Mathf.Sin(_yawRad), 0,
            GodotNative.Mathf.Cos(_yawRad));

        _targetLookAt -= right * screenDelta.X * scale;
        _targetLookAt -= forward * screenDelta.Y * scale;

        // Immediately clamp targets too (prevents overshoot)
        if (_hasBounds)
        {
            _targetLookAt.X = GodotNative.Mathf.Clamp(_targetLookAt.X, _boundsMin.X, _boundsMax.X);
            _targetLookAt.Z = GodotNative.Mathf.Clamp(_targetLookAt.Z, _boundsMin.Z, _boundsMax.Z);
        }
    }

    private void UpdateCameraTransform()
    {
        // Position camera on a sphere around the look-at point
        var offset = new GodotNative.Vector3(
            CameraDistance * GodotNative.Mathf.Cos(_pitchRad) * GodotNative.Mathf.Sin(_yawRad),
            CameraDistance * GodotNative.Mathf.Sin(_pitchRad),
            CameraDistance * GodotNative.Mathf.Cos(_pitchRad) * GodotNative.Mathf.Cos(_yawRad));

        GlobalPosition = _currentLookAt + offset;
        LookAt(_currentLookAt, GodotNative.Vector3.Up);
    }

    private List<GodotNative.Vector2> GetTouchPositions()
    {
        return new List<GodotNative.Vector2>(_touches.Values);
    }
}
