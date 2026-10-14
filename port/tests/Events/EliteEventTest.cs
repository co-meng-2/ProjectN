using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Core;
using VampireSurvivor.Events;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Events;

public class EliteEventTest : TestCase
{
	private const string EliteEventScene = "res://port/src/Events/elite_event.tscn";
	private static readonly Vector2 BuildingAt = new(50f, 40f);

	private sealed record Rig(EliteEvent Event, EventContext Context, ActorFixture.EventRecord Record);

	private Rig MakeRig()
	{
		EventContext context = ActorFixture.ContextFor(
			this, DefFactory.Event(GD.Load<PackedScene>(EliteEventScene), 25f, "elites"), null, BuildingAt);
		EliteEvent elites = Adopt(GD.Load<PackedScene>(EliteEventScene).Instantiate<EliteEvent>());
		elites.SetPhysicsProcess(false);
		ActorFixture.EventRecord record = ActorFixture.WatchEvent(elites);
		elites.Begin(context);
		return new Rig(elites, context, record);
	}

	private static List<Enemy> Elites(EliteEvent elites) => [.. elites.Objectives().Cast<Enemy>()];

	public void TestSpawnsTheElitesThroughTheFactoryOnTheSpawnRing()
	{
		Rig rig = MakeRig();
		List<Enemy> elites = Elites(rig.Event);
		Assert.Eq(elites.Count, 3);
		Assert.Eq(rig.Context.Factory.AliveCount, 3, "the factory made them, so kills and drops work");
		foreach (Enemy elite in elites)
		{
			Assert.Eq(elite.Def!.Id.ToString(), "elite_orc");
			Assert.AlmostEq(elite.GlobalPosition.DistanceTo(BuildingAt), 300.0, 0.5);
		}

		Assert.SequenceEq(rig.Record.Progress, new[] { "Elites 0/3" });
	}

	public void TestKillingEveryEliteSucceeds()
	{
		Rig rig = MakeRig();
		List<Enemy> elites = Elites(rig.Event);
		ActorFixture.Kill(this, elites[0].Entity);
		ActorFixture.Kill(this, elites[1].Entity);
		Assert.Eq(rig.Record.Progress[^1], "Elites 2/3");
		Assert.Eq(rig.Event.Objectives().Count, 1);
		Assert.True(rig.Event.IsRunning);
		ActorFixture.Kill(this, elites[2].Entity);
		Assert.Eq(rig.Record.Succeeded, 1);
		Assert.Eq(rig.Record.Failed, 0);
	}

	public async Task TestTimeUpFailsAndTheSurvivorsStay()
	{
		Rig rig = MakeRig();
		List<Enemy> elites = Elites(rig.Event);
		ActorFixture.Kill(this, elites[0].Entity);
		rig.Event.Step(25.5f);
		Assert.Eq(rig.Record.Failed, 1);
		await NextFrame();
		for (int i = 1; i < 3; i++)
		{
			Assert.True(GodotObject.IsInstanceValid(elites[i]) && !elites[i].IsQueuedForDeletion(), $"elite {i} keeps fighting");
			Assert.True(elites[i].IsInGroup(Groups.Enemies));
		}

		ActorFixture.Kill(this, elites[1].Entity);
		Assert.Eq(rig.Record.Progress[^1], "Elites 1/3", "kills after the end no longer count");
		Assert.Eq(rig.Record.Succeeded, 0);
	}

	public void TestAbortLeavesTheElitesInTheFight()
	{
		Rig rig = MakeRig();
		rig.Event.Abort();
		Assert.Eq(rig.Record.Failed, 0);
		Assert.Eq(rig.Context.Factory.AliveCount, 3);
		Assert.Eq(rig.Event.Objectives().Count, 0);
	}

	public void TestValidateReportsAMissingOrInvalidEliteAndABadCount()
	{
		EliteEvent elites = GD.Load<PackedScene>(EliteEventScene).Instantiate<EliteEvent>();
		Assert.NoErrors(elites.Validate(), "scene as shipped");
		elites.Count = 0;
		elites.EliteEnemy = null;
		Assert.Eq(elites.Validate().Count, 2);
		elites.Free();
	}
}
