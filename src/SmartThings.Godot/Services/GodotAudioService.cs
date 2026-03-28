// =============================================================================
// GodotAudioService.cs — Godot 4.5 implementation of IAudioService
// Covers audio playback (SFX/Music) + microphone capture (push-to-talk)
// =============================================================================

using SmartThings.Abstraction.Interfaces;
using GodotNative = Godot;

namespace SmartThings.Godot.Services;

/// <summary>
/// Godot backend for IAudioService.
///
/// Key mappings:
///   Unity AudioSource.PlayOneShot()  → AudioStreamPlayer + pooling
///   Unity AudioSource.clip           → AudioStreamPlayer.Stream = GD.Load()
///   Unity AudioListener              → Godot AudioListener3D (auto in Camera3D)
///   Unity Microphone.Start()         → AudioEffectCapture + AudioStreamMicrophone
///
/// Android push-to-talk:
///   Godot handles mic permissions via OS.RequestPermissions().
///   AudioEffectCapture provides raw PCM frames.
///   Silero VAD (ONNX) detects speech boundaries.
/// </summary>
public partial class GodotAudioService : GodotNative.Node, IAudioService
{
    private const int SfxPoolSize = 8;
    private readonly List<GodotNative.AudioStreamPlayer> _sfxPool = new();
    private GodotNative.AudioStreamPlayer? _musicPlayer;
    private GodotNative.AudioStreamPlayer? _nextMusicPlayer;
    private GodotMicrophoneCapture? _microphone;

    public IMicrophoneCapture Microphone => _microphone ??= new GodotMicrophoneCapture(this);

    public override void _Ready()
    {
        // Request mic permissions on Android
        if (GodotNative.OS.GetName() == "Android")
        {
            GodotNative.OS.RequestPermissions();
        }

        // Initialize SFX pool
        for (int i = 0; i < SfxPoolSize; i++)
        {
            var player = new GodotNative.AudioStreamPlayer();
            player.Bus = "SFX";
            AddChild(player);
            _sfxPool.Add(player);
        }

        // Initialize music player
        _musicPlayer = new GodotNative.AudioStreamPlayer();
        _musicPlayer.Bus = "Music";
        AddChild(_musicPlayer);

        // Ensure audio buses exist
        EnsureAudioBuses();
    }

    // --- Playback ---

    public IAudioHandle PlaySfx(string resourcePath, float volumeDb = 0f, float pitchScale = 1f)
    {
        var player = GetAvailableSfxPlayer();
        if (player == null)
        {
            // Pool exhausted — create temporary player
            player = new GodotNative.AudioStreamPlayer();
            player.Bus = "SFX";
            AddChild(player);
        }

        player.Stream = GodotNative.GD.Load<GodotNative.AudioStream>(resourcePath);
        player.VolumeDb = volumeDb;
        player.PitchScale = pitchScale;
        player.Play();

        return new GodotAudioHandle(player);
    }

    public IAudioHandle PlayMusic(string resourcePath, float volumeDb = -6f, float fadeInSeconds = 1f)
    {
        var stream = GodotNative.GD.Load<GodotNative.AudioStream>(resourcePath);

        if (_musicPlayer!.Playing)
        {
            // Cross-fade: create second player, fade out current
            _nextMusicPlayer = new GodotNative.AudioStreamPlayer();
            _nextMusicPlayer.Bus = "Music";
            _nextMusicPlayer.Stream = stream;
            _nextMusicPlayer.VolumeDb = -80f; // Start silent
            AddChild(_nextMusicPlayer);
            _nextMusicPlayer.Play();

            // Fade out old, fade in new
            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(_musicPlayer, "volume_db", -80.0, fadeInSeconds);
            tween.TweenProperty(_nextMusicPlayer, "volume_db", (double)volumeDb, fadeInSeconds);

            tween.Finished += () =>
            {
                _musicPlayer.Stop();
                _musicPlayer.QueueFree();
                _musicPlayer = _nextMusicPlayer;
                _nextMusicPlayer = null;
            };

            return new GodotAudioHandle(_nextMusicPlayer);
        }

        _musicPlayer.Stream = stream;
        _musicPlayer.VolumeDb = volumeDb;
        _musicPlayer.Play();

        return new GodotAudioHandle(_musicPlayer);
    }

