using System.Threading.Tasks;
using Godot;
using VampireSurvivor.App;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Actors;
using VampireSurvivor.Tests.Support;
using VampireSurvivor.Ui;

namespace VampireSurvivor.Tests.App;

public class DevStageTest : TestCase
{
	private DevStage MakeDev()
	{
		var dev = GD.Load<PackedScene>("res://port/src/App/dev_stage.tscn").Instantiate<DevStage>();
		dev.StageDef = DefFactory.Fresh<StageDef>("res://port/data/stages/stage_01.tres");
		dev.StageDef.Duration = 30f;
		dev.StageDef.Waves = null;
		return Adopt(dev);
	}

	private static InputEventAction RestartPress() => new() { Action = InputActions.Restart, Pressed = true };

	public void TestReadyStartsARunAndBindsTheHud()
	{
		DevStage dev = MakeDev();
		Assert.NotNull(dev.Stage);
		Assert.NotNull(dev.Stage!.RunState, "the run started");
		Assert.Eq(dev.Hud!.WalletLabel.Text, "Gold 200   Stone 0   Ticket 0");
		Assert.Null(dev.Result);
	}

	public void TestRestartDuringARunDoesNothing()
	{
		DevStage dev = MakeDev();
		StageRoot first = dev.Stage!;
		dev._UnhandledInput(RestartPress());
		Assert.True(dev.Stage == first, "R is only honoured on the result screen");
	}

	public async Task TestRestartAfterAResultStartsACleanRun()
	{
		DevStage dev = MakeDev();
		StageRoot first = dev.Stage!;
		first.RunState!.Wallet.Add(CurrencyKind.Gold, 50);
		first.Factory.Spawn(GD.Load<EnemyDef>("res://port/data/enemies/orc.tres"), new Vector2(300f, 0f));
		Assert.Eq(Host.GetTree().GetNodesInGroup(Groups.Enemies).Count, 1);
		ActorFixture.Kill(this, first.Building.Entity);
		Assert.Eq(dev.Result!.Outcome, RunOutcome.Failed);
		Assert.True(dev.Hud!.ResultLabel.Visible);
		dev._UnhandledInput(RestartPress());
		Assert.Eq(Host.GetTree().GetNodesInGroup(Groups.Enemies).Count, 0, "the old run's enemies left the group at once");
		Assert.True(dev.Stage != first, "a new stage");
		Assert.False(first.IsInsideTree(), "the old stage left the tree at once");
		Assert.Null(dev.Result);
		Assert.Eq(dev.Stage!.RunState!.Wallet.Gold, 200);
		Assert.False(dev.Hud!.ResultLabel.Visible);
		await NextFrame();
		Assert.False(GodotObject.IsInstanceValid(first), "the old stage was freed");
	}

	public void TestAPromoteRequestFromTheBarBuysASlot()
	{
		DevStage dev = MakeDev();
		dev.Hud!.AbilityBar.EmitSignal(AbilityBar.SignalName.PromoteRequested, 0);
		Assert.False(dev.Stage!.Loadout!.Slot(0)!.IsEmpty, "bought");
		Assert.Eq(dev.Stage.RunState!.Wallet.Gold, 100, "200 - 100");
	}

	public void TestShopRequestsAfterTheResultAreIgnored()
	{
		DevStage dev = MakeDev();
		ActorFixture.Kill(this, dev.Stage!.Building.Entity);
		dev.Hud!.AbilityBar.EmitSignal(AbilityBar.SignalName.PromoteRequested, 0);
		dev.Hud.AbilityBar.EmitSignal(AbilityBar.SignalName.RerollRequested, 0);
		Assert.True(dev.Stage.Loadout!.Slot(0)!.IsEmpty, "the shop closes with the run");
		Assert.Eq(dev.Stage.RunState!.Wallet.Gold, 200);
	}
	public void TestTheHudFollowsTheStageEventsAndTheBoss()
	{
		var dev = GD.Load<PackedScene>("res://port/src/App/dev_stage.tscn").Instantiate<DevStage>();
		dev.StageDef = DefFactory.Fresh<StageDef>("res://port/data/stages/stage_01.tres");
		dev.StageDef.Duration = 30f;
		dev.StageDef.Waves = null;
		dev.StageDef.TrickleEnemies = [];
		dev.StageDef.Events = DefFactory.Schedule([DefFactory.Event(DefFactory.Packed(new ProbeEvent { Endless = true }), 1f)], 1, 5f, 10f, 10f);
		dev.StageDef.Boss = DefFactory.Boss(GD.Load<PackedScene>(BossActorTest.BossScene), new ProbePatternDef());
		Adopt(dev);
		dev.Stage!.Clock.SetPhysicsProcess(false);
		Assert.Eq(dev.Hud!.Timeline.EventMarks.Length, 1, "the rolled event time is on the timeline");
		dev.Stage.Clock.Step(11f);
		Assert.True(dev.Hud.EventBanner.Visible, "the banner shows the running event");
		dev.Stage.Clock.Step(20f);
		Assert.True(dev.Hud.BossBar.Visible, "the boss bar shows the boss");
		Assert.True(dev.Hud.Timeline.IsShowingBoss);
		Assert.Eq(dev.Hud.EventBanner.TitleText, "Probe: Interrupted", "the boss interrupted the event");
	}
}
