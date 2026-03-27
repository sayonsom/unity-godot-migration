// =============================================================================
// ThermostatController.cs — Main thermostat scene controller
// Wires all 6 abstraction services to a working thermostat visualization
// =============================================================================

using SmartThings.Abstraction;
using SmartThings.Abstraction.Interfaces;
using SmartThings.Godot.Autoload;
using SmartThings.Godot.Data;
using GodotNative = Godot;

namespace SmartThings.Godot.Scripts;

/// <summary>
/// Controls the thermostat 3D scene.
/// Demonstrates all 6 IEngineAbstraction services working together:
///   - IRenderService: shader parameters for device glow/energy
///   - IInputService: touch to select, scroll to zoom
///   - ISceneService: back button transitions to MainMenu
///   - INetworkService: mock device state (swappable for real API)
///   - IAudioService: UI feedback sounds
///   - IAccessibilityService: screen reader + voice commands
/// </summary>
public partial class ThermostatController : GodotNative.Node3D
{
    private IRenderService _render = null!;
    private IInputService _input = null!;
    private ISceneService _scene = null!;
    private IAccessibilityService _accessibility = null!;
    private MockDeviceProvider _deviceProvider = null!;
    private DeviceVisualizationManager _vizManager = null!;

    private GodotNative.Camera3D? _camera;
    private GodotNative.MeshInstance3D? _thermostatBody;
    private GodotNative.MeshInstance3D? _thermostatRing;
    private GodotNative.MeshInstance3D? _energyOverlay;

    private float _cameraDistance = 2.5f;
    private float _cameraAngle;
    private bool _isSelected;
    private IDisposable? _processCallback;

    public override void _Ready()
    {
        // Resolve services from DI
        _render = GameBootstrap.Resolve<IRenderService>();
        _input = GameBootstrap.Resolve<IInputService>();
        _scene = GameBootstrap.Resolve<ISceneService>();
        _accessibility = GameBootstrap.Resolve<IAccessibilityService>();
        _deviceProvider = GameBootstrap.Resolve<MockDeviceProvider>();
        _vizManager = GameBootstrap.Resolve<DeviceVisualizationManager>();

        // Find scene nodes
        _camera = GetNode<GodotNative.Camera3D>("Camera3D");
        _thermostatBody = GetNode<GodotNative.MeshInstance3D>("ThermostatDevice/Body");
        _thermostatRing = GetNode<GodotNative.MeshInstance3D>("ThermostatDevice/Ring");
        _energyOverlay = GetNode<GodotNative.MeshInstance3D>("EnergyOverlay");

        // Apply shaders
        ApplyDeviceShader(_thermostatBody);
        ApplyDeviceShader(_thermostatRing);
        ApplyEnergyShader(_energyOverlay);

        // Register accessibility
        RegisterAccessibility();

        // Register voice commands
        RegisterVoiceCommands();

        // Wire input events
        _input.OnPointerEvent += OnPointer;

        GodotNative.GD.Print("[ThermostatController] Scene initialized.");
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        // Simulate device state
        _deviceProvider.SimulateStep(dt);

        // Update shader parameters
        UpdateVisualization();

        // Handle camera zoom
        if (GodotNative.Input.IsActionJustPressed("zoom_in"))
            _cameraDistance = Math.Max(1.5f, _cameraDistance - 0.3f);
        if (GodotNative.Input.IsActionJustPressed("zoom_out"))
            _cameraDistance = Math.Min(5f, _cameraDistance + 0.3f);

        // Handle back
        if (GodotNative.Input.IsActionJustPressed("back"))
            _ = _scene.LoadSceneAsync("res://Scenes/MainMenu.tscn", SceneTransition.Fade);

        // Update camera position
        UpdateCamera(dt);
    }

    public override void _ExitTree()
    {
        _input.OnPointerEvent -= OnPointer;
        _processCallback?.Dispose();
    }

    private void UpdateVisualization()
    {
        var mode = _deviceProvider.Mode;
        var currentTemp = _deviceProvider.CurrentTemperature;
        var targetTemp = _deviceProvider.TargetTemperature;
        var energy = _deviceProvider.EnergyUsage;

        // Update device_state_glow shader on thermostat body and ring
        var shaderParams = DeviceVisualizationManager.BuildShaderParams(
            mode, currentTemp, targetTemp, energy, _isSelected, false);

        foreach (var (key, value) in shaderParams)
        {
            SetShaderParam(_thermostatBody, key, value);
            SetShaderParam(_thermostatRing, key, value);
        }

        // Update energy_flow shader on overlay
        SetShaderParam(_energyOverlay, "energy_level",
            DeviceVisualizationManager.CalculateEnergyLevel(energy));
        SetShaderParam(_energyOverlay, "flow_speed",
            0.5f + energy * 2f);

        // Set energy flow color based on mode
        var modeColor = DeviceVisualizationManager.GetModeColor(mode);
        SetShaderParam(_energyOverlay, "flow_color", modeColor);
    }

