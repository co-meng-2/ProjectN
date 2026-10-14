using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Actors;

public class BossActorTest : TestCase
{
	public const string BossScene = "res://port/src/Actors/Boss/boss.tscn";

	private sealed record Rig(Player Player, BossActor Boss, ProbePatternDef Pattern);

	private Rig MakeRig(System.Action<EnemyDef, Vector2>? summon = null)
	{
		Player player = ActorFixture.Player(this);
		player.GlobalPosition = new Vector2(300f, 60f);
		var pattern = new ProbePatternDef();
		BossDef def = DefFactory.Boss(GD.Load<PackedScene>(BossScene), pattern);
		def.Stats!.MoveSpeed = 120f;
		BossActor boss = ActorFixture.Boss(this, player, def, new Vector2(-200f, 60f), summon);
		return new Rig(player, boss, pattern);
	}

	public void TestSetupAppliesStatsJoinsTheEnemyGroupAndGrantsThePatterns()
	{
		Rig rig = MakeRig();
		Assert.AlmostEq(rig.Boss.Health.Current(), 500.0);
		Assert.True(rig.Boss.IsInGroup(Groups.Enemies));
		Assert.Eq(rig.Boss.Patterns.Count, 1);
		Assert.Eq(rig.Boss.PatternWeights.Count, 1);
		Assert.AlmostEq(rig.Boss.PatternWeights[0], 1.0);
		Assert.True(rig.Boss.Context!.Actor == rig.Boss);
		Assert.True(rig.Boss.Context.Targets == rig.Player.Context!.Targets);
		Assert.Null(rig.Boss.Context.Wallet, "the boss has no wallet");
		Assert.Null(rig.Boss.Context.Summon, "no summon injected");
		Assert.True(ActorFixture.Fire(rig.Boss.Patterns[0]), "the pattern is granted to the boss's entity");
		Assert.Eq(rig.Pattern.Starts, 1);
	}

	public void TestTheInjectedSummonReachesThePatterns()
	{
		var calls = new List<Vector2>();
		Rig rig = MakeRig((_, at) => calls.Add(at));
		rig.Boss.Context!.Summon!(null!, new Vector2(7f, 8f));
		Assert.SequenceEq(calls, new[] { new Vector2(7f, 8f) });
	}

	public async Task TestChasesItsTargetAndStandsStillWhileCasting()
	{
		Rig rig = MakeRig();
		await WaitSeconds(0.3);
		Assert.True(rig.Boss.GlobalPosition.X > -190f, "moved toward the player on +x");
		TestEffects.GrantTag(rig.Boss.Entity, GameplayTags.StateCasting);
		await NextPhysicsFrame();
		Vector2 held = rig.Boss.GlobalPosition;
		await WaitSeconds(0.3);
		Assert.AlmostEq(rig.Boss.GlobalPosition.DistanceTo(held), 0.0, 0.5, "no chasing while casting");
		Assert.True(rig.Boss.IsCasting);
	}

	public async Task TestDeathAbortsPatternsLeavesTheGroupAndFreesItself()
	{
		Rig rig = MakeRig();
		rig.Pattern.Hold = true;
		Assert.True(ActorFixture.Fire(rig.Boss.Patterns[0]));
		Assert.True(rig.Boss.IsCasting);
		var deaths = new List<BossActor>();
		rig.Boss.Died += deaths.Add;
		ActorFixture.Kill(this, rig.Boss.Entity);
		Assert.Eq(deaths.Count, 1);
		Assert.Eq(rig.Pattern.Aborts, 1);
		Assert.False(rig.Boss.Patterns[0].Ability!.IsActive);
		Assert.False(rig.Boss.IsCasting, "the casting tag is stripped by the abort");
		Assert.False(rig.Boss.IsInGroup(Groups.Enemies));
		ActorFixture.Kill(this, rig.Boss.Entity);
		Assert.Eq(deaths.Count, 1, "dies once");
		await WaitSeconds(1.3);
		Assert.False(GodotObject.IsInstanceValid(rig.Boss));
	}
}
