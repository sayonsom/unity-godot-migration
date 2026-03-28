// =============================================================================
// ThermostatTests.cs — Phase 2 tests for thermostat vertical slice
// Tests MockDeviceProvider, DeviceVisualizationManager, and voice commands
// =============================================================================

using SmartThings.Abstraction;
using SmartThings.Abstraction.Models;
using SmartThings.Godot.Data;
using Xunit;

namespace SmartThings.Tests;

public class MockDeviceProviderTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        var provider = new MockDeviceProvider();

        Assert.Equal(72f, provider.CurrentTemperature);
        Assert.Equal(72f, provider.TargetTemperature);
        Assert.Equal("cool", provider.Mode);
        Assert.Equal(0.3f, provider.EnergyUsage);
    }

    [Fact]
    public void GetThermostat_ReturnsValidDevice()
    {
        var provider = new MockDeviceProvider();
        var device = provider.GetThermostat();

        Assert.Equal("thermostat-001", device.DeviceId);
        Assert.Equal("Living Room Thermostat", device.Label);
        Assert.Equal(DeviceCategory.Thermostat, device.Category);
        Assert.Equal(DeviceStatus.Online, device.Status);
        Assert.True(device.Capabilities.ContainsKey("thermostatMode"));
        Assert.True(device.Capabilities.ContainsKey("temperature"));
    }

    [Fact]
    public void SetMode_ChangesMode()
    {
        var provider = new MockDeviceProvider();
        provider.SetMode("heat");

        Assert.Equal("heat", provider.Mode);
    }

    [Fact]
    public void SetMode_FiresStateChangedEvent()
    {
        var provider = new MockDeviceProvider();
        DeviceStateChangedEvent? received = null;
        provider.OnDeviceStateChanged += evt => received = evt;

        provider.SetMode("heat");

        Assert.NotNull(received);
        Assert.Equal("thermostatMode", received.CapabilityId);
        Assert.Equal("cool", received.OldValue);
        Assert.Equal("heat", received.NewValue);
    }

    [Fact]
    public void SetTargetTemperature_ClampsToRange()
    {
        var provider = new MockDeviceProvider();

        provider.SetTargetTemperature(50f); // Below min
        Assert.Equal(60f, provider.TargetTemperature);

        provider.SetTargetTemperature(100f); // Above max
        Assert.Equal(90f, provider.TargetTemperature);

        provider.SetTargetTemperature(75f); // In range
        Assert.Equal(75f, provider.TargetTemperature);
    }

    [Fact]
    public void SetTargetTemperature_FiresStateChangedEvent()
    {
        var provider = new MockDeviceProvider();
        DeviceStateChangedEvent? received = null;
        provider.OnDeviceStateChanged += evt => received = evt;

        provider.SetTargetTemperature(78f);

        Assert.NotNull(received);
        Assert.Equal("coolingSetpoint", received.CapabilityId); // Default mode is cool
    }

    [Fact]
    public void SetTargetTemperature_UsesHeatingSetpointInHeatMode()
    {
        var provider = new MockDeviceProvider();
        provider.SetMode("heat");

        DeviceStateChangedEvent? received = null;
        provider.OnDeviceStateChanged += evt => received = evt;

        provider.SetTargetTemperature(70f);

        Assert.NotNull(received);
        Assert.Equal("heatingSetpoint", received.CapabilityId);
    }

    [Fact]
    public void SimulateStep_OffMode_DriftsTowardAmbient()
    {
        var provider = new MockDeviceProvider();
        provider.SetMode("off");

        // Ambient is 75°F, starting at 72°F — should drift upward
        var initialTemp = provider.CurrentTemperature;
        for (int i = 0; i < 1000; i++)
            provider.SimulateStep(0.1f);

        // Temperature should move toward 75°F
        Assert.True(provider.CurrentTemperature > initialTemp,
            $"Expected temp to rise from {initialTemp}, got {provider.CurrentTemperature}");
    }

    [Fact]
    public void SimulateStep_ActiveMode_MovesTowardTarget()
    {
        var provider = new MockDeviceProvider();
        provider.SetTargetTemperature(80f);

        // Simulate many steps
        for (int i = 0; i < 1000; i++)
            provider.SimulateStep(0.1f);

        // Should be closer to 80 than starting 72
        Assert.True(provider.CurrentTemperature > 72f,
            $"Expected temp to rise toward 80, got {provider.CurrentTemperature}");
    }
}

