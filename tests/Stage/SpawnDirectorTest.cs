using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class SpawnDirectorTest : TestCase
{
	private const string EnemyScene = "res://src/Actors/Enemy/enemy.tscn";
	private static readonly Rect2 BigArena = new(-5000f, -5000f, 10000f, 10000f);

	private (StageClock Clock, EnemyFactory Factory, SpawnDirector Director, Node2D Anchor) MakeRig(StageDef def)
	{
		Node2D layer = Adopt(new Node2D());
		TargetProvider targets = Adopt(new TargetProvider());
		Node2D anchor = Adopt(new Node2D());
		targets.Building = anchor;
		targets.Player = anchor;
		var rng = new RunRng(2);
		EnemyFactory factory = Adopt(new EnemyFactory());
		factory.Bind(layer, targets, rng);
		StageClock clock = Adopt(new StageClock());
		clock.SetPhysicsProcess(false);
		SpawnDirector director = Adopt(new SpawnDirector());
		director.Bind(def, clock, factory, anchor, rng, BigArena);
		clock.Start(def.Duration);
		return (clock, factory, director, anchor);
	}

	private static StageDef MakeStage(int cap)
	{
		StageDef def = DefFactory.Stage(100f);
		def.TrickleEnemies.Add(DefFactory.Enemy(GD.Load<PackedScene>(EnemyScene)));
		def.TrickleIntervalStart = 1f;
		def.TrickleIntervalEnd = 1f;
		def.TrickleCap = cap;
		def.SpawnRadius = 300f;
		return def;
	}

	public void TestIntervalInterpolatesAcrossTheStage()
	{
		StageDef def = MakeStage(10);
		def.TrickleIntervalStart = 2f;
		def.TrickleIntervalEnd = 0.5f;
		SpawnDirector director = MakeRig(def).Director;
		Assert.AlmostEq(director.CurrentInterval(0f), 2.0);
		Assert.AlmostEq(director.CurrentInterval(50f), 1.25);
		Assert.AlmostEq(director.CurrentInterval(100f), 0.5);
	}

	public void TestSpawnsOnTheRingAtTheConfiguredIntervalAndRespectsTheCap()
	{
		var rig = MakeRig(MakeStage(2));
		rig.Clock.Step(0.1f);
		Assert.Eq(rig.Factory.AliveCount, 1);
		rig.Clock.Step(0.5f);
		Assert.Eq(rig.Factory.AliveCount, 1, "interval not elapsed");
		rig.Clock.Step(1f);
		Assert.Eq(rig.Factory.AliveCount, 2);
		rig.Clock.Step(5f);
		Assert.Eq(rig.Factory.AliveCount, 2, "cap reached");
		foreach (Node node in Host.GetTree().GetNodesInGroup(Groups.Enemies))
		{
			Assert.AlmostEq(((Node2D)node).GlobalPosition.Length(), 300.0, 0.5);
		}
	}

	public void TestEmptyTrickleListSpawnsNothingAndDoesNotFail()
	{
		StageDef def = MakeStage(5);
		def.TrickleEnemies.Clear();
		var rig = MakeRig(def);
		rig.Clock.Step(3f);
		Assert.Eq(rig.Factory.AliveCount, 0);
	}

	public void TestTrickleContinuesInOvertimeAtTheEndInterval()
	{
		StageDef def = MakeStage(10);
		def.Duration = 2f;
		def.TrickleIntervalStart = 3f;
		def.TrickleIntervalEnd = 1f;
		var rig = MakeRig(def);
		Assert.AlmostEq(rig.Director.CurrentInterval(25f), 1.0, 0.001, "clamped to the end interval");
		rig.Clock.Step(2.5f);
		Assert.Eq(rig.Factory.AliveCount, 1);
		rig.Clock.Step(1f);
		Assert.Eq(rig.Factory.AliveCount, 2, "spawns keep coming after the stage time");
		rig.Clock.Step(1f);
		Assert.Eq(rig.Factory.AliveCount, 3);
		Assert.True(rig.Clock.Elapsed > def.Duration);
	}

	public void TestAZeroCapSpawnsNothing()
	{
		var rig = MakeRig(MakeStage(0));
		for (int i = 0; i < 5; i++)
		{
			rig.Clock.Step(1f);
		}

		Assert.Eq(rig.Factory.AliveCount, 0);
	}

	public void TestAFreedAnchorStopsTheTrickleWithoutErrors()
	{
		var rig = MakeRig(MakeStage(10));
		rig.Anchor.Free();
		rig.Clock.Step(0.1f);
		rig.Clock.Step(1f);
		Assert.Eq(rig.Factory.AliveCount, 0, "no building, no ring to spawn on");
	}
}
