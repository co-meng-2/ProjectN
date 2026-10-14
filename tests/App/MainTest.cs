using System.Threading.Tasks;
using Godot;
using VampireSurvivor.App;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Meta;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Actors;
using VampireSurvivor.Tests.Meta;
using VampireSurvivor.Tests.Support;
using VampireSurvivor.Ui;

namespace VampireSurvivor.Tests.App;

public class MainTest : TestCase
{
	public const string Dir = "user://test_main";
	public const string Path = Dir + "/save.json";

	public override void BeforeEach() => SaveFiles.Reset(Dir);

	public override void AfterEach() => SaveFiles.Reset(Dir);

	/// <summary>A short stage without waves or events (the shipped schedule needs the full 180 s stage).</summary>
	public static StageDef ShortStage()
	{
		StageDef stage = DefFactory.Fresh<StageDef>("res://data/stages/stage_01.tres");
		stage.Duration = 30f;
		stage.Waves = null;
		stage.Events = null;
		return stage;
	}

	/// <summary>
	/// The real stage screen with a scratch save and a recording router. <paramref name="stage"/> null means "no params
	/// were handed over": Main then falls back to the shipped stage_01.
	/// </summary>
	public static Main MakeMain(TestCase test, SaveService service, SceneRouterProbe? router = null, StageDef? stage = null, SkillTreeDef? tree = null)
	{
		var main = GD.Load<PackedScene>(SceneRouter.Routes[SceneRouter.Route.Stage]).Instantiate<Main>();
		main.SaveService = service;
		main.Router = router ?? test.Adopt(new SceneRouterProbe());
		main.StageDef = stage;
		main.SkillTree = tree;
		return test.Adopt(main);
	}

	private SaveService Service() => SaveServiceTest.Service(this, Path);

	private Main Short(SaveService? service = null, SceneRouterProbe? router = null) => MakeMain(this, service ?? Service(), router, ShortStage());

	private static RunResult Result(RunOutcome outcome) => new() { Outcome = outcome };

	private static InputEventAction Action(StringName action) => new() { Action = action, Pressed = true };

	private static InputEventKey Key(Key keycode) => new() { Keycode = keycode, Pressed = true };

	public void TestTheDefaultStageIsTheShippedStageWhenNoParamsAreGiven()
	{
		Assert.Eq(MakeMain(this, Service()).StageDef!.Id.ToString(), "stage_01");
	}

