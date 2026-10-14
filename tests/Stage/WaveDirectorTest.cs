using System.Collections.Generic;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class WaveDirectorTest : TestCase
{
	private const string EnemyScene = "res://src/Actors/Enemy/enemy.tscn";
	private static readonly Rect2 BigArena = new(-5000f, -5000f, 10000f, 10000f);

	private static WaveEntry Entry(float time, int count, string label) => new()
	{
		Time = time,
		Count = count,
		Label = label,
		Enemy = DefFactory.Enemy(GD.Load<PackedScene>(EnemyScene)),
	};

	private (StageClock Clock, EnemyFactory Factory, WaveDirector Director, Node2D Anchor, List<string> Labels) MakeRig(
		params WaveEntry[] entries)
	{
		StageDef def = DefFactory.Stage(10f);
		def.SpawnRadius = 300f;
		foreach (WaveEntry entry in entries)
		{
			def.Waves!.Entries.Add(entry);
		}

		Node2D layer = Adopt(new Node2D());
		TargetProvider targets = Adopt(new TargetProvider());
		Node2D anchor = Adopt(new Node2D());
		targets.Building = anchor;
		targets.Player = anchor;
		var rng = new RunRng(4);
		EnemyFactory factory = Adopt(new EnemyFactory());
		factory.Bind(layer, targets, rng);
		StageClock clock = Adopt(new StageClock());
		clock.SetPhysicsProcess(false);
		WaveDirector director = Adopt(new WaveDirector());
		director.Bind(def, clock, factory, anchor, rng, BigArena);
		var labels = new List<string>();
		director.WaveStarted += entry => labels.Add(entry.Label);
		clock.Start(def.Duration);
		return (clock, factory, director, anchor, labels);
	}

	public void TestWavesFireInTimeOrderWithTheirEnemyCounts()
	{
		var rig = MakeRig(Entry(3f, 4, "late"), Entry(1f, 3, "early"));
		rig.Clock.Step(1.5f);
		Assert.Eq(rig.Factory.AliveCount, 3);
		rig.Clock.Step(1f);
		Assert.SequenceEq(rig.Labels, new[] { "early" });
		rig.Clock.Step(1f);
		Assert.SequenceEq(rig.Labels, new[] { "early", "late" });
		Assert.Eq(rig.Factory.AliveCount, 7);
	}

	public void TestAHugeTimeStepFiresEveryWaveExactlyOnce()
	{
		var rig = MakeRig(Entry(1f, 2, "a"), Entry(3f, 2, "b"));
		rig.Clock.Step(100f);
		Assert.SequenceEq(rig.Labels, new[] { "a", "b" });
	}

	public void TestAWaveAtTimeZeroStillFires()
	{
		var rig = MakeRig(Entry(0f, 2, "opener"));
		rig.Clock.Step(0.1f);
		Assert.SequenceEq(rig.Labels, new[] { "opener" });
	}

	public void TestMarkTimesAreSortedForTheHud()
	{
		var rig = MakeRig(Entry(3f, 1, "b"), Entry(1f, 1, "a"));
		Assert.SequenceEq(rig.Director.MarkTimes(), new[] { 1f, 3f });
	}

	public void TestWavesNeverFireAgainInOvertime()
	{
		var rig = MakeRig(Entry(1f, 2, "a"), Entry(9.5f, 1, "b"));
		rig.Clock.Step(10.5f);
		Assert.SequenceEq(rig.Labels, new[] { "a", "b" });
		for (int i = 0; i < 5; i++)
		{
			rig.Clock.Step(3f);
		}

		Assert.AlmostEq(rig.Clock.Elapsed, 25.5, 0.001, "the clock ran on past the 10 s stage");
		Assert.SequenceEq(rig.Labels, new[] { "a", "b" }, "no wave re-fires in overtime");
		Assert.Eq(rig.Factory.AliveCount, 3);
	}

	public void TestAWaveWithoutAnAnchorIsAnnouncedButSpawnsNothing()
	{
		var rig = MakeRig(Entry(1f, 3, "orphan"));
		rig.Anchor.Free();
		rig.Clock.Step(2f);
		Assert.SequenceEq(rig.Labels, new[] { "orphan" });
		Assert.Eq(rig.Factory.AliveCount, 0);
	}
}
