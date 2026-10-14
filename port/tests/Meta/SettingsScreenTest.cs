using Godot;
using VampireSurvivor.App;
using VampireSurvivor.Meta;
using VampireSurvivor.Meta.Menu;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Meta;

public class SettingsScreenTest : TestCase
{
	private const string Dir = "user://test_settings_screen";
	private const string Path = Dir + "/save.json";

	private float _savedDb;

	private static float MasterDb => AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master"));

	public override void BeforeEach()
	{
		SaveFiles.Reset(Dir);
		_savedDb = MasterDb;
	}

	public override void AfterEach()
	{
		SaveFiles.Reset(Dir);
		AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), _savedDb);
	}

	private SettingsScreen Screen(SaveService service, SceneRouterProbe router)
	{
		var screen = GD.Load<PackedScene>(SceneRouter.Routes[SceneRouter.Route.Settings]).Instantiate<SettingsScreen>();
		screen.SaveService = service;
		screen.Router = router;
		return Adopt(screen);
	}

	public void TestTheSliderStartsAtTheSavedVolume()
	{
		SaveService service = SaveServiceTest.Service(this, Path);
		service.Data.Settings.MasterVolume = 0.3f;
		Assert.AlmostEq(Screen(service, Adopt(new SceneRouterProbe())).VolumeSlider.Value, 0.3, 0.001);
	}

	public void TestMovingTheSliderUpdatesTheModelAndTheAudioBus()
	{
		SaveService service = SaveServiceTest.Service(this, Path);
		Screen(service, Adopt(new SceneRouterProbe())).VolumeSlider.Value = 0.5;
		Assert.AlmostEq(service.Data.Settings.MasterVolume, 0.5, 0.001);
		Assert.AlmostEq(MasterDb, Mathf.LinearToDb(0.5f), 0.01);
	}

	public void TestBackSavesAndReturnsToTheMainMenu()
	{
		SaveService service = SaveServiceTest.Service(this, Path);
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		SettingsScreen screen = Screen(service, router);
		screen.VolumeSlider.Value = 0.25;
		screen.BackButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Eq(router.LastRoute, SceneRouter.Route.MainMenu);
		Assert.AlmostEq(SaveStore.LoadOrDefault(Path).Settings.MasterVolume, 0.25, 0.001, "persisted");
	}
}