public class DeviceVisualizationManagerTests
{
    [Theory]
    [InlineData("heat", 1.0f, 0.4f, 0.1f)]
    [InlineData("cool", 0.1f, 0.5f, 1.0f)]
    [InlineData("auto", 0.2f, 0.8f, 0.4f)]
    [InlineData("off", 0.3f, 0.3f, 0.3f)]
    public void GetModeColor_ReturnsCorrectColor(string mode, float r, float g, float b)
    {
        var color = DeviceVisualizationManager.GetModeColor(mode);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    [Theory]
    [InlineData("heat", true)]
    [InlineData("cool", true)]
    [InlineData("auto", true)]
    [InlineData("off", false)]
    [InlineData("unknown", false)]
    public void IsModeActive_ReturnsCorrectly(string mode, bool expected)
    {
        Assert.Equal(expected, DeviceVisualizationManager.IsModeActive(mode));
    }

    [Fact]
    public void CalculateGlowIntensity_OffMode_ReturnsZero()
    {
        var intensity = DeviceVisualizationManager.CalculateGlowIntensity(72f, 80f, "off");
        Assert.Equal(0f, intensity);
    }

    [Fact]
    public void CalculateGlowIntensity_LargeDelta_ClampsToMax()
    {
        // Delta of 20 * 0.2 = 4.0, clamped to 3.0
        var intensity = DeviceVisualizationManager.CalculateGlowIntensity(60f, 80f, "heat");
        Assert.Equal(3.0f, intensity);
    }

    [Fact]
    public void CalculateGlowIntensity_SmallDelta_ClampsToMin()
    {
        // Delta of 1 * 0.2 = 0.2, clamped to 0.5
        var intensity = DeviceVisualizationManager.CalculateGlowIntensity(71f, 72f, "cool");
        Assert.Equal(0.5f, intensity);
    }

    [Fact]
    public void CalculateEnergyLevel_ClampsTo01()
    {
        Assert.Equal(0f, DeviceVisualizationManager.CalculateEnergyLevel(-0.5f));
        Assert.Equal(0.5f, DeviceVisualizationManager.CalculateEnergyLevel(0.5f));
        Assert.Equal(1f, DeviceVisualizationManager.CalculateEnergyLevel(1.5f));
    }

    [Fact]
    public void CalculatePulseSpeed_ScalesWithEnergy()
    {
        Assert.Equal(0.5f, DeviceVisualizationManager.CalculatePulseSpeed(0f));
        Assert.Equal(1.5f, DeviceVisualizationManager.CalculatePulseSpeed(0.5f));
        Assert.Equal(2.5f, DeviceVisualizationManager.CalculatePulseSpeed(1.0f));
    }

    [Fact]
    public void BuildShaderParams_ContainsAllExpectedKeys()
    {
        var result = DeviceVisualizationManager.BuildShaderParams(
            "cool", 72f, 75f, 0.5f, false, false);

        Assert.True(result.ContainsKey("device_color"));
        Assert.True(result.ContainsKey("glow_color"));
        Assert.True(result.ContainsKey("glow_intensity"));
        Assert.True(result.ContainsKey("pulse_speed"));
        Assert.True(result.ContainsKey("is_active"));
        Assert.True(result.ContainsKey("is_error"));
        Assert.True(result.ContainsKey("is_selected"));
    }

    [Fact]
    public void BuildShaderParams_SetsCorrectValues()
    {
        var result = DeviceVisualizationManager.BuildShaderParams(
            "heat", 70f, 80f, 0.6f, true, true);

        Assert.Equal(DeviceVisualizationManager.HeatColor, result["glow_color"]);
        Assert.Equal(true, result["is_active"]);
        Assert.Equal(true, result["is_error"]);
        Assert.Equal(true, result["is_selected"]);
    }

    [Fact]
    public void GetModeColor_CaseInsensitive()
    {
        var lower = DeviceVisualizationManager.GetModeColor("heat");
        var upper = DeviceVisualizationManager.GetModeColor("HEAT");
        var mixed = DeviceVisualizationManager.GetModeColor("Heat");

        Assert.Equal(lower, upper);
        Assert.Equal(lower, mixed);
    }
}
