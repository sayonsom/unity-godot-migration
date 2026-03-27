// =============================================================================
// HomeMapSceneController.cs — Root controller for the Home Map scene
// Wires assembler, camera, UI, and pins together; loads mock data
// =============================================================================

using GodotNative = Godot;
using SmartThings.Godot.Data;

namespace SmartThings.Godot.Scripts.HomeMap;

/// <summary>
/// Root controller for the HomeMapScene. Initializes all components
/// and wires their signals together.
/// </summary>
public partial class HomeMapSceneController : GodotNative.Node3D
{
    private HomeMapAssembler? _assembler;
    private DevicePinManager? _pinManager;
    private IsometricCameraController? _camera;
    private HomeMapUI? _ui;

    public override void _Ready()
    {
        _assembler = GetNode<HomeMapAssembler>("HomeRoot");
        _pinManager = GetNode<DevicePinManager>("DevicePins");
        _camera = GetNode<IsometricCameraController>("IsometricCamera");
        _ui = GetNode<HomeMapUI>("UIOverlay/HomeMapUI");

        // Setup environment
        SetupEnvironment();

        // Wire UI signals
        _ui.ResetViewPressed += () => _camera?.ResetView();
        _assembler.RoomSelected += (roomId, roomName) => _ui.ShowRoomInfo(roomId, roomName);

        // Load mock home data
        var home = MockHomeProvider.CreateSampleHome();
        _ui.SetHome(home);
        _assembler.BuildHome(home);

        GodotNative.GD.Print("[HomeMapScene] Loaded with mock home data.");
    }

    private void SetupEnvironment()
    {
        var envNode = GetNode<GodotNative.WorldEnvironment>("WorldEnvironment");

        var env = new GodotNative.Environment();
        // Light background like SmartThings app
        env.BackgroundMode = GodotNative.Environment.BGMode.Color;
        env.BackgroundColor = new GodotNative.Color(0.96f, 0.96f, 0.98f, 1.0f);

        // Bright ambient light so rooms look clean and colorful
        env.AmbientLightSource = GodotNative.Environment.AmbientSource.Color;
        env.AmbientLightColor = new GodotNative.Color(1.0f, 1.0f, 1.0f, 1.0f);
        env.AmbientLightEnergy = 0.8f;

        env.TonemapMode = GodotNative.Environment.ToneMapper.Linear;

        envNode.Environment = env;
    }
}
