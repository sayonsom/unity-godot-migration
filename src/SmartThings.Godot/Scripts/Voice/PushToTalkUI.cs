// =============================================================================
// PushToTalkUI.cs — Floating mic button with waveform visualization
// Shows listening state, speech detection, processing, and command feedback
// =============================================================================

using GodotNative = Godot;

namespace SmartThings.Godot.Scripts.Voice;

/// <summary>
/// Push-to-talk UI overlay:
///   - Floating mic button (bottom-right, above nav bar)
///   - Waveform/level indicator when listening
///   - Status text (Listening... / Processing... / "Turn on kitchen light")
///   - Pulsing animation when detecting speech
///   - Command result feedback toast
/// </summary>
public partial class PushToTalkUI : GodotNative.Control
{
    private PushToTalkController? _controller;

    // UI elements
    private GodotNative.Button? _micButton;
    private GodotNative.Panel? _listeningPanel;
    private GodotNative.Label? _statusLabel;
    private GodotNative.Label? _transcriptLabel;
    private GodotNative.ProgressBar? _audioLevel;
    private GodotNative.PanelContainer? _feedbackToast;
    private GodotNative.Label? _feedbackLabel;
    private GodotNative.Timer? _toastTimer;

    // Waveform bars
    private readonly List<GodotNative.ColorRect> _waveBars = new();
    private const int WaveBarCount = 20;