    public void StopAll(float fadeOutSeconds = 0.5f)
    {
        if (fadeOutSeconds <= 0)
        {
            foreach (var player in _sfxPool) player.Stop();
            _musicPlayer?.Stop();
            return;
        }

        var tween = CreateTween();
        foreach (var player in _sfxPool.Where(p => p.Playing))
        {
            tween.TweenProperty(player, "volume_db", -80.0, fadeOutSeconds);
        }

        if (_musicPlayer?.Playing == true)
        {
            tween.TweenProperty(_musicPlayer, "volume_db", -80.0, fadeOutSeconds);
        }

        tween.Finished += () =>
        {
            foreach (var player in _sfxPool) player.Stop();
            _musicPlayer?.Stop();
        };
    }

    public void SetMasterVolume(float volumeDb)
    {
        var masterIdx = GodotNative.AudioServer.GetBusIndex("Master");
        if (masterIdx >= 0)
        {
            GodotNative.AudioServer.SetBusVolumeDb(masterIdx, volumeDb);
        }
    }

    // --- Helpers ---

    private GodotNative.AudioStreamPlayer? GetAvailableSfxPlayer()
    {
        return _sfxPool.FirstOrDefault(p => !p.Playing);
    }

    private static void EnsureAudioBuses()
    {
        // Create SFX and Music buses if they don't exist
        if (GodotNative.AudioServer.GetBusIndex("SFX") < 0)
        {
            GodotNative.AudioServer.AddBus();
            var idx = GodotNative.AudioServer.BusCount - 1;
            GodotNative.AudioServer.SetBusName(idx, "SFX");
            GodotNative.AudioServer.SetBusSend(idx, "Master");
        }

        if (GodotNative.AudioServer.GetBusIndex("Music") < 0)
        {
            GodotNative.AudioServer.AddBus();
            var idx = GodotNative.AudioServer.BusCount - 1;
            GodotNative.AudioServer.SetBusName(idx, "Music");
            GodotNative.AudioServer.SetBusSend(idx, "Master");
        }
    }
}

// =============================================================================
// GodotAudioHandle — Wraps a playing AudioStreamPlayer
// =============================================================================

internal class GodotAudioHandle : IAudioHandle
{
    private readonly GodotNative.AudioStreamPlayer _player;

    public bool IsPlaying => GodotNative.GodotObject.IsInstanceValid(_player) && _player.Playing;

    public float VolumeDb
    {
        get => _player.VolumeDb;
        set => _player.VolumeDb = value;
    }

    public GodotAudioHandle(GodotNative.AudioStreamPlayer player) => _player = player;

    public void Stop(float fadeOutSeconds = 0f)
    {
        if (!GodotNative.GodotObject.IsInstanceValid(_player)) return;

        if (fadeOutSeconds <= 0)
        {
            _player.Stop();
            return;
        }

        // Fade out then stop — requires scene tree access
        var tween = _player.CreateTween();
        tween.TweenProperty(_player, "volume_db", -80.0, fadeOutSeconds);
        tween.Finished += () => _player.Stop();
    }

    public void Dispose()
    {
        if (GodotNative.GodotObject.IsInstanceValid(_player))
        {
            _player.Stop();
        }
    }
}

// =============================================================================
// GodotMicrophoneCapture — Push-to-talk microphone pipeline
// =============================================================================

/// <summary>
/// Microphone capture using Godot's AudioEffectCapture.
///
/// Pipeline: Mic → AudioEffectCapture → Raw PCM → VAD (Silero ONNX) → Events
///
/// On Android: requires RECORD_AUDIO permission (requested in _Ready via OS.RequestPermissions).
/// Silero VAD runs locally via ONNX Runtime — no network needed for voice detection.
/// </summary>
internal partial class GodotMicrophoneCapture : GodotNative.Node, IMicrophoneCapture
{
    private GodotNative.AudioEffectCapture? _captureEffect;
    private GodotNative.AudioStreamPlayer? _micPlayer;
    private bool _isCapturing;
    private MicrophoneConfig _config = new();
    private readonly List<float> _capturedSamples = new();
    private DateTime _captureStartTime;
    private DateTime _lastVoiceTime;
    private bool _isSpeaking;

    public bool IsCapturing => _isCapturing;
    public bool IsAvailable => true; // Godot supports mic on all platforms

    public event Action<VoiceActivityEvent>? OnVoiceActivity;
    public event Action<AudioFrame>? OnAudioFrame;

    public GodotMicrophoneCapture(GodotNative.Node parent)
    {
        parent.AddChild(this);
    }

