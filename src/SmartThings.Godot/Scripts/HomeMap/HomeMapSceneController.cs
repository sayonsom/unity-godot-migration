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
        _ui.BackPressed += OnBackPressed;
        _ui.ResetViewPressed += () => _camera?.ResetView();
        _assembler.RoomSelected += (roomId, roomName) => _ui.ShowRoomInfo(roomId, roomName);

        // Load mock home data
        var home = MockHomeProvider.CreateSampleHome();
        _ui.SetHome(home);
        _assembler.BuildHome(home);

        GodotNative.GD.Print("[HomeMapScene] Loaded with mock home data.");
    }

    public override void _UnhandledInput(GodotNative.InputEvent @event)
    {
        // Back button / Escape to return to main menu
        if (@event is GodotNative.InputEventKey key && key.Pressed && key.Keycode == GodotNative.Key.Escape)
        {
            GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
        }

        // Tap empty space to deselect
        if (@event is GodotNative.InputEventMouseButton mb
            && mb.Pressed && mb.ButtonIndex == GodotNative.MouseButton.Left)
        {
            // Will be handled by camera raycast — if no room hit, deselect
        }
    }

    private void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
    }

    private void SetupEnvironment()
    {
        var envNode = GetNode<GodotNative.WorldEnvironment>("WorldEnvironment");

        var sky = new GodotNative.Sky();
        var env = new GodotNative.Environment();
        env.BackgroundMode = GodotNative.Environment.BGMode.Color;
        env.BackgroundColor = new GodotNative.Color(0.95f, 0.95f, 0.97f, 1.0f);
        env.AmbientLightSource = GodotNative.Environment.AmbientSource.Color;
        env.AmbientLightColor = new GodotNative.Color(0.9f, 0.9f, 0.95f, 1.0f);
        env.AmbientLightEnergy = 0.7f;
        env.TonemapMode = GodotNative.Environment.ToneMapper.Aces;

        envNode.Environment = env;
    }
}
