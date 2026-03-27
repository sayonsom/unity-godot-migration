// =============================================================================
// ISceneService.cs — Engine-agnostic scene/lifecycle abstraction
// Replaces Unity MonoBehaviour lifecycle + SceneManager
// =============================================================================

namespace SmartThings.Abstraction.Interfaces;

/// <summary>
/// Scene lifecycle management. Replaces Unity's SceneManager + MonoBehaviour lifecycle.
/// In Godot, this wraps SceneTree and Node lifecycle methods.
///
/// Key API mapping:
///   Unity SceneManager.LoadScene()     -> LoadSceneAsync()
///   Unity MonoBehaviour.Start()        -> Godot _Ready()
///   Unity MonoBehaviour.Update()       -> Godot _Process(delta)
///   Unity MonoBehaviour.OnDestroy()    -> Godot _ExitTree() / Dispose()
/// </summary>
public interface ISceneService
{
    /// <summary>Current active scene identifier.</summary>
    string CurrentSceneId { get; }

    /// <summary>Load and transition to a new scene.</summary>
    Task LoadSceneAsync(string sceneId, SceneTransition transition = SceneTransition.Fade, CancellationToken ct = default);

    /// <summary>Preload a scene in background (for instant switching).</summary>
    Task PreloadSceneAsync(string sceneId, CancellationToken ct = default);

    /// <summary>Register a game loop callback (replaces MonoBehaviour.Update).</summary>
    IDisposable RegisterProcessCallback(Action<float> callback, ProcessMode mode = ProcessMode.Idle);

    /// <summary>Register a physics loop callback (replaces MonoBehaviour.FixedUpdate).</summary>
    IDisposable RegisterPhysicsCallback(Action<float> callback);

    /// <summary>Fired when scene transition starts.</summary>
    event Action<SceneTransitionEvent>? OnSceneTransition;

    /// <summary>Quit the application.</summary>
    void QuitApplication(int exitCode = 0);
}

public enum SceneTransition
{
    Instant,    // No transition
    Fade,       // Fade to black and back
    CrossFade,  // Cross-fade between scenes
    SlideLeft,  // Slide transition
    Custom      // Custom shader transition
}

public enum ProcessMode
{
    Idle,       // Every frame (Godot _Process)
    Physics     // Fixed timestep (Godot _PhysicsProcess)
}

public record SceneTransitionEvent(
    string FromScene,
    string ToScene,
    SceneTransition Transition,
    SceneTransitionPhase Phase
);

public enum SceneTransitionPhase { Starting, Loading, Ready, Complete }
