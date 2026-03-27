// =============================================================================
// PushToTalkController.cs — Full push-to-talk voice pipeline
// Mic → VAD → STT → Intent Parser → Device Command → Feedback
// =============================================================================

using GodotNative = Godot;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Scripts.Voice;

/// <summary>
/// Push-to-talk voice controller that orchestrates the full pipeline:
///   1. User presses/holds mic button (or hotkey)
///   2. Microphone capture starts → audio frames stream in
///   3. Silero VAD detects speech boundaries
///   4. On speech end → collected audio sent to STT
///   5. STT text → SmartHomeIntentParser → ParsedIntent
///   6. Intent dispatched as DeviceCommand + spoken feedback
///
/// This controller runs as a Godot Node — add it to the scene tree.
/// </summary>
public partial class PushToTalkController : GodotNative.Node
{
    private SileroVADProcessor? _vad;
    private SmartHomeIntentParser? _intentParser;
    private PlatformSTTService? _stt;
    private IMicrophoneCapture? _mic;

    private SmartHome? _home;
    private bool _isListening;
    private bool _isProcessing;
    private DateTime _captureStartTime;

    // Accumulated speech samples for STT
    private readonly List<float> _speechSamples = new();
    private bool _speechDetected;

    // ── Signals ─────────────────────────────────────────────────────────────

    [GodotNative.Signal] public delegate void ListeningStartedEventHandler();
    [GodotNative.Signal] public delegate void ListeningStoppedEventHandler();
    [GodotNative.Signal] public delegate void SpeechDetectedEventHandler();
    [GodotNative.Signal] public delegate void ProcessingStartedEventHandler();
    [GodotNative.Signal] public delegate void TranscriptionReceivedEventHandler(string text);
    [GodotNative.Signal] public delegate void IntentParsedEventHandler(string description, float confidence);
    [GodotNative.Signal] public delegate void CommandExecutedEventHandler(string deviceLabel, string action);
    [GodotNative.Signal] public delegate void ErrorOccurredEventHandler(string error);
    [GodotNative.Signal] public delegate void AudioLevelChangedEventHandler(float level);

    // ── Public State ────────────────────────────────────────────────────────

    public bool IsListening => _isListening;
    public bool IsProcessing => _isProcessing;

    public override void _Ready()
    {
        // Initialize VAD
        _vad = new SileroVADProcessor(threshold: 0.45f, silenceTimeoutMs: 1200f);

        // Try to load Silero model from res:// or user://
        var modelPaths = new[]
        {
            GodotNative.ProjectSettings.GlobalizePath("res://Models/silero_vad.onnx"),
            GodotNative.ProjectSettings.GlobalizePath("user://silero_vad.onnx"),
            System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "silero_vad.onnx")
        };

        foreach (var path in modelPaths)
        {
            if (_vad.TryLoadModel(path)) break;
        }

        if (!_vad.IsModelLoaded)
            GodotNative.GD.Print("[PTT] Using RMS-based VAD (Silero model not found)");

        _vad.OnVoiceActivity += OnVADEvent;

        // Initialize STT
        _stt = new PlatformSTTService();
        _stt.OnTranscriptionResult += OnSTTResult;
        AddChild(_stt);

        // Initialize intent parser
        _intentParser = new SmartHomeIntentParser();
        _intentParser.OnIntentParsed += OnIntentResult;