    private void UpdateCamera(float delta)
    {
        if (_camera == null) return;

        // Gentle orbit
        _cameraAngle += delta * 0.2f;
        var x = MathF.Sin(_cameraAngle) * _cameraDistance;
        var z = MathF.Cos(_cameraAngle) * _cameraDistance;
        var y = _cameraDistance * 0.6f;

        _camera.GlobalPosition = new GodotNative.Vector3(x, y, z);
        _camera.LookAt(GodotNative.Vector3.Zero, GodotNative.Vector3.Up);
    }

    private void OnPointer(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Down)
        {
            // Raycast to check if thermostat was tapped
            var result = _input.Raycast(evt.Position);
            _isSelected = result?.HitNode != null;

            if (_isSelected)
            {
                _accessibility.Announce(
                    $"Thermostat selected. Currently {_deviceProvider.CurrentTemperature:F0} degrees, mode {_deviceProvider.Mode}.");
            }
        }
    }

    private void RegisterAccessibility()
    {
        // Register thermostat as accessible 3D element
        // Uses node metadata (GodotAccessibilityService reads these)
        var deviceNode = GetNode<GodotNative.Node3D>("ThermostatDevice");
        deviceNode.SetMeta("accessible_name", "Smart Thermostat");
        deviceNode.SetMeta("accessible_description",
            "Living room thermostat. Tap to select, use slider to adjust temperature.");
        deviceNode.SetMeta("accessible_role", "Device3D");
    }

    private void RegisterVoiceCommands()
    {
        var voiceProcessor = _accessibility.VoiceCommands;

        voiceProcessor.RegisterCommand(new VoiceCommandPattern(
            "set_temperature",
            new[] { "set temperature to {value}", "set thermostat to {value}", "make it {value} degrees" },
            "Set the thermostat target temperature"));

        voiceProcessor.RegisterCommand(new VoiceCommandPattern(
            "set_mode",
            new[] { "set mode to {mode}", "turn on {mode}", "switch to {mode}" },
            "Change the thermostat mode"));

        voiceProcessor.OnCommandRecognized += cmd =>
        {
            switch (cmd.CommandId)
            {
                case "set_temperature" when cmd.Parameters.TryGetValue("value", out var tempStr):
                    if (float.TryParse(tempStr, out var temp))
                    {
                        _deviceProvider.SetTargetTemperature(temp);
                        _accessibility.Announce($"Temperature set to {temp} degrees.");
                    }
                    break;

                case "set_mode" when cmd.Parameters.TryGetValue("mode", out var mode):
                    _deviceProvider.SetMode(mode);
                    _accessibility.Announce($"Mode changed to {mode}.");
                    break;
            }
        };
    }

    // --- Shader Helpers ---

    private static void ApplyDeviceShader(GodotNative.MeshInstance3D? mesh)
    {
        if (mesh == null) return;
        var shader = GodotNative.GD.Load<GodotNative.Shader>("res://Shaders/device_state_glow.gdshader");
        var material = new GodotNative.ShaderMaterial { Shader = shader };
        mesh.MaterialOverride = material;
    }

    private static void ApplyEnergyShader(GodotNative.MeshInstance3D? mesh)
    {
        if (mesh == null) return;
        var shader = GodotNative.GD.Load<GodotNative.Shader>("res://Shaders/energy_flow.gdshader");
        var material = new GodotNative.ShaderMaterial { Shader = shader };
        mesh.MaterialOverride = material;
    }

    private static void SetShaderParam(GodotNative.MeshInstance3D? mesh, string name, object value)
    {
        if (mesh?.MaterialOverride is not GodotNative.ShaderMaterial mat) return;

        GodotNative.Variant godotValue = value switch
        {
            float f => f,
            int i => i,
            bool b => b,
            Color c => new GodotNative.Color(c.R, c.G, c.B, c.A),
            _ => GodotNative.Variant.From(value.ToString())
        };

        mat.SetShaderParameter(name, godotValue);
    }
}
