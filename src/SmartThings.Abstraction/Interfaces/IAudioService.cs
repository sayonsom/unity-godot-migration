// =============================================================================
// IAudioService.cs — Engine-agnostic audio abstraction
// Covers playback + microphone capture (critical for push-to-talk)
// =============================================================================

namespace SmartThings.Abstraction.Interfaces;

/// <summary>
/// Audio service covering both playback and capture.
/// Push-to-talk (PTT) is a first-class feature — see <see cref="IMicrophoneCapture"/>.
/// </summary>
public interface IAudioService
{
    // --- Playback ---

    /// <summary>Play a one-shot sound effect.</summary>
    IAudioHandle PlaySfx(string resourcePath, float volumeDb = 0f, float pitchScale = 1f);

    /// <summary>Play background music (cross-fades if already playing).</summary>
    IAudioHandle PlayMusic(string resourcePath, float volumeDb = -6f, float fadeInSeconds = 1f);

    /// <summary>Stop all audio.</summary>
    void StopAll(float fadeOutSeconds = 0.5f);

    /// <summary>Set master volume.</summary>
    void SetMasterVolume(float volumeDb);

    // --- Microphone Capture (Push-to-Talk) ---

    /// <summary>Get the microphone capture interface for PTT pipeline.</summary>
    IMicrophoneCapture Microphone { get; }
}

/// <summary>
/// Microphone capture interface for the push-to-talk voice pipeline.
/// Implementation requires:
///   - Godot: AudioEffectCapture + GDExtension for platform mic access
///   - Android: SpeechRecognizer via JNI bridge
///   - Web: MediaDevices.getUserMedia + SpeechRecognition API
/// </summary>
public interface IMicrophoneCapture
{
    /// <summary>Is microphone capture currently active?</summary>
    bool IsCapturing { get; }

    /// <summary>Is a microphone device available?</summary>
    bool IsAvailable { get; }

    /// <summary>Start capturing audio from the default microphone.</summary>
    Task<bool> StartCaptureAsync(MicrophoneConfig config, CancellationToken ct = default);

    /// <summary>Stop capturing and return the captured audio buffer.</summary>
    Task<AudioBuffer> StopCaptureAsync(CancellationToken ct = default);

    /// <summary>Fired when Voice Activity Detection detects speech start/stop.</summary>
    event Action<VoiceActivityEvent>? OnVoiceActivity;

    /// <summary>Fired with raw audio frames during capture (for visualization).</summary>
    event Action<AudioFrame>? OnAudioFrame;
}

public record MicrophoneConfig(
    int SampleRate = 16000,     // 16kHz for STT models
    int ChannelCount = 1,       // Mono for voice
    bool EnableVAD = true,      // Voice Activity Detection
    float VADThreshold = 0.5f,  // Silero VAD threshold
    float SilenceTimeoutMs = 1500f  // Stop after this much silence
);

public record AudioBuffer(
    float[] Samples,
    int SampleRate,
    int ChannelCount,
    TimeSpan Duration
);

public record AudioFrame(
    float[] Samples,
    int SampleRate,
    float PeakAmplitude,
    float RmsLevel
);

public record VoiceActivityEvent(
    VoiceActivityState State,
    float Confidence,
    TimeSpan Timestamp
);

public enum VoiceActivityState { SpeechStarted, SpeechEnded }

public interface IAudioHandle : IDisposable
{
    bool IsPlaying { get; }
    float VolumeDb { get; set; }
    void Stop(float fadeOutSeconds = 0f);
}
