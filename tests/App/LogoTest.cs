using Godot;
using VampireSurvivor.App;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.App;

public class LogoTest : TestCase
{
	private static InputEventKey KeyPress(bool pressed = true) => new() { Pressed = pressed, Keycode = Key.Space };

	private Logo MakeLogo(SceneRouterProbe router)
	{
		var logo = GD.Load<PackedScene>(SceneRouter.Routes[SceneRouter.Route.Logo]).Instantiate<Logo>();
		logo.Router = router;
		return Adopt(logo);
	}

	public void TestAKeyPressSkipsToTheMainMenu()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		MakeLogo(router)._UnhandledInput(KeyPress());
		Assert.Eq(router.LastRoute, SceneRouter.Route.MainMenu);
	}

	public void TestProceedingTwiceChangesSceneOnce()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		Logo logo = MakeLogo(router);
		logo._UnhandledInput(KeyPress());
		logo._UnhandledInput(KeyPress());
		Assert.Eq(router.ChangedTo.Count, 1);
	}

	public void TestAKeyReleaseDoesNotSkip()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		MakeLogo(router)._UnhandledInput(KeyPress(false));
		Assert.Eq(router.ChangedTo.Count, 0);
	}
}
