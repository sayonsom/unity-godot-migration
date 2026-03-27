// =============================================================================
// GodotRenderService.cs — Godot 4.5 implementation of IRenderService
// Wraps Godot SceneTree, Node3D, ShaderMaterial, Camera3D
// =============================================================================

using SmartThings.Abstraction;
using SmartThings.Abstraction.Interfaces;
using GodotNative = Godot;

namespace SmartThings.Godot.Services;

/// <summary>
/// Godot backend for IRenderService.
///
/// IMPORTANT MEMORY NOTE (from migration plan):
/// Godot C# objects wrapping engine objects need explicit Dispose() or using blocks.
/// Unity's GC handles this implicitly — you WILL get memory leaks without discipline.
/// All ISceneHandle and INodeHandle implement IDisposable; callers must dispose them.
/// </summary>
public partial class GodotRenderService : GodotNative.Node, IRenderService
{
    private GodotNative.Camera3D? _activeCamera;

    public override void _Ready()
    {
        // Find or create the active camera
        _activeCamera = GetViewport().GetCamera3D();
    }

    // --- Scene Management ---

    public async Task<ISceneHandle> LoadSceneAsync(string resourcePath, CancellationToken ct = default)
    {
        // Use Godot's ResourceLoader for async loading
        var loader = GodotNative.ResourceLoader.LoadThreadedRequest(resourcePath);
        // Poll until loaded (in production, use ResourceLoader.LoadThreadedGetStatus)
        while (GodotNative.ResourceLoader.LoadThreadedGetStatus(resourcePath) ==
               GodotNative.ResourceLoader.ThreadLoadStatus.InProgress)
        {
            ct.ThrowIfCancellationRequested();
            await ToSignal(GetTree(), GodotNative.SceneTree.SignalName.ProcessFrame);
        }

        var resource = GodotNative.ResourceLoader.LoadThreadedGet(resourcePath);
        if (resource is GodotNative.PackedScene packedScene)
        {
            return new GodotSceneHandle(resourcePath, packedScene);
        }
        throw new InvalidOperationException($"Resource at '{resourcePath}' is not a PackedScene.");
    }

    public INodeHandle InstantiateScene(ISceneHandle scene, Transform3D? parentTransform = null)
    {
        if (scene is not GodotSceneHandle godotScene)
            throw new ArgumentException("Scene handle is not a Godot scene.");

        var instance = godotScene.PackedScene.Instantiate<GodotNative.Node3D>();

        if (parentTransform != null)
        {
            instance.GlobalTransform = ToGodotTransform(parentTransform);
        }

        // Add to scene tree
        GetTree().Root.AddChild(instance);

        return new GodotNodeHandle(instance);
    }

    public void DestroyNode(INodeHandle node)
    {
        if (node is GodotNodeHandle godotNode && godotNode.IsValid)
        {
            godotNode.GodotNode.QueueFree();
        }
    }

    // --- Materials & Shaders ---

    public void SetShaderParameter(INodeHandle node, string paramName, object value)
    {
        if (node is not GodotNodeHandle godotNode) return;

        // Find the MeshInstance3D and its material
        var mesh = FindMeshInstance(godotNode.GodotNode);
        if (mesh?.GetActiveMaterial(0) is GodotNative.ShaderMaterial shaderMat)
        {
            shaderMat.SetShaderParameter(paramName, ConvertToGodotVariant(value));
        }
    }

    public void SetMaterial(INodeHandle node, string materialResourcePath)
    {
        if (node is not GodotNodeHandle godotNode) return;
        var mesh = FindMeshInstance(godotNode.GodotNode);
        if (mesh != null)
        {
            var material = GodotNative.GD.Load<GodotNative.Material>(materialResourcePath);
            mesh.SetSurfaceOverrideMaterial(0, material);
        }
    }

    public T? GetShaderParameter<T>(INodeHandle node, string paramName)
    {
        if (node is not GodotNodeHandle godotNode) return default;
        var mesh = FindMeshInstance(godotNode.GodotNode);
        if (mesh?.GetActiveMaterial(0) is GodotNative.ShaderMaterial shaderMat)
        {
            var value = shaderMat.GetShaderParameter(paramName);
            return (T)Convert.ChangeType(value.Obj, typeof(T));
        }
        return default;
    }

    // --- Camera ---

    public void SetCameraTransform(Transform3D transform) =>
        _activeCamera!.GlobalTransform = ToGodotTransform(transform);

    public Transform3D GetCameraTransform() =>
        FromGodotTransform(_activeCamera!.GlobalTransform);

    public void SetCameraProjection(CameraProjection projection)
    {
        if (_activeCamera == null) return;
        _activeCamera.Fov = projection.FieldOfView;
        _activeCamera.Near = projection.NearClip;
        _activeCamera.Far = projection.FarClip;
        _activeCamera.Projection = projection.Type == CameraProjectionType.Orthographic
            ? GodotNative.Camera3D.ProjectionType.Orthogonal
            : GodotNative.Camera3D.ProjectionType.Perspective;
    }

    // --- Metrics ---