	public void TestTheStageComesFromTheRouterParams()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		StageDef stage = ShortStage();
		router.Go(SceneRouter.Route.Stage, new() { [SceneRouter.ParamStageDef] = stage });
		Assert.True(MakeMain(this, Service(), router).StageDef == stage);
	}

	public void TestReadyStartsARunAndBindsTheHud()
	{
		Main main = Short();
		Assert.NotNull(main.Stage!.RunState, "the run started");
		Assert.Eq(main.Hud!.WalletLabel.Text, "Gold 200   Stone 0   Ticket 0");
		Assert.Null(main.Result);
	}

	public void TestACollapsedBuildingFailsTheRunThroughTheStageSignal()
	{
		SaveService service = Service();
		Main main = Short(service);
		ActorFixture.Kill(this, main.Stage!.Building.Entity);
		Assert.Eq(main.Result!.Outcome, RunOutcome.Failed);
		Assert.True(main.Hud!.ResultLabel.Visible);
		Assert.Eq(service.Data.Gem, 0L);
	}

	public void TestAClearedRunCreditsTheStageRewardAndSavesIt()
	{
		SaveService service = Service();
		Main main = Short(service);
		int reward = main.StageDef!.GemReward;
		Assert.True(reward > 0);
		main.OnStageFinished(Result(RunOutcome.Cleared));
		Assert.Eq(service.Data.Gem, (long)reward);
		Assert.Eq(main.Result!.Gem, reward, "the result screen shows the gem");
		Assert.Eq(SaveStore.LoadOrDefault(Path).Gem, (long)reward, "persisted to disk");
	}

	public void TestAFailedRunCreditsNothing()
	{
		SaveService service = Service();
		Short(service).OnStageFinished(Result(RunOutcome.Failed));
		Assert.Eq(service.Data.Gem, 0L);
	}

	public void TestFinishingTwiceCreditsAndRecordsOnlyOnce()
	{
		SaveService service = Service();
		Main main = Short(service);
		main.OnStageFinished(Result(RunOutcome.Cleared));
		main.OnStageFinished(Result(RunOutcome.Cleared));
		Assert.Eq(service.Data.Gem, (long)main.StageDef!.GemReward);
		Assert.Eq(service.Data.ClearedStages.Count, 1);
	}

	public void TestAClearedRunRecordsTheClearAndSavesIt()
	{
		SaveService service = Service();
		Short(service).OnStageFinished(Result(RunOutcome.Cleared));
		Assert.SequenceEq(service.Data.ClearedStages, new[] { "stage_01" });
		Assert.SequenceEq(SaveStore.LoadOrDefault(Path).ClearedStages, new[] { "stage_01" }, "persisted to disk");
	}

	public void TestAFailedRunRecordsNoClear()
	{
		SaveService service = Service();
		Short(service).OnStageFinished(Result(RunOutcome.Failed));
		Assert.Eq(service.Data.ClearedStages.Count, 0);
	}

	public void TestConfirmAfterAResultReturnsToTheHub()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		Main main = Short(router: router);
		main.OnStageFinished(Result(RunOutcome.Cleared));
		main._UnhandledInput(Action(InputActions.Confirm));
		Assert.Eq(router.LastRoute, SceneRouter.Route.Hub);
	}

	public void TestSpaceIsTheDashKeyAndDoesNotLeaveTheResultScreen()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		Main main = Short(router: router);
		main.OnStageFinished(Result(RunOutcome.Failed));
		main._UnhandledInput(Key(Godot.Key.Space));
		Assert.Eq(router.ChangedTo.Count, 0, "mashing dash must not skip the result");
	}

	public void TestEnterContinuesFromTheResultScreen()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		Main main = Short(router: router);
		main.OnStageFinished(Result(RunOutcome.Failed));
		main._UnhandledInput(Key(Godot.Key.Enter));
		Assert.Eq(router.LastRoute, SceneRouter.Route.Hub);
	}

	public void TestConfirmDuringARunDoesNothing()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		Short(router: router)._UnhandledInput(Action(InputActions.Confirm));
		Assert.Eq(router.ChangedTo.Count, 0);
	}

	public void TestRestartDuringARunDoesNothing()
	{
		Main main = Short();
		StageRoot first = main.Stage!;
		main._UnhandledInput(Action(InputActions.Restart));
		Assert.True(main.Stage == first, "R is only honoured on the result screen");
	}

	public async Task TestRestartAfterAResultStartsACleanRun()
	{
		Main main = Short();
		StageRoot first = main.Stage!;
		first.RunState!.Wallet.Add(CurrencyKind.Gold, 50);
		first.Factory.Spawn(GD.Load<EnemyDef>("res://data/enemies/orc.tres"), new Vector2(300f, 0f));
		Assert.Eq(Host.GetTree().GetNodesInGroup(Groups.Enemies).Count, 1);
		ActorFixture.Kill(this, first.Building.Entity);
		main._UnhandledInput(Action(InputActions.Restart));
		Assert.Eq(Host.GetTree().GetNodesInGroup(Groups.Enemies).Count, 0, "the old run's enemies left the group at once");
		Assert.True(main.Stage != first, "a new stage");
		Assert.False(first.IsInsideTree(), "the old stage left the tree at once");
		Assert.Null(main.Result);
		Assert.Eq(main.Stage!.RunState!.Wallet.Gold, 200);
		Assert.False(main.Hud!.ResultLabel.Visible);
		await NextFrame();
		Assert.False(GodotObject.IsInstanceValid(first), "the old stage was freed");
	}

	public void TestAPromoteRequestFromTheBarBuysASlot()
	{
		Main main = Short();
		main.Hud!.AbilityBar.EmitSignal(AbilityBar.SignalName.PromoteRequested, 0);
		Assert.False(main.Stage!.Loadout!.Slot(0)!.IsEmpty, "bought");
		Assert.Eq(main.Stage.RunState!.Wallet.Gold, 100, "200 - 100");
	}

	public void TestShopRequestsAfterTheResultAreIgnored()
	{
		Main main = Short();
		ActorFixture.Kill(this, main.Stage!.Building.Entity);
		main.Hud!.AbilityBar.EmitSignal(AbilityBar.SignalName.PromoteRequested, 0);
		main.Hud.AbilityBar.EmitSignal(AbilityBar.SignalName.RerollRequested, 0);
		Assert.True(main.Stage.Loadout!.Slot(0)!.IsEmpty, "the shop closes with the run");
		Assert.Eq(main.Stage.RunState!.Wallet.Gold, 200);
	}

	public void TestTheHudFollowsTheStageEventsAndTheBoss()
	{
		StageDef stage = ShortStage();
		stage.TrickleEnemies = [];
		stage.Events = DefFactory.Schedule([DefFactory.Event(DefFactory.Packed(new ProbeEvent { Endless = true }), 1f)], 1, 5f, 10f, 10f);
		stage.Boss = DefFactory.Boss(GD.Load<PackedScene>(BossActorTest.BossScene), new ProbePatternDef());
		Main main = MakeMain(this, Service(), null, stage);
		main.Stage!.Clock.SetPhysicsProcess(false);
		Assert.Eq(main.Hud!.Timeline.EventMarks.Length, 1, "the rolled event time is on the timeline");
		main.Stage.Clock.Step(11f);
		Assert.True(main.Hud.EventBanner.Visible, "the banner shows the running event");
		main.Stage.Clock.Step(20f);
		Assert.True(main.Hud.BossBar.Visible, "the boss bar shows the boss");
		Assert.True(main.Hud.Timeline.IsShowingBoss);
		Assert.Eq(main.Hud.EventBanner.TitleText, "Probe: Interrupted", "the boss interrupted the event");
	}

	public void TestKillingTheBossClearsTheRunAndPaysGem()
	{
		StageDef stage = ShortStage();
		stage.TrickleEnemies = [];
		SaveService service = Service();
		Main main = MakeMain(this, service, null, stage);
		main.Stage!.Clock.SetPhysicsProcess(false);
		main.Stage.Clock.Step(31f);
		ActorFixture.Kill(this, main.Stage.CurrentBoss!.Entity);
		Assert.Eq(main.Result!.Outcome, RunOutcome.Cleared);
		Assert.Eq(service.Data.Gem, (long)stage.GemReward);
		Assert.SequenceEq(service.Data.ClearedStages, new[] { "stage_01" });
	}

	public void TestBoughtSkillLevelsShapeTheProfileTheStageStartsWith()
	{
		SaveService service = Service();
		service.Data.SetLevel("vitality", 2);
		service.Data.SetLevel("extra_slot", 1);
		Main main = Short(service);
		Assert.AlmostEq(main.Stage!.Player.Health.Maximum(), 120.0, 0.001, "base 100 + vitality level 2 (+20)");
		Assert.AlmostEq(main.Stage.Player.Health.Current(), 120.0, 0.001, "starts at full modified health");
		Assert.Eq(main.Stage.Loadout!.Size, 7, "6 base slots + extra slot");
	}

	public void TestWithoutPurchasesTheRunUsesTheBaseProfile()
	{
		Main main = Short();
		Assert.AlmostEq(main.Stage!.Player.Health.Maximum(), 100.0, 0.001);
		Assert.Eq(main.Stage.Loadout!.Size, 6);
	}

	public void TestAnInvalidSkillTreeFallsBackToTheBaseProfile()
	{
		SaveService service = Service();
		service.Data.SetLevel("vitality", 3);
		Main main = MakeMain(this, service, null, ShortStage(), new SkillTreeDef());
		Assert.AlmostEq(main.Stage!.Player.Health.Maximum(), 100.0, 0.001, "no bonuses from a broken tree");
		Assert.Eq(main.Stage.Loadout!.Size, 6);
	}
}
