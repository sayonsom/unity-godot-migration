// =============================================================================
// MockDeviceProvider.cs — Simulated SmartThings device data
// Provides thermostat state without requiring live API credentials
// =============================================================================

using SmartThings.Abstraction;
using SmartThings.Abstraction.Models;

namespace SmartThings.Godot.Data;

/// <summary>
/// Mock data provider for development and testing.
/// Simulates a SmartThings thermostat with realistic state transitions.
/// Replace with real SmartThings API calls when credentials are available.
/// </summary>
public class MockDeviceProvider
{
    private readonly SmartDevice _thermostat;
    private float _currentTemperature = 72f;
    private float _targetTemperature = 72f;
    private string _mode = "cool"; // heat, cool, auto, off
    private float _energyUsage = 0.3f;
    private readonly Random _rng = new();

    public event Action<DeviceStateChangedEvent>? OnDeviceStateChanged;

    public MockDeviceProvider()
    {
        _thermostat = new SmartDevice
        {
            DeviceId = "thermostat-001",
            Name = "Smart Thermostat",
            Label = "Living Room Thermostat",
            Category = DeviceCategory.Thermostat,
            RoomId = "room-living",
            RoomName = "Living Room",
            ManufacturerName = "Samsung",
            ModelName = "SmartThings Thermostat Pro",
            Model3DPath = null, // Built programmatically
            Status = DeviceStatus.Online,
            Capabilities = new Dictionary<string, DeviceCapabilityState>
            {
                ["thermostatMode"] = new("thermostatMode", "thermostatMode", "cool"),
                ["temperature"] = new("temperatureMeasurement", "temperature", 72f, "F"),
                ["coolingSetpoint"] = new("thermostatCoolingSetpoint", "coolingSetpoint", 72f, "F"),
                ["heatingSetpoint"] = new("thermostatHeatingSetpoint", "heatingSetpoint", 68f, "F"),
                ["humidity"] = new("relativeHumidityMeasurement", "humidity", 45, "%"),
                ["energyUsage"] = new("powerMeter", "power", 150, "W")
            }
        };
    }

    public SmartDevice GetThermostat() => _thermostat;

    public float CurrentTemperature => _currentTemperature;
    public float TargetTemperature => _targetTemperature;
    public string Mode => _mode;
    public float EnergyUsage => _energyUsage;

    /// <summary>Set thermostat mode (heat/cool/auto/off).</summary>
    public void SetMode(string mode)
    {
        var oldMode = _mode;
        _mode = mode;

        OnDeviceStateChanged?.Invoke(new DeviceStateChangedEvent(
            _thermostat.DeviceId, "thermostatMode", "thermostatMode",
            oldMode, mode, DateTimeOffset.UtcNow));
    }

    /// <summary>Set target temperature.</summary>
    public void SetTargetTemperature(float temp)
    {
        var oldTemp = _targetTemperature;
        _targetTemperature = Math.Clamp(temp, 60f, 90f);

        var capability = _mode == "heat" ? "heatingSetpoint" : "coolingSetpoint";
        OnDeviceStateChanged?.Invoke(new DeviceStateChangedEvent(
            _thermostat.DeviceId, capability, capability,
            oldTemp, _targetTemperature, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Simulate temperature drift and energy usage.
    /// Call this each frame or on a timer.
    /// </summary>
    public void SimulateStep(float deltaSeconds)
    {
        if (_mode == "off")
        {
            // Drift toward ambient (75°F)
            _currentTemperature += (75f - _currentTemperature) * 0.001f * deltaSeconds;
            _energyUsage = Math.Max(0f, _energyUsage - 0.01f * deltaSeconds);
        }
        else
        {
            // Move current temp toward target
            var diff = _targetTemperature - _currentTemperature;
            if (Math.Abs(diff) > 0.1f)
            {
                _currentTemperature += Math.Sign(diff) * 0.05f * deltaSeconds;
                _energyUsage = Math.Min(1f, 0.3f + Math.Abs(diff) * 0.05f);
            }
            else
            {
                _energyUsage = Math.Max(0.1f, _energyUsage - 0.005f * deltaSeconds);
            }
        }

        // Add small random fluctuation
        _currentTemperature += (_rng.NextSingle() - 0.5f) * 0.02f * deltaSeconds;
    }
}
