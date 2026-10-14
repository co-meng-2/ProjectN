using Godot;
using VampireSurvivor.App;

namespace VampireSurvivor.Meta.Menu;

/// <summary>Start / Settings / Quit. Holds no state: it asks the router to move on. Start opens the hub.</summary>
public partial class MainMenu : Control
{
	[Export]
	public Button StartButton { get; set; } = null!;

	[Export]
	public Button SettingsButton { get; set; } = null!;

	[Export]
	public Button QuitButton { get; set; } = null!;

	/// <summary>Defaults to the SceneRouter autoload in _Ready; tests set it first.</summary>
	public SceneRouter? Router { get; set; }

	public override void _Ready()
	{
		Router ??= Autoloads.Router(this);
		StartButton.Pressed += () => Router.Go(SceneRouter.Route.Hub);
		SettingsButton.Pressed += () => Router.Go(SceneRouter.Route.Settings);
		QuitButton.Pressed += () => GetTree().Quit();
		StartButton.GrabFocus();
	}
}
