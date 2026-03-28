// =============================================================================
// AndroidProfiler.cs — On-device performance overlay for Android profiling
// Shows FPS, frame time, memory, draw calls, node count in real-time
// Designed for Phase 5 optimization pass on Galaxy S24
// =============================================================================

using GodotNative = Godot;

namespace SmartThings.Godot.Scripts.Performance;

/// <summary>
/// Lightweight performance profiler overlay for Android testing.
///
/// Displays:
///   - FPS (frames per second) with color coding (green > 55, yellow > 30, red)
///   - Frame time in milliseconds
///   - Memory usage (static + objects)
///   - Render stats: draw calls, vertices, objects
///   - Scene node count
///   - GC collection counts (Gen0/1/2)
///
/// Toggle with the "PERF" FAB button (bottom-left).
/// Logs periodic snapshots to Android logcat for post-analysis.
/// </summary>
public partial class AndroidProfiler : GodotNative.Control
{
    private GodotNative.Label? _fpsLabel;
    private GodotNative.Label? _frameTimeLabel;
    private GodotNative.Label? _memoryLabel;
    private GodotNative.Label? _renderLabel;
    private GodotNative.Label? _nodesLabel;
    private GodotNative.Label? _gcLabel;
    private GodotNative.PanelContainer? _overlay;
    private GodotNative.Button? _toggleBtn;
    private bool _isVisible;

    // Sampling
    private double _sampleTimer;
    private double _logTimer;
    private const double SampleInterval = 0.25;  // update display 4x/sec
    private const double LogInterval = 10.0;      // log to console every 10s
    private int _frameCount;
    private double _frameTimeAccum;
    private float _peakFrameTime;