    // Animation state
    private float _currentLevel;
    private float _targetLevel;
    private bool _isPulsing;
    private float _pulsePhase;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        BuildUI();
    }

    /// <summary>Connect to a PushToTalkController.</summary>
    public void SetController(PushToTalkController controller)
    {
        _controller = controller;

        controller.ListeningStarted += OnListeningStarted;
        controller.ListeningStopped += OnListeningStopped;
        controller.SpeechDetected += OnSpeechDetected;
        controller.ProcessingStarted += OnProcessingStarted;
        controller.TranscriptionReceived += OnTranscriptionReceived;
        controller.IntentParsed += OnIntentParsed;
        controller.CommandExecuted += OnCommandExecuted;
        controller.ErrorOccurred += OnError;
        controller.AudioLevelChanged += OnAudioLevel;
    }

    public override void _Process(double delta)
    {
        // Smooth audio level interpolation
        _currentLevel = GodotNative.Mathf.Lerp(_currentLevel, _targetLevel, (float)delta * 12f);
        if (_audioLevel != null)
            _audioLevel.Value = _currentLevel * 100;

        // Update waveform bars
        UpdateWaveformBars((float)delta);

        // Pulse animation on mic button when listening
        if (_isPulsing && _micButton != null)
        {
            _pulsePhase += (float)delta * 3f;
            var scale = 1.0f + 0.08f * MathF.Sin(_pulsePhase);
            _micButton.Scale = new GodotNative.Vector2(scale, scale);
        }
    }

    // ── Event Handlers ──────────────────────────────────────────────────────

    private void OnListeningStarted()
    {
        if (_listeningPanel != null) _listeningPanel.Visible = true;
        if (_statusLabel != null) _statusLabel.Text = "Listening...";
        if (_transcriptLabel != null) _transcriptLabel.Text = "";
        SetMicButtonState(MicState.Listening);
        _isPulsing = true;
        _pulsePhase = 0;
    }

    private void OnListeningStopped()
    {
        _isPulsing = false;
        if (_micButton != null) _micButton.Scale = GodotNative.Vector2.One;
        _targetLevel = 0;
    }

    private void OnSpeechDetected()
    {
        if (_statusLabel != null) _statusLabel.Text = "Speech detected...";
        SetMicButtonState(MicState.SpeechDetected);
    }

    private void OnProcessingStarted()
    {
        if (_statusLabel != null) _statusLabel.Text = "Processing...";
        SetMicButtonState(MicState.Processing);
    }

    private void OnTranscriptionReceived(string text)
    {
        if (_transcriptLabel != null)
            _transcriptLabel.Text = $"\"{text}\"";
    }

    private void OnIntentParsed(string description, float confidence)
    {
        if (_statusLabel != null)
            _statusLabel.Text = description;
    }

    private void OnCommandExecuted(string deviceLabel, string action)
    {
        ShowFeedback($"{action} — {deviceLabel}");
        HideListeningPanel();
        SetMicButtonState(MicState.Idle);
    }

    private void OnError(string error)
    {
        ShowFeedback(error);
        HideListeningPanel();
        SetMicButtonState(MicState.Idle);
    }

    private void OnAudioLevel(float level)
    {
        _targetLevel = Math.Clamp(level * 5f, 0f, 1f); // Amplify for visibility
    }

    // ── UI Construction ─────────────────────────────────────────────────────

    private void BuildUI()
    {
        AnchorsPreset = (int)LayoutPreset.FullRect;

        // === Mic Button (floating, bottom-right) ===
        _micButton = new GodotNative.Button();
        _micButton.Text = "MIC";
        _micButton.CustomMinimumSize = new GodotNative.Vector2(70, 70);
        _micButton.AnchorsPreset = (int)LayoutPreset.BottomRight;
        _micButton.OffsetLeft = -90;
        _micButton.OffsetTop = -140;
        _micButton.OffsetRight = -15;
        _micButton.OffsetBottom = -65;
        _micButton.PivotOffset = new GodotNative.Vector2(35, 35);
        _micButton.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_micButton);

        // Style the mic button
        var micStyle = new GodotNative.StyleBoxFlat();
        micStyle.BgColor = new GodotNative.Color(0.2f, 0.6f, 1.0f, 1.0f); // Blue
        micStyle.CornerRadiusTopLeft = 35;
        micStyle.CornerRadiusTopRight = 35;
        micStyle.CornerRadiusBottomLeft = 35;
        micStyle.CornerRadiusBottomRight = 35;
        _micButton.AddThemeStyleboxOverride("normal", micStyle);

        var micPressedStyle = new GodotNative.StyleBoxFlat();
        micPressedStyle.BgColor = new GodotNative.Color(1.0f, 0.3f, 0.3f, 1.0f); // Red when pressed
        micPressedStyle.CornerRadiusTopLeft = 35;
        micPressedStyle.CornerRadiusTopRight = 35;
        micPressedStyle.CornerRadiusBottomLeft = 35;
        micPressedStyle.CornerRadiusBottomRight = 35;
        _micButton.AddThemeStyleboxOverride("pressed", micPressedStyle);

        var micHoverStyle = new GodotNative.StyleBoxFlat();
        micHoverStyle.BgColor = new GodotNative.Color(0.3f, 0.7f, 1.0f, 1.0f);
        micHoverStyle.CornerRadiusTopLeft = 35;
        micHoverStyle.CornerRadiusTopRight = 35;
        micHoverStyle.CornerRadiusBottomLeft = 35;
        micHoverStyle.CornerRadiusBottomRight = 35;
        _micButton.AddThemeStyleboxOverride("hover", micHoverStyle);

        _micButton.AddThemeFontSizeOverride("font_size", 16);
        _micButton.Pressed += () => _controller?.ToggleListening();

        // === Listening Panel (centered, shows when active) ===
        _listeningPanel = new GodotNative.Panel();
        _listeningPanel.AnchorsPreset = (int)LayoutPreset.CenterBottom;
        _listeningPanel.OffsetLeft = -160;
        _listeningPanel.OffsetTop = -220;
        _listeningPanel.OffsetRight = 160;
        _listeningPanel.OffsetBottom = -60;
        _listeningPanel.Visible = false;
        _listeningPanel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_listeningPanel);

        var panelStyle = new GodotNative.StyleBoxFlat();
        panelStyle.BgColor = new GodotNative.Color(0.1f, 0.1f, 0.15f, 0.92f);
        panelStyle.CornerRadiusTopLeft = 16;
        panelStyle.CornerRadiusTopRight = 16;
        panelStyle.CornerRadiusBottomLeft = 16;
        panelStyle.CornerRadiusBottomRight = 16;
        panelStyle.ContentMarginLeft = 16;
        panelStyle.ContentMarginRight = 16;
        panelStyle.ContentMarginTop = 12;
        panelStyle.ContentMarginBottom = 12;
        _listeningPanel.AddThemeStyleboxOverride("panel", panelStyle);

        var vbox = new GodotNative.VBoxContainer();
        vbox.AnchorsPreset = (int)LayoutPreset.FullRect;
        vbox.OffsetLeft = 16;
        vbox.OffsetTop = 12;
        vbox.OffsetRight = -16;
        vbox.OffsetBottom = -12;
        _listeningPanel.AddChild(vbox);

        // Status label
        _statusLabel = new GodotNative.Label();
        _statusLabel.Text = "Listening...";
        _statusLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _statusLabel.AddThemeFontSizeOverride("font_size", 18);
        _statusLabel.AddThemeColorOverride("font_color", new GodotNative.Color(1, 1, 1, 0.9f));
        vbox.AddChild(_statusLabel);

        // Waveform container
        var waveContainer = new GodotNative.HBoxContainer();
        waveContainer.CustomMinimumSize = new GodotNative.Vector2(0, 40);
        waveContainer.Alignment = GodotNative.BoxContainer.AlignmentMode.Center;
        vbox.AddChild(waveContainer);

        for (int i = 0; i < WaveBarCount; i++)
        {
            var bar = new GodotNative.ColorRect();
            bar.CustomMinimumSize = new GodotNative.Vector2(8, 4);
            bar.Color = new GodotNative.Color(0.3f, 0.7f, 1.0f, 0.8f);
            bar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            waveContainer.AddChild(bar);
            _waveBars.Add(bar);
        }

        // Audio level bar
        _audioLevel = new GodotNative.ProgressBar();
        _audioLevel.CustomMinimumSize = new GodotNative.Vector2(0, 6);
        _audioLevel.ShowPercentage = false;
        _audioLevel.MaxValue = 100;
        vbox.AddChild(_audioLevel);

        // Transcript label
        _transcriptLabel = new GodotNative.Label();
        _transcriptLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _transcriptLabel.AddThemeFontSizeOverride("font_size", 14);
        _transcriptLabel.AddThemeColorOverride("font_color", new GodotNative.Color(0.7f, 0.8f, 1.0f, 0.8f));
        _transcriptLabel.AutowrapMode = GodotNative.TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(_transcriptLabel);

        // === Feedback Toast (top-center, auto-dismiss) ===
        _feedbackToast = new GodotNative.PanelContainer();
        _feedbackToast.AnchorsPreset = (int)LayoutPreset.CenterTop;
        _feedbackToast.OffsetLeft = -180;
        _feedbackToast.OffsetTop = 80;
        _feedbackToast.OffsetRight = 180;
        _feedbackToast.OffsetBottom = 130;
        _feedbackToast.Visible = false;
        _feedbackToast.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_feedbackToast);

        var toastStyle = new GodotNative.StyleBoxFlat();
        toastStyle.BgColor = new GodotNative.Color(0.15f, 0.5f, 0.3f, 0.9f); // Green
        toastStyle.CornerRadiusTopLeft = 10;
        toastStyle.CornerRadiusTopRight = 10;
        toastStyle.CornerRadiusBottomLeft = 10;
        toastStyle.CornerRadiusBottomRight = 10;
        toastStyle.ContentMarginLeft = 16;
        toastStyle.ContentMarginRight = 16;
        toastStyle.ContentMarginTop = 10;
        toastStyle.ContentMarginBottom = 10;
        _feedbackToast.AddThemeStyleboxOverride("panel", toastStyle);

        _feedbackLabel = new GodotNative.Label();
        _feedbackLabel.HorizontalAlignment = GodotNative.HorizontalAlignment.Center;
        _feedbackLabel.AddThemeFontSizeOverride("font_size", 16);
        _feedbackLabel.AutowrapMode = GodotNative.TextServer.AutowrapMode.WordSmart;
        _feedbackToast.AddChild(_feedbackLabel);

        // Toast auto-dismiss timer
        _toastTimer = new GodotNative.Timer();
        _toastTimer.WaitTime = 3.0;
        _toastTimer.OneShot = true;
        _toastTimer.Timeout += () =>
        {
            if (_feedbackToast != null) _feedbackToast.Visible = false;
        };
        AddChild(_toastTimer);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private enum MicState { Idle, Listening, SpeechDetected, Processing }

    private void SetMicButtonState(MicState state)
    {
        if (_micButton == null) return;

        _micButton.Text = state switch
        {
            MicState.Idle => "MIC",
            MicState.Listening => "...",
            MicState.SpeechDetected => "REC",
            MicState.Processing => "...",
            _ => "MIC"
        };
    }

    private void ShowFeedback(string text)
    {
        if (_feedbackToast == null || _feedbackLabel == null) return;

        _feedbackLabel.Text = text;
        _feedbackToast.Visible = true;
        _toastTimer?.Start();
    }

    private void HideListeningPanel()
    {
        if (_listeningPanel != null) _listeningPanel.Visible = false;
    }

    private void UpdateWaveformBars(float delta)
    {
        if (_waveBars.Count == 0) return;

        for (int i = 0; i < _waveBars.Count; i++)
        {
            // Each bar has a slightly different frequency for organic look
            float freq = 2.0f + i * 0.3f;
            float phase = (float)(GodotNative.Time.GetTicksMsec() / 1000.0) * freq;
            float barHeight = 4f + _currentLevel * 36f * (0.5f + 0.5f * MathF.Sin(phase));

            _waveBars[i].CustomMinimumSize = new GodotNative.Vector2(8, MathF.Max(4, barHeight));

            // Color shifts with level
            float hue = 0.55f + _currentLevel * 0.1f; // Blue to cyan
            _waveBars[i].Color = GodotNative.Color.FromHsv(hue, 0.6f, 0.9f, 0.8f);
        }
    }
}
