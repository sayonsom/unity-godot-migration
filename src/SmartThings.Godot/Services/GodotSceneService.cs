// =============================================================================
// GodotSceneService.cs — Godot 4.5 implementation of ISceneService
// Wraps SceneTree lifecycle, scene transitions, and process callbacks
// =============================================================================

using SmartThings.Abstraction.Interfaces;
using GodotNative = Godot;

namespace SmartThings.Godot.Services;

/// <summary>
/// Godot backend for ISceneService.
/// Replaces Unity's SceneManager + MonoBehaviour lifecycle.
///
/// Key mappings:
///   SceneManager.LoadScene()    → GetTree().ChangeSceneToFile()
///   MonoBehaviour.Update()      → _Process(delta) via callback nodes
///   MonoBehaviour.FixedUpdate() → _PhysicsProcess(delta) via callback nodes
///   DontDestroyOnLoad           → Autoload nodes (configured in project.godot)
/// </summary>
public partial class GodotSceneService : GodotNative.Node, ISceneService
{
    private string _currentSceneId = "";
    private readonly Dictionary<string, GodotNative.PackedScene> _preloadedScenes = new();
    private GodotNative.CanvasLayer? _transitionLayer;
    private GodotNative.ColorRect? _transitionOverlay;

    public string CurrentSceneId => _currentSceneId;

    public event Action<SceneTransitionEvent>? OnSceneTransition;

    public override void _Ready()
    {
        // Track the current scene
        var currentScene = GetTree().CurrentScene;
        _currentSceneId = currentScene?.SceneFilePath ?? "root";

        // Create transition overlay for fade effects
        SetupTransitionOverlay();
    }

    public async Task LoadSceneAsync(
        string sceneId,
        SceneTransition transition = SceneTransition.Fade,
        CancellationToken ct = default)
    {
        var fromScene = _currentSceneId;

        OnSceneTransition?.Invoke(new SceneTransitionEvent(
            fromScene, sceneId, transition, SceneTransitionPhase.Starting));

        // Transition out
        if (transition == SceneTransition.Fade || transition == SceneTransition.CrossFade)
        {
            await FadeOutAsync(0.3f, ct);
        }

        OnSceneTransition?.Invoke(new SceneTransitionEvent(
            fromScene, sceneId, transition, SceneTransitionPhase.Loading));

        // Load scene — use preloaded if available
        GodotNative.PackedScene? packedScene;
        if (_preloadedScenes.TryGetValue(sceneId, out var preloaded))
        {
            packedScene = preloaded;
            _preloadedScenes.Remove(sceneId);
        }
        else
        {
            packedScene = GodotNative.GD.Load<GodotNative.PackedScene>(sceneId);
        }

        if (packedScene == null)
            throw new InvalidOperationException($"Failed to load scene: {sceneId}");

        GetTree().ChangeSceneToPacked(packedScene);
        _currentSceneId = sceneId;

        // Wait one frame for scene to be ready
        await ToSignal(GetTree(), GodotNative.SceneTree.SignalName.ProcessFrame);

        OnSceneTransition?.Invoke(new SceneTransitionEvent(
            fromScene, sceneId, transition, SceneTransitionPhase.Ready));

        // Transition in
        if (transition == SceneTransition.Fade || transition == SceneTransition.CrossFade)
        {
            await FadeInAsync(0.3f, ct);
        }

        OnSceneTransition?.Invoke(new SceneTransitionEvent(
            fromScene, sceneId, transition, SceneTransitionPhase.Complete));
    }

    public async Task PreloadSceneAsync(string sceneId, CancellationToken ct = default)
    {
        if (_preloadedScenes.ContainsKey(sceneId)) return;

        GodotNative.ResourceLoader.LoadThreadedRequest(sceneId);

        while (GodotNative.ResourceLoader.LoadThreadedGetStatus(sceneId) ==
               GodotNative.ResourceLoader.ThreadLoadStatus.InProgress)
        {
            ct.ThrowIfCancellationRequested();
            await ToSignal(GetTree(), GodotNative.SceneTree.SignalName.ProcessFrame);
        }

        var resource = GodotNative.ResourceLoader.LoadThreadedGet(sceneId);
        if (resource is GodotNative.PackedScene packed)
        {
            _preloadedScenes[sceneId] = packed;
        }
    }

    public IDisposable RegisterProcessCallback(Action<float> callback, ProcessMode mode = ProcessMode.Idle)
    {
        var callbackNode = new ProcessCallbackNode(callback, isPhysics: false);
        AddChild(callbackNode);
        return callbackNode;
    }

    public IDisposable RegisterPhysicsCallback(Action<float> callback)
    {
        var callbackNode = new ProcessCallbackNode(callback, isPhysics: true);
        AddChild(callbackNode);
        return callbackNode;
    }

    public void QuitApplication(int exitCode = 0)
    {
        GetTree().Quit(exitCode);
    }

    // --- Transition Helpers ---

    private void SetupTransitionOverlay()
    {
        _transitionLayer = new GodotNative.CanvasLayer();
        _transitionLayer.Layer = 128; // On top of everything
        AddChild(_transitionLayer);

        _transitionOverlay = new GodotNative.ColorRect();
        _transitionOverlay.Color = new GodotNative.Color(0, 0, 0, 0);
        _transitionOverlay.SetAnchorsPreset(GodotNative.Control.LayoutPreset.FullRect);
        _transitionOverlay.MouseFilter = GodotNative.Control.MouseFilterEnum.Ignore;
        _transitionLayer.AddChild(_transitionOverlay);
    }

    private async Task FadeOutAsync(float duration, CancellationToken ct)
    {
        if (_transitionOverlay == null) return;
        var tween = CreateTween();
        tween.TweenProperty(_transitionOverlay, "color:a", 1.0f, duration);
        await ToSignal(tween, GodotNative.Tween.SignalName.Finished);
    }

    private async Task FadeInAsync(float duration, CancellationToken ct)
    {
        if (_transitionOverlay == null) return;
        var tween = CreateTween();
        tween.TweenProperty(_transitionOverlay, "color:a", 0.0f, duration);
        await ToSignal(tween, GodotNative.Tween.SignalName.Finished);
    }
}

/// <summary>
/// Internal node that forwards _Process or _PhysicsProcess to a callback.
/// Disposing removes the node from the tree.
/// </summary>
internal partial class ProcessCallbackNode : GodotNative.Node, IDisposable
{
    private readonly Action<float> _callback;
    private readonly bool _isPhysics;

    public ProcessCallbackNode(Action<float> callback, bool isPhysics)
    {
        _callback = callback;
        _isPhysics = isPhysics;

        // Only enable the relevant process mode
        SetProcess(!isPhysics);
        SetPhysicsProcess(isPhysics);
    }

    public override void _Process(double delta)
    {
        if (!_isPhysics) _callback((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isPhysics) _callback((float)delta);
    }

    public new void Dispose()
    {
        if (GodotNative.GodotObject.IsInstanceValid(this))
        {
            QueueFree();
        }
    }
}
