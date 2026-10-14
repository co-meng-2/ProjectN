using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class StageRootTest : TestCase
{
	private const string StageScene = "res://port/src/Stage/stage.tscn";
	private const string EnemyScene = "res://port/src/Actors/Enemy/enemy.tscn";
	private const string PickupScene = "res://port/src/Actors/Pickup/pickup.tscn";

	private static EnemyDef Grunt()
	{
		EnemyDef def = DefFactory.Enemy(GD.Load<PackedScene>(EnemyScene));
		def.Stats = new StatBlock { MaxHealth = 20f, MoveSpeed = 40f };
		def.Drops = new DropTable();
		def.Drops.Entries.Add(new DropEntry { Chance = 1f, Icon = DefFactory.Texture(8, 8) });
		return def;
	}

	private static StageDef MakeDef(float duration)
	{
		StageDef def = DefFactory.Stage(duration);
		EnemyDef grunt = Grunt();
		def.TrickleEnemies.Add(grunt);
		def.TrickleIntervalStart = 0.3f;
		def.TrickleIntervalEnd = 0.3f;
		def.Waves!.Entries.Add(new WaveEntry { Time = 0.5f, Enemy = grunt, Count = 3 });
		return def;
	}

	private StageRoot NewStage() => Adopt(GD.Load<PackedScene>(StageScene).Instantiate<StageRoot>());

	private StageRoot RunningStage(float duration, List<RunResult> results)
	{
		StageRoot stage = NewStage();
		stage.Finished += results.Add;
		Assert.True(stage.Start(MakeDef(duration), DefFactory.Profile(), 11));
		return stage;
	}

	private List<Node> Enemies() => [.. Host.GetTree().GetNodesInGroup(Groups.Enemies)];

	public async Task TestRunReachesClearedWhenTimeIsUp()
	{
		var results = new List<RunResult>();
		RunningStage(1.5f, results);
		await WaitSeconds(1.0);
		Assert.True(Enemies().Count > 0, "enemies spawned");
		await WaitSeconds(1.2);
		Assert.Eq(results.Count, 1);
		Assert.Eq(results[0].Outcome, RunOutcome.Cleared);
		Assert.AlmostEq(results[0].Elapsed, 1.5, 0.15);
	}

	public void TestDestroyingTheBuildingFailsTheRun()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		ActorFixture.Kill(this, stage.Building.Entity);
		Assert.Eq(results.Count, 1);
		Assert.Eq(results[0].Outcome, RunOutcome.Failed);
	}

	public void TestPlayerDeathFailsTheRun()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		ActorFixture.Kill(this, stage.Player.Entity);
		Assert.Eq(results.Count, 1);
		Assert.Eq(results[0].Outcome, RunOutcome.Failed);
	}

	public void TestFinishIsIdempotentFailureFirst()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		ActorFixture.Kill(this, stage.Building.Entity);
		stage.Player.EmitSignal(Player.SignalName.Died);
		stage.Clock.EmitSignal(StageClock.SignalName.Ended);
		Assert.Eq(results.Count, 1, "later finish paths must hit the terminal guard");
		Assert.Eq(results[0].Outcome, RunOutcome.Failed);
	}

	public void TestFinishIsIdempotentClearedFirst()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		stage.Clock.EmitSignal(StageClock.SignalName.Ended);
		ActorFixture.Kill(this, stage.Building.Entity);
		stage.Player.EmitSignal(Player.SignalName.Died);
		Assert.Eq(results.Count, 1);
		Assert.Eq(results[0].Outcome, RunOutcome.Cleared);
		Assert.Eq(stage.FlowState, StageState.Cleared);
	}

	public void TestTheEndedRunStopsTheWorldAndTheClock()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		ActorFixture.Kill(this, stage.Building.Entity);
		Assert.Eq(stage.World.ProcessMode, Node.ProcessModeEnum.Disabled);
		Assert.False(stage.Clock.IsRunning);
	}

	public async Task TestInvalidDefsAreRejectedBeforeAnythingStarts()
	{
		var results = new List<RunResult>();
		StageRoot stage = NewStage();
		stage.Finished += results.Add;
		Assert.False(stage.Start(MakeDef(0f), DefFactory.Profile()));
		await WaitSeconds(0.2);
		Assert.Eq(results.Count, 0);
		Assert.Null(stage.RunState);
	}

	public async Task TestCollectedLootIsCreditedWithTheGainMultiplier()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		ActorEntity entity = stage.Player.Entity;
		entity.Apply(StatEffects.SetBase(Stats.GoldGain, entity.Attributes[Stats.GoldGain].ToRawValue(2f)));
		var pickup = GD.Load<PackedScene>(PickupScene).Instantiate<Pickup>();
		stage.PickupLayer.AddChild(pickup);
		pickup.GlobalPosition = stage.Player.GlobalPosition;
		pickup.Setup(new DropRoll { Kind = CurrencyKind.Gold, Amount = 5, Icon = DefFactory.Texture(8, 8) });
		await WaitSeconds(0.3);
		Assert.Eq(stage.RunState!.Wallet.Gold, 10);
	}

	public async Task TestKilledEnemiesAreCountedAndDropPickups()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		await WaitSeconds(0.9);
		List<Node> victims = Enemies();
		Assert.True(victims.Count > 0);
		foreach (Node victim in victims)
		{
			ActorFixture.Kill(this, ((Enemy)victim).Entity);
		}

		await NextFrame();
		await NextFrame();
		Assert.Eq(stage.RunState!.Kills, victims.Count);
		Assert.True(stage.PickupLayer.GetChildCount() > 0, "gold dropped");
	}

	public async Task TestEveryEnemyDeathRaisesEventKillOnThePlayer()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		var payloads = new List<Node>();
		stage.Player.Entity.Events.Subscribe<Node>(
			GameplayTags.Request(GameplayTags.EventKill), data => payloads.Add(data.Payload), 0);
		await WaitSeconds(0.9);
		List<Node> victims = Enemies();
		Assert.True(victims.Count > 0);
		foreach (Node victim in victims)
		{
			ActorFixture.Kill(this, ((Enemy)victim).Entity);
		}

		Assert.Eq(payloads.Count, victims.Count);
		Assert.True(payloads[0] is Enemy, "the payload is the dead enemy");
		Assert.Eq(stage.RunState!.Kills, victims.Count);
	}

	public async Task TestRestartStartsClean()
	{
		var firstResults = new List<RunResult>();
		StageRoot first = RunningStage(30f, firstResults);
		first.RunState!.Wallet.Add(CurrencyKind.Gold, 50);
		ActorFixture.Kill(this, first.Building.Entity);
		first.Free();
		await NextFrame();
		var secondResults = new List<RunResult>();
		StageRoot second = RunningStage(30f, secondResults);
		Assert.Eq(second.RunState!.Wallet.Gold, 0);
		Assert.AlmostEq(second.Clock.Elapsed, 0.0);
		Assert.Eq(Enemies().Count, 0);
	}

	public void TestSecondStartIsRejectedAndDoesNotDoubleWire()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		RunState first = stage.RunState!;
		Assert.False(stage.Start(MakeDef(30f), DefFactory.Profile(), 12));
		Assert.True(stage.RunState == first, "run state untouched by the rejected start");
		stage.Clock.EmitSignal(StageClock.SignalName.Ended);
		Assert.Eq(results.Count, 1);
	}
}
