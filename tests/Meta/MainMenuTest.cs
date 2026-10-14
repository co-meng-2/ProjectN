using Godot;
using VampireSurvivor.App;
using VampireSurvivor.Meta.Menu;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Meta;

public class MainMenuTest : TestCase
{
	private MainMenu Menu(SceneRouterProbe router)
	{
		var menu = GD.Load<PackedScene>(SceneRouter.Routes[SceneRouter.Route.MainMenu]).Instantiate<MainMenu>();
		menu.Router = router;
		return Adopt(menu);
	}

	public void TestStartGoesToTheHubWithoutParams()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		Menu(router).StartButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Eq(router.LastRoute, SceneRouter.Route.Hub);
		Assert.Eq(router.TakeParams().Count, 0);
	}

	public void TestSettingsGoesToTheSettingsScreen()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		Menu(router).SettingsButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Eq(router.LastRoute, SceneRouter.Route.Settings);
	}

	public void TestTheMenuShowsAQuitButtonAndFocusesStart()
	{
		MainMenu menu = Menu(Adopt(new SceneRouterProbe()));
		Assert.NotNull(menu.QuitButton);
		Assert.True(menu.StartButton.HasFocus(), "Enter starts the game");
	}
}
