// =============================================================================
// IRenderService.cs — Engine-agnostic rendering abstraction
// Part of IEngineAbstraction interface set (see migration plan Phase 1)
// =============================================================================

namespace SmartThings.Abstraction.Interfaces;

/// <summary>
/// Abstracts 3D rendering operations so business logic never references
/// Unity or Godot types directly. Implementations live in engine-specific backends.
/// </summary>
public interface IRenderService
{
    // --- Scene Management ---

    /// <summary>Load a 3D scene by resource path (glTF/tscn/prefab).</summary>
    Task<ISceneHandle> LoadSceneAsync(string resourcePath, CancellationToken ct = default);

    /// <summary>Instantiate a loaded scene into the active world.</summary>
    INodeHandle InstantiateScene(ISceneHandle scene, Transform3D? parentTransform = null);

    /// <summary>Remove a node from the scene tree and free its resources.</summary>
    void DestroyNode(INodeHandle node);

    // --- Materials & Shaders ---

    /// <summary>Set a shader parameter on a node's material (e.g., device state color).</summary>
    void SetShaderParameter(INodeHandle node, string paramName, object value);

    /// <summary>Swap the material on a mesh node.</summary>
    void SetMaterial(INodeHandle node, string materialResourcePath);

    /// <summary>Get current material parameter value.</summary>
    T? GetShaderParameter<T>(INodeHandle node, string paramName);

    // --- Camera ---

    /// <summary>Set camera transform (position + rotation).</summary>
    void SetCameraTransform(Transform3D transform);

    /// <summary>Get current camera transform.</summary>
    Transform3D GetCameraTransform();

    /// <summary>Set camera projection parameters.</summary>
    void SetCameraProjection(CameraProjection projection);

    // --- Rendering State ---

    /// <summary>Get current frame performance metrics.</summary>
    RenderMetrics GetCurrentMetrics();

    /// <summary>Set rendering quality preset (for adaptive quality on mobile).</summary>
    void SetQualityPreset(QualityPreset preset);

    /// <summary>Request a screenshot of the current viewport.</summary>
    Task<byte[]> CaptureScreenshotAsync(int width, int height, CancellationToken ct = default);
}

/// <summary>Opaque handle to a loaded scene resource.</summary>
public interface ISceneHandle : IDisposable
{
    string ResourcePath { get; }
    bool IsLoaded { get; }
}

/// <summary>Opaque handle to an instantiated scene node.</summary>
public interface INodeHandle : IDisposable
{
    string Name { get; }
    bool IsValid { get; }
    Transform3D GlobalTransform { get; set; }
    Transform3D LocalTransform { get; set; }
    bool Visible { get; set; }
}

public enum QualityPreset
{
    Low,        // Mobile renderer, reduced draw distance
    Medium,     // Mobile renderer, full features
    High,       // Forward+ renderer
    Ultra       // Forward+ with all post-processing
}

public readonly record struct RenderMetrics(
    int DrawCalls,
    float FrameTimeMs,
    long MemoryUsageBytes,
    int ActiveNodes
);

public enum CameraProjectionType { Perspective, Orthographic }

public record CameraProjection(
    CameraProjectionType Type,
    float FieldOfView = 70f,
    float NearClip = 0.05f,
    float FarClip = 4000f,
    float OrthographicSize = 10f
);