    // History for average
    private readonly float[] _fpsHistory = new float[60];
    private int _fpsHistoryIdx;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorsPreset = (int)LayoutPreset.FullRect;
        BuildToggleButton();
        BuildOverlay();
    }

    public override void _Process(double delta)
    {
        _frameCount++;
        _frameTimeAccum += delta;
        _peakFrameTime = MathF.Max(_peakFrameTime, (float)delta);

        _sampleTimer += delta;
        if (_sampleTimer >= SampleInterval)
        {
            UpdateDisplay();
            _sampleTimer = 0;
        }

        _logTimer += delta;
        if (_logTimer >= LogInterval)
        {
            LogSnapshot();
            _logTimer = 0;
        }
    }

    private void UpdateDisplay()
    {
        if (!_isVisible || _overlay == null) return;

        float fps = (float)(_frameCount / _frameTimeAccum);
        float avgFrameTime = (float)(_frameTimeAccum / _frameCount) * 1000f;

        // Record FPS history
        _fpsHistory[_fpsHistoryIdx % _fpsHistory.Length] = fps;
        _fpsHistoryIdx++;

        // FPS with color coding
        string fpsColor = fps >= 55 ? "00cc44" : fps >= 30 ? "cccc00" : "cc2222";
        if (_fpsLabel != null)
            _fpsLabel.Text = $"FPS: {fps:F0}  ({avgFrameTime:F1}ms)  peak: {_peakFrameTime * 1000:F1}ms";

        // Set FPS color
        var color = fps >= 55
            ? new GodotNative.Color(0, 0.8f, 0.27f)
            : fps >= 30
                ? new GodotNative.Color(0.8f, 0.8f, 0)
                : new GodotNative.Color(0.8f, 0.13f, 0.13f);
        _fpsLabel?.AddThemeColorOverride("font_color", color);

        // Memory
        long staticMem = (long)GodotNative.OS.GetStaticMemoryUsage();
        long staticMB = staticMem / (1024 * 1024);
        if (_memoryLabel != null)
            _memoryLabel.Text = $"Memory: {staticMB}MB static";

        // Render info
        var viewport = GetViewport();
        if (viewport != null && _renderLabel != null)
        {
            var ri = viewport.GetRenderInfo(
                GodotNative.Viewport.RenderInfoType.Visible,
                GodotNative.Viewport.RenderInfo.ObjectsInFrame);
            var drawCalls = viewport.GetRenderInfo(
                GodotNative.Viewport.RenderInfoType.Visible,
                GodotNative.Viewport.RenderInfo.DrawCallsInFrame);
            var primitives = viewport.GetRenderInfo(
                GodotNative.Viewport.RenderInfoType.Visible,
                GodotNative.Viewport.RenderInfo.PrimitivesInFrame);

            _renderLabel.Text = $"Draw: {drawCalls}  Obj: {ri}  Tris: {primitives / 1000}K";
        }

        // Node count
        if (_nodesLabel != null)
        {
            int nodeCount = (int)GodotNative.Performance.GetMonitor(
                GodotNative.Performance.Monitor.ObjectNodeCount);
            int orphanCount = (int)GodotNative.Performance.GetMonitor(
                GodotNative.Performance.Monitor.ObjectOrphanNodeCount);
            _nodesLabel.Text = $"Nodes: {nodeCount}  Orphans: {orphanCount}";

            // Warn on orphan leak
            if (orphanCount > 10)
                _nodesLabel.AddThemeColorOverride("font_color",
                    new GodotNative.Color(1, 0.4f, 0.4f));
            else
                _nodesLabel.AddThemeColorOverride("font_color",
                    new GodotNative.Color(0.8f, 0.8f, 0.8f));
        }

        // .NET GC stats
        if (_gcLabel != null)
        {
            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);
            long totalMem = GC.GetTotalMemory(false) / (1024 * 1024);
            _gcLabel.Text = $"GC: {totalMem}MB  Gen0:{gen0} Gen1:{gen1} Gen2:{gen2}";
        }

        // Reset accumulators
        _frameCount = 0;
        _frameTimeAccum = 0;
        _peakFrameTime = 0;
    }

    private void LogSnapshot()
    {
        // Average FPS from history
        float sum = 0;
        int count = Math.Min(_fpsHistoryIdx, _fpsHistory.Length);
        for (int i = 0; i < count; i++) sum += _fpsHistory[i];
        float avgFps = count > 0 ? sum / count : 0;

        long mem = GC.GetTotalMemory(false) / (1024 * 1024);
        int nodes = (int)GodotNative.Performance.GetMonitor(
            GodotNative.Performance.Monitor.ObjectNodeCount);
        int orphans = (int)GodotNative.Performance.GetMonitor(
            GodotNative.Performance.Monitor.ObjectOrphanNodeCount);

        GodotNative.GD.Print(
            $"[PERF] Avg FPS: {avgFps:F0} | .NET Heap: {mem}MB | " +
            $"Nodes: {nodes} | Orphans: {orphans} | " +
            $"GC Gen0:{GC.CollectionCount(0)} Gen1:{GC.CollectionCount(1)} Gen2:{GC.CollectionCount(2)}");

        // Warn on issues
        if (avgFps < 30)
            GodotNative.GD.PushWarning($"[PERF] LOW FPS: {avgFps:F0} — needs optimization");
        if (orphans > 20)
            GodotNative.GD.PushWarning($"[PERF] MEMORY LEAK: {orphans} orphan nodes detected");
        if (mem > 200)
            GodotNative.GD.PushWarning($"[PERF] HIGH MEMORY: {mem}MB .NET heap");
    }

    // ── UI Construction ────────────────────────────────────────────────────────

    private void BuildToggleButton()
    {
        _toggleBtn = new GodotNative.Button();
        _toggleBtn.Text = "PERF";
        _toggleBtn.MouseFilter = MouseFilterEnum.Stop;

        // Bottom-left FAB
        _toggleBtn.AnchorLeft = 0;
        _toggleBtn.AnchorTop = 1;
        _toggleBtn.AnchorRight = 0;
        _toggleBtn.AnchorBottom = 1;
        _toggleBtn.OffsetLeft = 12;
        _toggleBtn.OffsetTop = -140;
        _toggleBtn.OffsetRight = 84;
        _toggleBtn.OffsetBottom = -76;
        _toggleBtn.AddThemeFontSizeOverride("font_size", 16);
        _toggleBtn.AddThemeColorOverride("font_color", GodotNative.Colors.White);

        var style = new GodotNative.StyleBoxFlat();
        style.BgColor = new GodotNative.Color(0.6f, 0.3f, 0.1f, 0.9f);
        style.CornerRadiusTopLeft = 14;
        style.CornerRadiusTopRight = 14;
        style.CornerRadiusBottomLeft = 14;
        style.CornerRadiusBottomRight = 14;
        _toggleBtn.AddThemeStyleboxOverride("normal", style);

        var pressed = new GodotNative.StyleBoxFlat();
        pressed.BgColor = new GodotNative.Color(0.75f, 0.4f, 0.15f, 0.95f);
        pressed.CornerRadiusTopLeft = 14;
        pressed.CornerRadiusTopRight = 14;
        pressed.CornerRadiusBottomLeft = 14;
        pressed.CornerRadiusBottomRight = 14;
        _toggleBtn.AddThemeStyleboxOverride("hover", pressed);
        _toggleBtn.AddThemeStyleboxOverride("pressed", pressed);

        _toggleBtn.Pressed += () =>
        {
            _isVisible = !_isVisible;
            if (_overlay != null) _overlay.Visible = _isVisible;
            _toggleBtn.Text = _isVisible ? "HIDE" : "PERF";
        };

        AddChild(_toggleBtn);
    }

    private void BuildOverlay()
    {
        _overlay = new GodotNative.PanelContainer();
        _overlay.Visible = false;
        _overlay.MouseFilter = MouseFilterEnum.Ignore; // don't block touches

        // Top-left corner, compact
        _overlay.AnchorLeft = 0;
        _overlay.AnchorTop = 0;
        _overlay.AnchorRight = 1;
        _overlay.AnchorBottom = 0;
        _overlay.OffsetLeft = 0;
        _overlay.OffsetTop = 56; // below status bar
        _overlay.OffsetRight = 0;
        _overlay.OffsetBottom = 210;

        var bgStyle = new GodotNative.StyleBoxFlat();
        bgStyle.BgColor = new GodotNative.Color(0, 0, 0, 0.7f);
        bgStyle.ContentMarginLeft = 12;
        bgStyle.ContentMarginRight = 12;
        bgStyle.ContentMarginTop = 6;
        bgStyle.ContentMarginBottom = 6;
        _overlay.AddThemeStyleboxOverride("panel", bgStyle);
        AddChild(_overlay);

        var vbox = new GodotNative.VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        _overlay.AddChild(vbox);

        _fpsLabel = MakeLabel("FPS: --");
        vbox.AddChild(_fpsLabel);

        _memoryLabel = MakeLabel("Memory: --");
        vbox.AddChild(_memoryLabel);

        _renderLabel = MakeLabel("Draw: --");
        vbox.AddChild(_renderLabel);

        _nodesLabel = MakeLabel("Nodes: --");
        vbox.AddChild(_nodesLabel);

        _gcLabel = MakeLabel("GC: --");
        vbox.AddChild(_gcLabel);
    }

    private static GodotNative.Label MakeLabel(string text)
    {
        var label = new GodotNative.Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new GodotNative.Color(0.8f, 0.8f, 0.8f));
        return label;
    }
}
