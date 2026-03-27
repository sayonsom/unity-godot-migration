// =============================================================================
// ServiceRegistration.cs — DI container setup for Godot backend
// Wires all IEngineAbstraction interfaces to their Godot implementations
// =============================================================================

using Microsoft.Extensions.DependencyInjection;
using SmartThings.Abstraction.Interfaces;
using GodotNative = Godot;

namespace SmartThings.Godot.Services;

/// <summary>
/// Registers all Godot backend services with Microsoft.Extensions.DependencyInjection.
///
/// Usage in Godot autoload:
/// <code>
/// public partial class GameBootstrap : Node
/// {
///     public override void _Ready()
///     {
///         var services = new ServiceCollection();
///         ServiceRegistration.RegisterGodotServices(services, this);
///         var provider = services.BuildServiceProvider();
///         // Store provider globally or use Chickensoft.AutoInject
///     }
/// }
/// </code>
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection RegisterGodotServices(
        IServiceCollection services,
        GodotNative.Node sceneRoot)
    {
        // Core engine services — Godot implementations
        services.AddSingleton<IRenderService>(sp =>
        {
            var renderService = new GodotRenderService();
            sceneRoot.AddChild(renderService);
            return renderService;
        });

        services.AddSingleton<IAccessibilityService>(sp =>
        {
            var a11yService = new GodotAccessibilityService();
            sceneRoot.AddChild(a11yService);
            return a11yService;
        });

        services.AddSingleton<IInputService>(sp =>
        {
            var inputService = new GodotInputService();
            sceneRoot.AddChild(inputService);
            return inputService;
        });

        services.AddSingleton<IAudioService>(sp =>
        {
            var audioService = new GodotAudioService();
            sceneRoot.AddChild(audioService);
            return audioService;
        });

        services.AddSingleton<INetworkService>(sp =>
        {
            var networkService = new GodotNetworkService();
            sceneRoot.AddChild(networkService);
            return networkService;
        });

        services.AddSingleton<ISceneService>(sp =>
        {
            var sceneService = new GodotSceneService();
            sceneRoot.AddChild(sceneService);
            return sceneService;
        });

        return services;
    }

    /// <summary>
    /// Register services for the Unity backend (transition period only).
    /// Keep this if you need to run both backends during migration.
    /// Remove once migration is complete.
    /// </summary>
    public static IServiceCollection RegisterUnityServices(
        IServiceCollection services)
    {
        // Placeholder — implement if parallel Unity backend is needed
        // services.AddSingleton<IRenderService, UnityRenderService>();
        // etc.
        throw new NotImplementedException(
            "Unity backend not implemented. " +
            "If migration is one-way (recommended), remove this method.");
    }
}
