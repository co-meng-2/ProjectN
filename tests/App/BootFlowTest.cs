using Godot;
using VampireSurvivor.App;
using VampireSurvivor.Defs;
using VampireSurvivor.Meta;
using VampireSurvivor.Meta.Hub;
using VampireSurvivor.Meta.Menu;
using VampireSurvivor.Meta.SkillTree;
using VampireSurvivor.Tests.Meta;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.App;

/// <summary>
/// Walks the real scenes through the real router table and shipped catalog, with a scratch save:
/// menu -> hub -> stage_01 cleared -> hub shows stage_02 open; gem from a clear buys a skill for the next run.
/// </summary>
public class BootFlowTest : TestCase
{
	private const string Dir = "user://test_boot_flow";
	private const string Path = Dir + "/save.json";

	public override void BeforeEach() => SaveFiles.Reset(Dir);

	public override void AfterEach() => SaveFiles.Reset(Dir);

	private SaveService Service() => SaveServiceTest.Service(this, Path);

	private T Screen<T>(SceneRouter.Route route)
		where T : Node => GD.Load<PackedScene>(SceneRouter.Routes[route]).Instantiate<T>();

	private HubScreen Hub(SaveService service, SceneRouterProbe router)
	{
		var hub = Screen<HubScreen>(SceneRouter.Route.Hub);
		hub.SaveService = service;
		hub.Router = router;
		return Adopt(hub);
	}

	private static Button Play(HubScreen hub, int index) => hub.StageList.GetChild<StageCard>(index).PlayButton;

	public void TestClearingStage01FromTheHubPaysGemUnlocksStage02AndSurvivesARelaunch()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		SaveService service = Service();

		var menu = Screen<MainMenu>(SceneRouter.Route.MainMenu);
		menu.Router = router;
		Adopt(menu).StartButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Eq(router.LastRoute, SceneRouter.Route.Hub);

		HubScreen hub = Hub(service, router);
		Assert.False(Play(hub, 0).Disabled);
		Assert.True(Play(hub, 1).Disabled, "stage_02 starts locked");
		Play(hub, 0).EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Eq(router.LastRoute, SceneRouter.Route.Stage);

		// The probe never changes scenes; Main reads the params the router kept for it.
		Main main = MainTest.MakeMain(this, service, router);
		Assert.Eq(main.StageDef!.Id.ToString(), "stage_01");
		main.OnStageFinished(new RunResult { Outcome = RunOutcome.Cleared });
		Assert.Eq(service.Data.Gem, (long)main.StageDef.GemReward);

		SaveService relaunched = Service();
		Assert.SequenceEq(relaunched.Data.ClearedStages, new[] { "stage_01" }, "a fresh session reads the saved clear");
		Assert.False(Play(Hub(relaunched, router), 1).Disabled, "stage_02 is open after clearing stage_01");
	}

	public void TestGemFromAClearBuysASkillThatStrengthensTheNextRun()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		SaveService service = Service();

		Main first = MainTest.MakeMain(this, service, router, MainTest.ShortStage());
		first.OnStageFinished(new RunResult { Outcome = RunOutcome.Cleared });
		int reward = first.StageDef!.GemReward;
		Assert.Eq(service.Data.Gem, (long)reward);

		var screen = Screen<SkillTreeScreen>(SceneRouter.Route.SkillTree);
		screen.SaveService = service;
		screen.Router = router;
		Adopt(screen).NodeList.GetChild<SkillNodeRow>(0).BuyButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Eq(service.Data.LevelOf("vitality"), 1);
		Assert.Eq(service.Data.Gem, (long)(reward - 10), "vitality level 1 costs 10");

		Main second = MainTest.MakeMain(this, service, router, MainTest.ShortStage());
		Assert.AlmostEq(second.Stage!.Player.Health.Maximum(), 110.0, 0.001, "base 100 + vitality level 1 (+10)");
		Assert.AlmostEq(second.Stage.Player.Health.Current(), 110.0, 0.001);
	}

	public void TestEveryRouteHasASceneThatLoads()
	{
		foreach (string path in SceneRouter.Routes.Values)
		{
			Assert.True(ResourceLoader.Load(path) is PackedScene, path);
		}
	}
}
