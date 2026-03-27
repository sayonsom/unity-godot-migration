// =============================================================================
// MainMenuController.cs — Main menu button handler
// =============================================================================

using GodotNative = Godot;

namespace SmartThings.Godot.Scripts;

public partial class MainMenuController : GodotNative.Control
{
    public override void _Ready()
    {
        var thermostatBtn = GetNode<GodotNative.Button>("VBox/ThermostatBtn");
        var apartmentBtn = GetNode<GodotNative.Button>("VBox/ApartmentBtn");
        var homeMapBtn = GetNode<GodotNative.Button>("VBox/HomeMapBtn");

        thermostatBtn.Pressed += () =>
            GetTree().ChangeSceneToFile("res://Scenes/ThermostatScene.tscn");

        apartmentBtn.Pressed += () =>
            GetTree().ChangeSceneToFile("res://Scenes/ApartmentScene.tscn");

        homeMapBtn.Pressed += () =>
            GetTree().ChangeSceneToFile("res://Scenes/HomeMapScene.tscn");
    }
}
