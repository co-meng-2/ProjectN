using System.Collections.Generic;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Actors;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class BossDirectorTest : TestCase
{
	private static readonly Vector2 AnchorAt = new(80f, -40f);

	private sealed class Record
	{
		public List<BossActor> Spawned { get; } = [];

		public List<(Health Health, string Name)> Appeared { get; } = [];

		public int Defeated { get; set; }
	}

	private sealed record Rig(BossDirector Director, StageFlow Flow, Node2D Layer, Player Player, Record Record);

	private Rig MakeRig(bool withBoss = true)
	{
		Player player = ActorFixture.Player(this);
		player.GlobalPosition = new Vector2(80f, 110f);
		Node2D anchor = Adopt(new Node2D { Position = AnchorAt });
		Node2D layer = Adopt(new Node2D());
		var flow = new StageFlow();
		flow.TransitionTo(StageState.Running);
		EventDirector events = Adopt(new EventDirector());
		BossDirector director = Adopt(new BossDirector());
		BossDef? def = withBoss ? DefFactory.Boss(GD.Load<PackedScene>(BossActorTest.BossScene), new ProbePatternDef()) : null;
		var record = new Record();
		director.BossSpawned += record.Spawned.Add;
		director.BossAppeared += (health, name) => record.Appeared.Add((health, name));
		director.BossDefeated += () => record.Defeated++;
		director.Bind(
			def, flow, events, layer, anchor, 300f, new Rect2(-1200f, -800f, 2400f, 1600f), new RunRng(6),
			player.Context!.Targets, player.Context.ProjectileLayer, null);
		return new Rig(director, flow, layer, player, record);
	}

	public void TestAppearSwitchesToBossAndSpawnsItOnTheRing()
	{
		Rig rig = MakeRig();
		BossActor? boss = rig.Director.Appear();
		Assert.NotNull(boss);
		Assert.Eq(rig.Flow.State, StageState.Boss);
		Assert.True(boss!.GetParent() == rig.Layer);
		Assert.AlmostEq(boss.GlobalPosition.DistanceTo(AnchorAt), 300.0, 0.5);
		Assert.Eq(rig.Record.Spawned.Count, 1);
		Assert.True(rig.Record.Spawned[0] == boss);
		Assert.Eq(rig.Record.Appeared.Count, 1);
		Assert.True(rig.Record.Appeared[0].Health == boss.Health);
		Assert.Eq(rig.Record.Appeared[0].Name, "Test Boss");
		Assert.True(boss.Context!.Targets.Player == rig.Player, "chases and targets the player");
	}

	public void TestAppearHappensOnceAndOnlyFromRunning()
	{
		Rig rig = MakeRig();
		BossActor? first = rig.Director.Appear();
		Assert.True(rig.Director.Appear() == first, "a second call returns the same boss");
		Assert.Eq(rig.Record.Spawned.Count, 1);
		Rig finished = MakeRig();
		finished.Flow.TransitionTo(StageState.Failed);
		Assert.Null(finished.Director.Appear(), "the run is over");
		Assert.Eq(finished.Record.Spawned.Count, 0);
	}

	public void TestBossDeathIsReportedAsDefeated()
	{
		Rig rig = MakeRig();
		BossActor boss = rig.Director.Appear()!;
		ActorFixture.Kill(this, boss.Entity);
		Assert.Eq(rig.Record.Defeated, 1);
	}

	public void TestWithoutABossNothingAppears()
	{
		Rig rig = MakeRig(withBoss: false);
		Assert.False(rig.Director.HasBoss);
		Assert.Null(rig.Director.Appear());
		Assert.Eq(rig.Flow.State, StageState.Running, "flow untouched");
	}

	public void TestValidateDefRequiresABossSceneRoot()
	{
		Assert.NoErrors(BossDirector.ValidateDef(DefFactory.Boss(GD.Load<PackedScene>(BossActorTest.BossScene))), "a real boss scene");
		Assert.NoErrors(BossDirector.ValidateDef(null), "no boss");
		var errors = BossDirector.ValidateDef(DefFactory.Boss(DefFactory.Packed(new Node2D())));
		Assert.Eq(errors.Count, 1);
		Assert.True(errors[0].Contains("test_boss"));
	}
}
