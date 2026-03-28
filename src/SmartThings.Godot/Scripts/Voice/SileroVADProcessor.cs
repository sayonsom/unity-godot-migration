// =============================================================================
// SileroVADProcessor.cs — Silero VAD via ONNX Runtime for speech detection
// Replaces basic RMS threshold with a real neural network VAD model
// =============================================================================

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SmartThings.Abstraction.Interfaces;

namespace SmartThings.Godot.Scripts.Voice;

/// <summary>
/// Silero Voice Activity Detection using ONNX Runtime.
/// Model: silero_vad.onnx (runs locally, no network needed).
///
/// The model takes 512-sample windows at 16kHz and outputs a speech probability.
/// We use a ring buffer to accumulate samples and process in chunks.
///
/// If the ONNX model file is not found, falls back to RMS-based detection.
/// </summary>
public class SileroVADProcessor : IDisposable
{
    private const int SampleRate = 16000;
    private const int WindowSize = 512; // Silero expects 512 samples at 16kHz
    private const float DefaultThreshold = 0.5f;

    private InferenceSession? _session;
    private float[] _state; // LSTM hidden state (2, 1, 64)
    private bool _isModelLoaded;
    private float _threshold;

    // Ring buffer for accumulating samples
    private readonly List<float> _sampleBuffer = new();

    // State tracking
    private bool _isSpeaking;
    private DateTime _lastSpeechTime;
    private float _silenceTimeoutMs;

    public bool IsModelLoaded => _isModelLoaded;
    public bool IsSpeaking => _isSpeaking;

    public event Action<VoiceActivityEvent>? OnVoiceActivity;

    public SileroVADProcessor(float threshold = DefaultThreshold, float silenceTimeoutMs = 1500f)
    {
        _threshold = threshold;
        _silenceTimeoutMs = silenceTimeoutMs;
        _state = new float[2 * 1 * 64]; // Initialize LSTM state to zeros
        _lastSpeechTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Try to load the Silero VAD ONNX model.
    /// Returns true if successful, false if model file not found (will use RMS fallback).
    /// </summary>
    public bool TryLoadModel(string modelPath)
    {
        try
        {
            if (!File.Exists(modelPath))
            {
                global::Godot.GD.PushWarning($"[SileroVAD] Model not found at {modelPath}, using RMS fallback");
                return false;
            }

            var options = new SessionOptions();
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            options.InterOpNumThreads = 1;
            options.IntraOpNumThreads = 1;

            _session = new InferenceSession(modelPath, options);
            _isModelLoaded = true;
            global::Godot.GD.Print("[SileroVAD] ONNX model loaded successfully");
            return true;
        }
        catch (Exception ex)
        {
            global::Godot.GD.PushWarning($"[SileroVAD] Failed to load model: {ex.Message}, using RMS fallback");
            _isModelLoaded = false;
            return false;
        }
    }

    /// <summary>
    /// Process incoming audio samples. Call this with each audio frame from the mic.
    /// </summary>
    public void ProcessSamples(float[] samples, TimeSpan timestamp)
    {
        _sampleBuffer.AddRange(samples);

        // Process in WindowSize chunks
        while (_sampleBuffer.Count >= WindowSize)
        {
            var window = _sampleBuffer.GetRange(0, WindowSize).ToArray();
            _sampleBuffer.RemoveRange(0, WindowSize);

            float speechProb = _isModelLoaded ? RunOnnxInference(window) : ComputeRmsProbability(window);
            UpdateSpeechState(speechProb, timestamp);
        }
    }

    /// <summary>Reset the VAD state (call when starting a new capture session).</summary>
    public void Reset()
    {
        _sampleBuffer.Clear();
        _state = new float[2 * 1 * 64];
        _isSpeaking = false;
        _lastSpeechTime = DateTime.UtcNow;
    }

    private float RunOnnxInference(float[] window)
    {
        if (_session == null) return 0f;

        try
        {
            // Input tensor: (1, 512)
            var inputTensor = new DenseTensor<float>(window, new[] { 1, WindowSize });

            // State tensor: (2, 1, 64)
            var stateTensor = new DenseTensor<float>(_state, new[] { 2, 1, 64 });

            // Sample rate tensor: scalar int64
            var srTensor = new DenseTensor<long>(new long[] { SampleRate }, new[] { 1 });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor),
                NamedOnnxValue.CreateFromTensor("state", stateTensor),
                NamedOnnxValue.CreateFromTensor("sr", srTensor)
            };

            using var results = _session.Run(inputs);
            var resultList = results.ToList();

            // Output: speech probability
            var outputTensor = resultList[0].AsTensor<float>();
            float probability = outputTensor[0];

            // Updated state for next call
            var newState = resultList[1].AsTensor<float>();
            Buffer.BlockCopy(newState.ToArray(), 0, _state, 0, _state.Length * sizeof(float));

            return probability;
        }
        catch (Exception ex)
        {
            global::Godot.GD.PushWarning($"[SileroVAD] Inference error: {ex.Message}");
            return 0f;
        }
    }

    /// <summary>RMS-based fallback when ONNX model is not available.</summary>
    private static float ComputeRmsProbability(float[] window)
    {
        float rmsSum = 0f;
        foreach (var s in window)
            rmsSum += s * s;
        float rms = (float)Math.Sqrt(rmsSum / window.Length);

        // Map RMS to pseudo-probability (calibrated for typical speech levels)
        // Silence: RMS < 0.01, Speech: RMS > 0.03
        return Math.Clamp((rms - 0.01f) / 0.04f, 0f, 1f);
    }

    private void UpdateSpeechState(float probability, TimeSpan timestamp)
    {
        if (probability >= _threshold)
        {
            _lastSpeechTime = DateTime.UtcNow;

            if (!_isSpeaking)
            {
                _isSpeaking = true;
                OnVoiceActivity?.Invoke(new VoiceActivityEvent(
                    VoiceActivityState.SpeechStarted, probability, timestamp));
            }
        }
        else if (_isSpeaking)
        {
            var silenceDuration = (DateTime.UtcNow - _lastSpeechTime).TotalMilliseconds;
            if (silenceDuration >= _silenceTimeoutMs)
            {
                _isSpeaking = false;
                OnVoiceActivity?.Invoke(new VoiceActivityEvent(
                    VoiceActivityState.SpeechEnded, 1f - probability, timestamp));
            }
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
        _isModelLoaded = false;
    }
}
