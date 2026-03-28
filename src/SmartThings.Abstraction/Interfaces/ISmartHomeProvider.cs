// =============================================================================
// ISmartHomeProvider.cs — Production data provider for SmartThings home data
// Implement this interface to supply real home data from the SmartThings API
// =============================================================================

using SmartThings.Abstraction.Models;

namespace SmartThings.Abstraction.Interfaces;

/// <summary>
/// Provides SmartHome data to the 3D scene controller.
///
/// Production implementations should fetch from the SmartThings Cloud API:
///   GET /locations/{locationId}/rooms
///   GET /devices
///
/// Register your implementation in GameBootstrap.cs:
///   services.AddSingleton&lt;ISmartHomeProvider, MyApiHomeProvider&gt;();
/// </summary>
public interface ISmartHomeProvider
{
    /// <summary>
    /// Returns the current SmartHome data (rooms, devices, placements).
    /// Called once at scene load. Use HomeMapSceneController.LoadHome()
    /// to refresh at runtime.
    /// </summary>
    SmartHome GetCurrentHome();

    /// <summary>
    /// Async version for API-based providers that need to fetch over network.
    /// </summary>
    Task<SmartHome> GetCurrentHomeAsync() => Task.FromResult(GetCurrentHome());
}