        GodotNative.GD.Print("[PTT] Push-to-talk controller ready");
    }

    /// <summary>Set the home data for intent parsing context.</summary>
    public void SetHome(SmartHome home)
    {
        _home = home;
        _intentParser?.SetHome(home);
    }

    /// <summary>Set the microphone capture source (from IAudioService).</summary>
    public void SetMicrophone(IMicrophoneCapture mic) => _mic = mic;

    /// <summary>Start listening for voice commands.</summary>
    public async void StartListening()
    {
        if (_isListening || _isProcessing || _mic == null) return;

        _isListening = true;
        _speechDetected = false;
        _speechSamples.Clear();
        _captureStartTime = DateTime.UtcNow;
        _vad?.Reset();

        var config = new MicrophoneConfig(
            SampleRate: 16000,
            ChannelCount: 1,
            EnableVAD: false, // We handle VAD ourselves with Silero
            SilenceTimeoutMs: 2000
        );

        var success = await _mic.StartCaptureAsync(config);
        if (!success)
        {
            _isListening = false;
            EmitSignal(SignalName.ErrorOccurred, "Failed to start microphone");
            return;
        }

        _mic.OnAudioFrame += OnAudioFrame;
        EmitSignal(SignalName.ListeningStarted);
        GodotNative.GD.Print("[PTT] Listening started");
    }

    /// <summary>Stop listening and process any captured speech.</summary>
    public async void StopListening()
    {
        if (!_isListening) return;

        _isListening = false;
        if (_mic != null)
        {
            _mic.OnAudioFrame -= OnAudioFrame;
            await _mic.StopCaptureAsync();
        }

        EmitSignal(SignalName.ListeningStopped);

        // If we collected speech samples, send to STT
        if (_speechSamples.Count > 1600) // At least 100ms of audio
        {
            ProcessSpeech();
        }
        else
        {
            GodotNative.GD.Print("[PTT] No speech detected, stopped");
        }
    }

    /// <summary>Toggle listening on/off.</summary>
    public void ToggleListening()
    {
        if (_isListening) StopListening();
        else StartListening();
    }

    // ── Audio Processing Pipeline ───────────────────────────────────────────

    private void OnAudioFrame(AudioFrame frame)
    {
        // Forward audio level for UI visualization
        EmitSignal(SignalName.AudioLevelChanged, frame.RmsLevel);

        // Feed samples to VAD
        var timestamp = TimeSpan.FromMilliseconds((DateTime.UtcNow - _captureStartTime).TotalMilliseconds);
        _vad?.ProcessSamples(frame.Samples, timestamp);

        // Always accumulate samples while listening (in case VAD is late)
        if (_isListening)
        {
            _speechSamples.AddRange(frame.Samples);

            // Safety: max 30 seconds of audio
            if (_speechSamples.Count > 16000 * 30)
            {
                StopListening();
            }
        }
    }

    private void OnVADEvent(VoiceActivityEvent evt)
    {
        if (evt.State == VoiceActivityState.SpeechStarted)
        {
            _speechDetected = true;
            EmitSignal(SignalName.SpeechDetected);
        }
        else if (evt.State == VoiceActivityState.SpeechEnded && _isListening)
        {
            // Auto-stop when speech ends (hands-free mode)
            StopListening();
        }
    }

    private void ProcessSpeech()
    {
        _isProcessing = true;
        EmitSignal(SignalName.ProcessingStarted);

        // Send to platform STT
        _stt?.TranscribeAudio(_speechSamples.ToArray(), 16000);
    }

    private void OnSTTResult(string text, float confidence, bool isFinal)
    {
        if (!isFinal) return;

        _isProcessing = false;
        EmitSignal(SignalName.TranscriptionReceived, text);
        GodotNative.GD.Print($"[PTT] Transcription: \"{text}\" (confidence: {confidence:F2})");

        if (string.IsNullOrWhiteSpace(text))
        {
            EmitSignal(SignalName.ErrorOccurred, "Could not understand speech");
            return;
        }

        // Parse intent
        var intent = _intentParser?.Parse(text);
        if (intent == null)
        {
            EmitSignal(SignalName.ErrorOccurred, $"Could not understand: \"{text}\"");
            SpeakFeedback($"I heard \"{text}\" but I'm not sure what to do.");
            return;
        }

        EmitSignal(SignalName.IntentParsed, intent.Description, intent.Confidence);

        // Execute the intent
        ExecuteIntent(intent);
    }

    private void OnIntentResult(ParsedIntent intent)
    {
        GodotNative.GD.Print($"[PTT] Intent: {intent.Type} — {intent.Description} (confidence: {intent.Confidence:F2})");
    }

    // ── Intent Execution ────────────────────────────────────────────────────

    private void ExecuteIntent(ParsedIntent intent)
    {
        switch (intent.Type)
        {
            case IntentType.DeviceControl:
                ExecuteDeviceControl(intent);
                break;

            case IntentType.StatusQuery:
                ExecuteStatusQuery(intent);
                break;

            case IntentType.RoomNavigation:
                ExecuteRoomNavigation(intent);
                break;

            case IntentType.RoomQuery:
                ExecuteRoomQuery(intent);
                break;

            case IntentType.SceneActivation:
                ExecuteSceneActivation(intent);
                break;
        }
    }

    private void ExecuteDeviceControl(ParsedIntent intent)
    {
        if (intent.Device == null)
        {
            SpeakFeedback("I couldn't find that device.");
            return;
        }

        var action = intent.Command?.CapabilityId ?? "control";
        var value = intent.Command?.CommandName ?? "";

        EmitSignal(SignalName.CommandExecuted, intent.Device.Label, $"{action} {value}");

        // Provide spoken feedback
        var feedback = intent.Command?.CapabilityId switch
        {
            "switch" when value == "on" => $"Turning on {intent.Device.Label}",
            "switch" when value == "off" => $"Turning off {intent.Device.Label}",
            "setLevel" => $"Setting {intent.Device.Label} to {value}",
            "lock" when value == "locked" => $"Locking {intent.Device.Label}",
            "lock" when value == "unlocked" => $"Unlocking {intent.Device.Label}",
            _ => $"Controlling {intent.Device.Label}"
        };

        SpeakFeedback(feedback);
        GodotNative.GD.Print($"[PTT] Command: {feedback}");
    }

    private void ExecuteStatusQuery(ParsedIntent intent)
    {
        if (intent.Device != null)
        {
            SpeakFeedback($"{intent.Device.Label} is {intent.Device.Status}");
        }
        else
        {
            // List all active devices
            var active = _home?.Devices.Where(d => d.Status == DeviceStatus.Online).ToList();
            var count = active?.Count ?? 0;
            SpeakFeedback($"You have {count} devices online");
        }
    }

    private void ExecuteRoomNavigation(ParsedIntent intent)
    {
        if (intent.Room != null)
        {
            SpeakFeedback($"Navigating to {intent.Room.Name}");
            // The UI will handle actual camera movement via the signal
        }
    }

    private void ExecuteRoomQuery(ParsedIntent intent)
    {
        if (intent.Room == null) return;

        var devices = _home?.Devices.Where(d => d.RoomId == intent.Room.RoomId).ToList();
        var count = devices?.Count ?? 0;

        if (count == 0)
        {
            SpeakFeedback($"{intent.Room.Name} has no devices");
        }
        else
        {
            var names = string.Join(", ", devices!.Select(d => d.Label).Take(5));
            SpeakFeedback($"{intent.Room.Name} has {count} devices: {names}");
        }
    }

    private void ExecuteSceneActivation(ParsedIntent intent)
    {
        SpeakFeedback($"Activating {intent.SceneName} scene");
    }

    // ── Feedback ────────────────────────────────────────────────────────────

    private void SpeakFeedback(string text)
    {
        GodotNative.DisplayServer.TtsSpeak(
            text, voice: "", volume: 100, pitch: 1.0f, rate: 1.1f,
            utteranceId: 0, interrupt: true);
    }

    public override void _ExitTree()
    {
        _vad?.Dispose();
        _mic = null;
    }
}