    public RenderMetrics GetCurrentMetrics()
    {
        var perf = GodotNative.Performance.Singleton;
        return new RenderMetrics(
            DrawCalls: (int)perf.GetMonitor(GodotNative.Performance.Monitor.RenderTotalDrawCallsInFrame),
            FrameTimeMs: (float)(1000.0 / GodotNative.Engine.GetFramesPerSecond()),
            MemoryUsageBytes: (long)perf.GetMonitor(GodotNative.Performance.Monitor.MemoryStatic),
            ActiveNodes: (int)perf.GetMonitor(GodotNative.Performance.Monitor.ObjectNodeCount)
        );
    }

    public void SetQualityPreset(QualityPreset preset)
    {
        // Adjust Godot rendering settings based on preset
        var vp = GetViewport();
        switch (preset)
        {
            case QualityPreset.Low:
                vp.Msaa3D = GodotNative.Viewport.Msaa.Disabled;
                vp.ScreenSpaceAA = GodotNative.Viewport.ScreenSpaceAAEnum.Disabled;
                break;
            case QualityPreset.Medium:
                vp.Msaa3D = GodotNative.Viewport.Msaa.Msaa2X;
                break;
            case QualityPreset.High:
            case QualityPreset.Ultra:
                vp.Msaa3D = GodotNative.Viewport.Msaa.Msaa4X;
                vp.ScreenSpaceAA = GodotNative.Viewport.ScreenSpaceAAEnum.Fxaa;
                break;
        }
    }

    public async Task<byte[]> CaptureScreenshotAsync(int width, int height, CancellationToken ct = default)
    {
        await ToSignal(GodotNative.RenderingServer.Singleton, "frame_post_draw");
        var image = GetViewport().GetTexture().GetImage();
        image.Resize(width, height);
        return image.SavePngToBuffer();
    }

    // --- Helpers ---

    private static GodotNative.MeshInstance3D? FindMeshInstance(GodotNative.Node node)
    {
        if (node is GodotNative.MeshInstance3D mesh) return mesh;
        foreach (var child in node.GetChildren())
        {
            var found = FindMeshInstance(child);
            if (found != null) return found;
        }
        return null;
    }

    private static GodotNative.Transform3D ToGodotTransform(Transform3D t) =>
        new(new GodotNative.Basis(new GodotNative.Quaternion(t.Rotation.X, t.Rotation.Y, t.Rotation.Z, t.Rotation.W)),
            new GodotNative.Vector3(t.Position.X, t.Position.Y, t.Position.Z));

    private static Transform3D FromGodotTransform(GodotNative.Transform3D t)
    {
        var q = t.Basis.GetRotationQuaternion();
        return new Transform3D(
            new Vector3(t.Origin.X, t.Origin.Y, t.Origin.Z),
            new Quaternion(q.X, q.Y, q.Z, q.W),
            new Vector3(t.Basis.Scale.X, t.Basis.Scale.Y, t.Basis.Scale.Z)
        );
    }

    private static GodotNative.Variant ConvertToGodotVariant(object value) => value switch
    {
        float f => f,
        int i => i,
        bool b => b,
        Color c => new GodotNative.Color(c.R, c.G, c.B, c.A),
        Vector3 v => new GodotNative.Vector3(v.X, v.Y, v.Z),
        _ => GodotNative.Variant.From(value.ToString())
    };
}

// --- Handle implementations ---

internal class GodotSceneHandle : ISceneHandle
{
    public string ResourcePath { get; }
    public bool IsLoaded => PackedScene != null;
    internal GodotNative.PackedScene PackedScene { get; }

    public GodotSceneHandle(string path, GodotNative.PackedScene scene)
    {
        ResourcePath = path;
        PackedScene = scene;
    }

    public void Dispose() { /* PackedScene is managed by Godot ResourceCache */ }
}

internal class GodotNodeHandle : INodeHandle
{
    internal GodotNative.Node3D GodotNode { get; }

    public string Name => GodotNode.Name;
    public bool IsValid => GodotNative.GodotObject.IsInstanceValid(GodotNode);

    public Transform3D GlobalTransform
    {
        get
        {
            var t = GodotNode.GlobalTransform;
            var q = t.Basis.GetRotationQuaternion();
            return new Transform3D(
                new Vector3(t.Origin.X, t.Origin.Y, t.Origin.Z),
                new Quaternion(q.X, q.Y, q.Z, q.W),
                new Vector3(t.Basis.Scale.X, t.Basis.Scale.Y, t.Basis.Scale.Z)
            );
        }
        set => GodotNode.GlobalTransform = new GodotNative.Transform3D(
            new GodotNative.Basis(new GodotNative.Quaternion(value.Rotation.X, value.Rotation.Y, value.Rotation.Z, value.Rotation.W)),
            new GodotNative.Vector3(value.Position.X, value.Position.Y, value.Position.Z)
        );
    }

    public Transform3D LocalTransform
    {
        get => GlobalTransform; // Simplified — use local in production
        set => GlobalTransform = value;
    }

    public bool Visible
    {
        get => GodotNode.Visible;
        set => GodotNode.Visible = value;
    }

    public GodotNodeHandle(GodotNative.Node3D node) => GodotNode = node;

    public void Dispose()
    {
        if (IsValid) GodotNode.QueueFree();
    }
}
