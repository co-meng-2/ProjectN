using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Tests.Abilities;
using VampireSurvivor.Tests.Actors;
using VampireSurvivor.Tests.Boss;
using VampireSurvivor.Actors;
using VampireSurvivor.Boss;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class StageRootTest : TestCase
{
	private const string StageScene = "res://src/Stage/stage.tscn";
	private const string EnemyScene = "res://src/Actors/Enemy/enemy.tscn";
	private const string PickupScene = "res://src/Actors/Pickup/pickup.tscn";

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
		first.Factory.Spawn(Grunt(), new Vector2(300f, 0f));
		Assert.Eq(Enemies().Count, 1);
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

	/// <summary>A stage whose profile offers <paramref name="abilities"/>; quiet removes every enemy so nothing dies or drops.</summary>
	private StageRoot ShopStage(AbilityDef[] abilities, int gold, int stone, bool quiet = true)
	{
		StageDef def = MakeDef(60f);
		if (quiet)
		{
			def.TrickleEnemies.Clear();
			def.Waves!.Entries.Clear();
		}

		def.StartGold = gold;
		def.StartStone = stone;
		PlayerProfile profile = DefFactory.Profile();
		profile.SlotCount = 2;
		foreach (AbilityDef ability in abilities)
		{
			profile.Abilities.Add(ability);
		}

		StageRoot stage = NewStage();
		Assert.True(stage.Start(def, profile, 11));
		return stage;
	}

	private static double MaxHealth(StageRoot stage) => stage.Player.Entity.Attributes[Stats.MaxHealth].DisplayValue;

	public void TestStageBuildsTheLoadoutAndGradeServiceFromTheProfile()
	{
		StageRoot stage = ShopStage([DefFactory.VitalityAbility()], 200, 0);
		Assert.Eq(stage.Loadout!.Size, 2);
		Assert.Eq(stage.RunState!.Wallet.Gold, 200);
		Assert.Eq(stage.GradeService!.PromoteCost(0)!.Value.Amount, 100);
		Assert.Null(stage.GradeService.RerollCost(0));
	}

	public void TestBuyingPromotingAndRevealingThroughTheServiceChangesThePlayer()
	{
		AbilityDef vitality = DefFactory.VitalityAbility();
		StageRoot stage = ShopStage([vitality], 1500, 20);
		GradeService service = stage.GradeService!;
		var wallet = stage.RunState!.Wallet;
		Assert.Eq(service.Promote(0).Outcome, GradeOutcome.Ok);
		Assert.Eq(wallet.Gold, 1400);
		Assert.AlmostEq(MaxHealth(stage), 120.0);
		for (int i = 0; i < 4; i++)
		{
			Assert.Eq(service.Promote(0).Outcome, GradeOutcome.Ok);
		}

		Assert.Eq(stage.Loadout!.Slot(0)!.Grade, Grade.Legendary);
		Assert.Eq(wallet.Gold, 0);
		Assert.AlmostEq(MaxHealth(stage), 260.0);
		Assert.Eq(service.Reroll(0).Outcome, GradeOutcome.Ok);
		Assert.Eq(wallet.Stone, 18);
		Assert.AlmostEq(MaxHealth(stage), 260.0, 0.001, "reinstalled, not stacked");
		Assert.Eq(service.Promote(0).Outcome, GradeOutcome.Ok);
		Assert.Eq(stage.Loadout.Slot(0)!.Grade, Grade.Revealed);
		Assert.True(stage.Loadout.Slot(0)!.Def == vitality);
		Assert.Eq(wallet.Stone, 8);
		Assert.AlmostEq(MaxHealth(stage), 340.0);
		Assert.Eq(service.Promote(0).Outcome, GradeOutcome.NotApplicable);
		Assert.Eq(wallet.Stone, 8);
	}

	public async Task TestABoughtAttackIsGrantedToThePlayerAndAutoFires()
	{
		BladeSweepDef sweep = BladeSweepTest.MakeSweep();
		sweep.AutoFire = true;
		StageRoot stage = ShopStage([DefFactory.Ability("sweep", active: sweep)], 100, 0);
		Assert.Eq(stage.GradeService!.Promote(0).Outcome, GradeOutcome.Ok);
		Node2D enemy = Adopt(new Node2D { GlobalPosition = stage.Player.GlobalPosition + new Vector2(20f, 0f) });
		enemy.AddToGroup(Groups.Enemies);
		await WaitSeconds(0.2);
		Assert.True(
			stage.Player.Entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.SlotCooldowns[0])),
			"the slot's attack fired on its own and is cooling down");
	}

	public async Task TestBountyPaysGoldWhenTheStageReportsAKill()
	{
		StageRoot stage = ShopStage([DefFactory.Ability("bounty", active: BountyTest.MakeBounty())], 100, 0, quiet: false);
		Assert.Eq(stage.GradeService!.Promote(0).Outcome, GradeOutcome.Ok);
		Assert.Eq(stage.RunState!.Wallet.Gold, 0);
		await WaitSeconds(0.9);
		List<Node> victims = Enemies();
		Assert.True(victims.Count > 0);
		foreach (Node victim in victims)
		{
			ActorFixture.Kill(this, ((Enemy)victim).Entity);
		}

		Assert.Eq(stage.RunState.Wallet.Gold, victims.Count, "1 gold per kill at Common; the drops are never collected");
	}

	public void TestAbilitiesWithUnregisteredTagsAreRejectedBeforeAnythingStarts()
	{
		PlayerProfile catalog = DefFactory.Profile();
		catalog.Abilities.Add(DefFactory.Ability("bad", active: new ProbeAbilityDef { Tag = "ability.nonexistent" }));
		StageRoot first = NewStage();
		Assert.False(first.Start(MakeDef(30f), catalog), "a catalog attack with an unregistered tag");
		Assert.Null(first.RunState);
		PlayerProfile innate = DefFactory.Profile();
		innate.InnateAbilities.Add(new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe, TriggerEventTag = "event.nonexistent" });
		StageRoot second = NewStage();
		Assert.False(second.Start(MakeDef(30f), innate), "an innate ability with an unregistered trigger tag");
		Assert.Null(second.RunState);
	}
	/// <summary>A stage without trickle or waves whose clock the test drives by hand.</summary>
	private StageRoot ManualStage(StageDef def, List<RunResult> results)
	{
		StageRoot stage = NewStage();
		stage.Finished += results.Add;
		Assert.True(stage.Start(def, DefFactory.Profile(), 11));
		stage.Clock.SetPhysicsProcess(false);
		return stage;
	}

	private static StageDef QuietDef(float duration)
	{
		StageDef def = MakeDef(duration);
		def.TrickleEnemies.Clear();
		def.Waves!.Entries.Clear();
		return def;
	}

	private static BossDef BossWith(PatternDef pattern) =>
		DefFactory.Boss(GD.Load<PackedScene>(BossActorTest.BossScene), pattern);

	/// <summary>
	/// One endless probe event somewhere in [windowStart, windowEnd]. Its time limit is 1 s so the schedule passes the
	/// window_end + time_limit rule even on a short stage; being endless it runs until the test (or the boss) ends it.
	/// </summary>
	private static EventSchedule ProbeEvents(float windowStart, float windowEnd) =>
		DefFactory.Schedule([DefFactory.Event(DefFactory.Packed(new ProbeEvent { Endless = true }), 1f)], 1, windowStart, windowEnd, 10f);

	public void TestTimeUpBringsTheBossAndItsDeathClearsTheStage()
	{
		StageDef def = QuietDef(5f);
		def.Boss = BossWith(new ProbePatternDef());
		var results = new List<RunResult>();
		StageRoot stage = ManualStage(def, results);
		var appeared = new List<(Health Health, string Name)>();
		stage.BossAppeared += (health, name) => appeared.Add((health, name));
		stage.Clock.Step(5f);
		Assert.Eq(results.Count, 0, "time up starts the boss fight instead of clearing");
		Assert.Eq(stage.FlowState, StageState.Boss);
		BossActor? boss = stage.CurrentBoss;
		Assert.NotNull(boss);
		Assert.Eq(appeared.Count, 1);
		Assert.True(appeared[0].Health == boss!.Health);
		Assert.Eq(appeared[0].Name, "Test Boss");
		Assert.AlmostEq(boss.GlobalPosition.DistanceTo(stage.Building.GlobalPosition), def.SpawnRadius, 1.0);
		Assert.True(boss.GetNodeOrNull("Brain") is BossBrain, "the stage attached the brain");
		stage.Clock.Step(30f);
		Assert.Eq(results.Count, 0, "the boss fight has no time limit");
		Assert.AlmostEq(stage.Clock.Elapsed, 35.0);
		ActorFixture.Kill(this, boss.Entity);
		Assert.Eq(results.Count, 1);
		Assert.Eq(results[0].Outcome, RunOutcome.Cleared);
		Assert.Eq(stage.RunState!.Kills, 0, "the boss is not a kill");
	}

	public void TestWithoutABossTimeUpClearsAtOnce()
	{
		var results = new List<RunResult>();
		StageRoot stage = ManualStage(QuietDef(5f), results);
		stage.Clock.Step(5f);
		Assert.Eq(results.Count, 1);
		Assert.Eq(results[0].Outcome, RunOutcome.Cleared);
		Assert.Null(stage.CurrentBoss);
		Assert.Eq(stage.FlowState, StageState.Cleared);
	}

	public void TestPlayerDeathDuringTheBossFightFailsTheRun()
	{
		StageDef def = QuietDef(5f);
		def.Boss = BossWith(new ProbePatternDef());
		var results = new List<RunResult>();
		StageRoot stage = ManualStage(def, results);
		stage.Clock.Step(5f);
		ActorFixture.Kill(this, stage.Player.Entity);
		Assert.Eq(results.Count, 1);
		Assert.Eq(results[0].Outcome, RunOutcome.Failed);
	}

	public void TestTheBossAppearanceAbortsARunningEventWithoutReward()
	{
		StageDef def = QuietDef(5f);
		def.Boss = BossWith(new ProbePatternDef());
		def.Events = ProbeEvents(1f, 3f);
		var results = new List<RunResult>();
		StageRoot stage = ManualStage(def, results);
		var ended = new List<bool>();
		var aborted = new List<EventDef>();
		stage.Events.EventEnded += (_, success) => ended.Add(success);
		stage.Events.EventAborted += aborted.Add;
		stage.Clock.Step(3.5f);
		var probe = stage.Events.Running as ProbeEvent;
		Assert.NotNull(probe, "the event is running");
		Assert.True(probe!.GetParent() == stage.GetNode("World/Events"));
		stage.Clock.Step(1.5f);
		Assert.NotNull(stage.CurrentBoss);
		Assert.Eq(aborted.Count, 1);
		Assert.Eq(ended.Count, 0, "an abort is not a failure");
		Assert.Eq(probe.Cleanups, 1, "its objects were removed");
		Assert.Null(stage.Events.Running);
		Assert.Eq(stage.RunState!.Wallet.Gold, 0);
		Assert.Eq(stage.RunState.Wallet.Tickets, 0);
	}

	public void TestASuccessfulEventCreditsItsReward()
	{
		StageDef def = QuietDef(60f);
		def.Events = ProbeEvents(5f, 10f);
		var results = new List<RunResult>();
		StageRoot stage = ManualStage(def, results);
		stage.Clock.Step(11f);
		((ProbeEvent)stage.Events.Running!).Finish(true);
		Assert.Eq(stage.RunState!.Wallet.Gold, 150);
		Assert.Eq(stage.RunState.Wallet.Stone, 2);
		Assert.Eq(stage.RunState.Wallet.Tickets, 1);
	}

	public void TestAFailedEventPaysNothing()
	{
		StageDef def = QuietDef(60f);
		def.Events = ProbeEvents(5f, 10f);
		var results = new List<RunResult>();
		StageRoot stage = ManualStage(def, results);
		stage.Clock.Step(11f);
		((ProbeEvent)stage.Events.Running!).Finish(false);
		Assert.Eq(stage.RunState!.Wallet.Gold, 0);
		Assert.Eq(stage.RunState.Wallet.Tickets, 0);
	}

	public void TestTheRunEndingAbortsARunningEvent()
	{
		StageDef def = QuietDef(60f);
		def.Events = ProbeEvents(5f, 10f);
		var results = new List<RunResult>();
		StageRoot stage = ManualStage(def, results);
		stage.Clock.Step(11f);
		var probe = (ProbeEvent)stage.Events.Running!;
		ActorFixture.Kill(this, stage.Building.Entity);
		Assert.Eq(results[0].Outcome, RunOutcome.Failed);
		Assert.Eq(probe.Cleanups, 1);
		Assert.Null(stage.Events.Running);
	}

	public void TestBossSummonsSpawnThroughTheFactoryAndCountAsKills()
	{
		StageDef def = QuietDef(5f);
		var summon = DefFactory.Fresh<SummonDef>(SummonTest.SummonPath);
		def.Boss = BossWith(summon);
		var results = new List<RunResult>();
		StageRoot stage = ManualStage(def, results);
		stage.Clock.Step(5f);
		BossActor boss = stage.CurrentBoss!;
		Assert.True(ActorFixture.Fire(boss.Patterns[0]));
		List<Enemy> summoned = [.. Enemies().OfType<Enemy>()];
		Assert.Eq(summoned.Count, 4);
		Assert.True(summoned[0].GetParent() == stage.EnemyLayer);
		ActorFixture.Kill(this, summoned[0].Entity);
		Assert.Eq(stage.RunState!.Kills, 1, "summons are ordinary kills");
	}

	public void TestEventControllersMustBeStageEvents()
	{
		StageDef def = QuietDef(60f);
		def.Events = DefFactory.Schedule([DefFactory.Event(DefFactory.Packed(new Node2D()), 10f, "plain")], 1, 5f, 10f, 10f);
		Assert.NoErrors(def.Validate(), "the def alone cannot see the problem");
		StageRoot stage = NewStage();
		Assert.False(stage.Start(def, DefFactory.Profile(), 11));
		Assert.Null(stage.RunState);
	}

	public void TestBossPatternsMustBeBossPatterns()
	{
		StageDef def = QuietDef(60f);
		def.Boss = DefFactory.Boss(GD.Load<PackedScene>(BossActorTest.BossScene), new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe });
		Assert.NoErrors(def.Validate(), "the def alone cannot see the problem");
		StageRoot stage = NewStage();
		Assert.False(stage.Start(def, DefFactory.Profile(), 11));
		Assert.Null(stage.RunState);
	}

	public void TestTheBossSceneMustBeABoss()
	{
		StageDef def = QuietDef(60f);
		def.Boss = DefFactory.Boss(DefFactory.Packed(new Node2D()), new ProbePatternDef());
		Assert.NoErrors(def.Validate(), "the def alone cannot see the problem");
		StageRoot stage = NewStage();
		Assert.False(stage.Start(def, DefFactory.Profile(), 11));
		Assert.Null(stage.RunState);
	}
}
