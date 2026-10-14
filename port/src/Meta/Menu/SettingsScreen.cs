using Godot;
using VampireSurvivor.App;

namespace VampireSurvivor.Meta.Menu;

/// <summary>Master-volume slider. Changes apply immediately; leaving the screen saves once.</summary>
public partial class SettingsScreen : Control
{
	[Export]
	public HSlider VolumeSlider { get; set; } = null!;

	[Export]
	public Button BackButton { get; set; } = null!;

	/// <summary>Default to the autoloads in _Ready; tests set them first.</summary>
	public SaveService? SaveService { get; set; }

	public SceneRouter? Router { get; set; }

	public override void _Ready()
	{
		SaveService ??= Autoloads.Save(this);
		Router ??= Autoloads.Router(this);
		VolumeSlider.Value = SaveService.Data.Settings.MasterVolume;
		VolumeSlider.ValueChanged += OnVolumeChanged;
		BackButton.Pressed += OnBackPressed;
		BackButton.GrabFocus();
	}

	private void OnVolumeChanged(double value)
	{
		SaveService!.Data.Settings.MasterVolume = (float)value;
		AudioSettings.Apply(SaveService.Data.Settings);
	}

	private void OnBackPressed()
	{
		SaveService!.Save();
		Router!.Go(SceneRouter.Route.MainMenu);
	}
}
