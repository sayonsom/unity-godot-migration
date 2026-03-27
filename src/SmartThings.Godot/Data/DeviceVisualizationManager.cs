// =============================================================================
// DeviceVisualizationManager.cs — Maps device state to visual properties
// Translates SmartThings capabilities into shader parameters and colors
// =============================================================================

using SmartThings.Abstraction;

namespace SmartThings.Godot.Data;

/// <summary>
/// Maps SmartThings device state to 3D visualization parameters.
/// This is the bridge between IoT data and rendering.
///
/// Key mappings:
///   thermostatMode → glow_color + is_active
///   temperature delta → energy_level (energy_flow shader)
///   device status → is_error
///   user selection → is_selected
/// </summary>
public class DeviceVisualizationManager
{
    // Color palette for thermostat modes
    public static readonly Color HeatColor = new(1.0f, 0.4f, 0.1f); // Orange
    public static readonly Color CoolColor = new(0.1f, 0.5f, 1.0f); // Blue
    public static readonly Color AutoColor = new(0.2f, 0.8f, 0.4f); // Green
    public static readonly Color OffColor = new(0.3f, 0.3f, 0.3f);  // Gray
    public static readonly Color ErrorColor = new(1.0f, 0.1f, 0.1f); // Red

    /// <summary>Get the glow color for a thermostat mode.</summary>
    public static Color GetModeColor(string mode) => mode.ToLowerInvariant() switch
    {
        "heat" => HeatColor,
        "cool" => CoolColor,
        "auto" => AutoColor,
        "off" => OffColor,
        _ => OffColor
    };

    /// <summary>Get whether the device should show active glow.</summary>
    public static bool IsModeActive(string mode) =>
        mode.ToLowerInvariant() is "heat" or "cool" or "auto";

    /// <summary>
    /// Calculate glow intensity based on how hard the HVAC is working.
    /// Higher delta between current and target = more intense glow.
    /// </summary>
    public static float CalculateGlowIntensity(float currentTemp, float targetTemp, string mode)
    {
        if (mode.ToLowerInvariant() == "off") return 0f;
        var delta = Math.Abs(targetTemp - currentTemp);
        return Math.Clamp(delta * 0.2f, 0.5f, 3.0f);
    }

    /// <summary>
    /// Calculate energy flow level (0-1) for the energy overlay shader.
    /// Maps energy usage to visual intensity.
    /// </summary>
    public static float CalculateEnergyLevel(float energyUsage) =>
        Math.Clamp(energyUsage, 0f, 1f);

    /// <summary>
    /// Get the pulse speed based on HVAC activity.
    /// Higher energy = faster pulse.
    /// </summary>
    public static float CalculatePulseSpeed(float energyUsage) =>
        0.5f + energyUsage * 2.0f;

    /// <summary>
    /// Build a complete set of shader parameters for the device_state_glow shader.
    /// </summary>
    public static Dictionary<string, object> BuildShaderParams(
        string mode, float currentTemp, float targetTemp,
        float energyUsage, bool isSelected, bool isError)
    {
        var color = GetModeColor(mode);
        return new Dictionary<string, object>
        {
            ["device_color"] = Color.White,
            ["glow_color"] = color,
            ["glow_intensity"] = CalculateGlowIntensity(currentTemp, targetTemp, mode),
            ["pulse_speed"] = CalculatePulseSpeed(energyUsage),
            ["is_active"] = IsModeActive(mode),
            ["is_error"] = isError,
            ["is_selected"] = isSelected
        };
    }
}
