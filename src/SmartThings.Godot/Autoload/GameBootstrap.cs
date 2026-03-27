// =============================================================================
// GameBootstrap.cs — Application entry point and DI container setup
// Autoloaded by Godot (configured in project.godot)
// =============================================================================

using Microsoft.Extensions.DependencyInjection;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Godot.Services;
using GodotNative = Godot;

namespace SmartThings.Godot.Autoload;

/// <summary>
/// Application bootstrap node. Sets up DI container and provides
/// service resolution for all game scripts.
///
/// This replaces Unity's typical pattern of:
///   - Zenject/VContainer for DI
///   - DontDestroyOnLoad for persistent managers
///   - ScriptableObject singletons
///
/// In Godot, Autoload nodes persist across scene changes automatically.
/// </summary>
public partial class GameBootstrap : GodotNative.Node
{
    private static ServiceProvider? _serviceProvider;

    /// <summary>Global service provider — accessible from any script.</summary>
    public static ServiceProvider Services =>
        _serviceProvider ?? throw new InvalidOperationException(
            "GameBootstrap not initialized. Ensure it is set as Autoload in project.godot.");

    /// <summary>Resolve a service from the DI container.</summary>
    public static T Resolve<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>Try to resolve a service (returns null if not registered).</summary>
    public static T? TryResolve<T>() where T : class => Services.GetService<T>();

    public override void _Ready()
    {
        GodotNative.GD.Print("[GameBootstrap] Initializing SmartThings services...");

        var services = new ServiceCollection();

        // Register all Godot backend services
        ServiceRegistration.RegisterGodotServices(services, this);

        // Register application-layer services
        services.AddSingleton<Data.MockDeviceProvider>();
        services.AddSingleton<Data.DeviceVisualizationManager>();

        _serviceProvider = services.BuildServiceProvider();

        GodotNative.GD.Print("[GameBootstrap] All services registered.");
        GodotNative.GD.Print($"  IRenderService: {Resolve<IRenderService>().GetType().Name}");
        GodotNative.GD.Print($"  IInputService: {Resolve<IInputService>().GetType().Name}");
        GodotNative.GD.Print($"  IAudioService: {Resolve<IAudioService>().GetType().Name}");
        GodotNative.GD.Print($"  IAccessibilityService: {Resolve<IAccessibilityService>().GetType().Name}");
        GodotNative.GD.Print($"  INetworkService: {Resolve<INetworkService>().GetType().Name}");
        GodotNative.GD.Print($"  ISceneService: {Resolve<ISceneService>().GetType().Name}");
    }

    public override void _ExitTree()
    {
        GodotNative.GD.Print("[GameBootstrap] Shutting down services...");
        _serviceProvider?.Dispose();
        _serviceProvider = null;
    }
}