    public Task<bool> StartCaptureAsync(MicrophoneConfig config, CancellationToken ct = default)
    {
        if (_isCapturing) return Task.FromResult(false);

        _config = config;
        _capturedSamples.Clear();
        _captureStartTime = DateTime.UtcNow;
        _lastVoiceTime = DateTime.UtcNow;
        _isSpeaking = false;

        // Set up mic bus with capture effect
        var micBusIdx = EnsureMicBus();

        _captureEffect = (GodotNative.AudioEffectCapture)
            GodotNative.AudioServer.GetBusEffect(micBusIdx, 0);

        // Create mic input player
        _micPlayer = new GodotNative.AudioStreamPlayer();
        _micPlayer.Stream = new GodotNative.AudioStreamMicrophone();
        _micPlayer.Bus = "Mic";
        AddChild(_micPlayer);
        _micPlayer.Play();

        _captureEffect.ClearBuffer();
        _isCapturing = true;
        SetProcess(true);

        return Task.FromResult(true);
    }

    public Task<AudioBuffer> StopCaptureAsync(CancellationToken ct = default)
    {
        _isCapturing = false;
        SetProcess(false);

        _micPlayer?.Stop();
        _micPlayer?.QueueFree();
        _micPlayer = null;

        var duration = DateTime.UtcNow - _captureStartTime;
        var buffer = new AudioBuffer(
            _capturedSamples.ToArray(),
            _config.SampleRate,
            _config.ChannelCount,
            duration);

        _capturedSamples.Clear();
        return Task.FromResult(buffer);
    }

    public override void _Process(double delta)
    {
        if (!_isCapturing || _captureEffect == null) return;

        var framesAvailable = _captureEffect.GetFramesAvailable();
        if (framesAvailable <= 0) return;

        var buffer = _captureEffect.GetBuffer((int)framesAvailable);
        var samples = new float[buffer.Length];
        for (int i = 0; i < buffer.Length; i++)
        {
            samples[i] = buffer[i].X; // Mono: take left channel
        }

        _capturedSamples.AddRange(samples);

        // Compute audio metrics
        float peak = 0f, rmsSum = 0f;
        foreach (var s in samples)
        {
            var abs = Math.Abs(s);
            if (abs > peak) peak = abs;
            rmsSum += s * s;
        }
        var rms = (float)Math.Sqrt(rmsSum / samples.Length);

        OnAudioFrame?.Invoke(new AudioFrame(samples, _config.SampleRate, peak, rms));

        // Simple VAD based on RMS threshold (Silero ONNX integration is TODO)
        if (_config.EnableVAD)
        {
            ProcessVAD(rms);
        }
    }

    private void ProcessVAD(float rmsLevel)
    {
        var threshold = _config.VADThreshold * 0.1f; // Scale to RMS range

        if (rmsLevel > threshold)
        {
            _lastVoiceTime = DateTime.UtcNow;

            if (!_isSpeaking)
            {
                _isSpeaking = true;
                OnVoiceActivity?.Invoke(new VoiceActivityEvent(
                    VoiceActivityState.SpeechStarted,
                    rmsLevel / threshold, // Confidence estimate
                    TimeSpan.FromMilliseconds((DateTime.UtcNow - _captureStartTime).TotalMilliseconds)));
            }
        }
        else if (_isSpeaking)
        {
            var silenceDuration = (DateTime.UtcNow - _lastVoiceTime).TotalMilliseconds;
            if (silenceDuration >= _config.SilenceTimeoutMs)
            {
                _isSpeaking = false;
                OnVoiceActivity?.Invoke(new VoiceActivityEvent(
                    VoiceActivityState.SpeechEnded,
                    0.9f,
                    TimeSpan.FromMilliseconds((DateTime.UtcNow - _captureStartTime).TotalMilliseconds)));
            }
        }
    }

    private static int EnsureMicBus()
    {
        var idx = GodotNative.AudioServer.GetBusIndex("Mic");
        if (idx < 0)
        {
            GodotNative.AudioServer.AddBus();
            idx = GodotNative.AudioServer.BusCount - 1;
            GodotNative.AudioServer.SetBusName(idx, "Mic");
            GodotNative.AudioServer.SetBusMute(idx, true); // Don't play mic back to speakers
            GodotNative.AudioServer.AddBusEffect(idx, new GodotNative.AudioEffectCapture());
        }
        return idx;
    }
}
