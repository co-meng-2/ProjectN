using System.Collections.Generic;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Boss;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Actors;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Boss;

public class SummonTest : TestCase
{
	public const string SummonPath = "res://data/bosses/patterns/summon.tres";
	private static readonly Vector2 BossAt = new(100f, 50f);

	private (BossActor Boss, SummonDef Summon) MakeBoss(System.Action<EnemyDef, Vector2>? summon)
	{
		Player player = ActorFixture.Player(this);
		player.GlobalPosition = new Vector2(-500f, 0f);
		var def = DefFactory.Fresh<SummonDef>(SummonPath);
		BossActor boss = ActorFixture.Boss(this, player, DefFactory.Boss(GD.Load<PackedScene>(BossActorTest.BossScene), def), BossAt, summon);
		return (boss, def);
	}

	public void TestSummonsCountEnemiesOnTheRingThroughTheContext()
	{
		var calls = new List<(EnemyDef Def, Vector2 At)>();
		var (boss, def) = MakeBoss((enemy, at) => calls.Add((enemy, at)));
		Assert.True(ActorFixture.Fire(boss.Patterns[0]));
		Assert.Eq(calls.Count, 4);
		var seen = new HashSet<Vector2>();
		foreach (var (enemy, at) in calls)
		{
			Assert.True(enemy == def.Enemy);
			Assert.AlmostEq(at.DistanceTo(BossAt), 80.0, 0.01);
			Assert.True(seen.Add(at), "distinct points");
		}

		Assert.False(boss.IsCasting);
	}

	public void TestWithoutASummonTheSummonDoesNotStart()
	{
		var (boss, _) = MakeBoss(null);
		ActorFixture.Fire(boss.Patterns[0]);
		Assert.False(boss.Patterns[0].Ability!.IsActive);
		Assert.False(boss.IsCasting);
	}

	public void TestSummonedEnemiesComeFromARealFactory()
	{
		Node2D layer = Adopt(new Node2D());
		TargetProvider targets = Adopt(new TargetProvider());
		EnemyFactory factory = Adopt(new EnemyFactory());
		factory.Bind(layer, targets, new RunRng(3));
		var (boss, def) = MakeBoss((enemy, at) => factory.Spawn(enemy, at));
		def.Enemy = DefFactory.Enemy(GD.Load<PackedScene>("res://src/Actors/Enemy/enemy.tscn"));
		ActorFixture.Fire(boss.Patterns[0]);
		Assert.Eq(factory.AliveCount, 4);
		Assert.Eq(layer.GetChildCount(), 4);
	}

	public void TestValidateReportsAMissingEnemyAndBadNumbers()
	{
		var summon = DefFactory.Fresh<SummonDef>(SummonPath);
		Assert.NoErrors(summon.Validate(), "as shipped");
		Assert.Eq(summon.Enemy!.Id.ToString(), "orc_swarmer");
		summon.Enemy = null;
		summon.Radius = 0f;
		Assert.Eq(summon.Validate().Count, 2);
	}
}
