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

public class StatueEventTest : TestCase
{
	private const string StatueEventScene = "res://port/src/Events/statue_event.tscn";
	private static readonly Vector2 BuildingAt = new(100f, -50f);

	private sealed record Rig(StatueEvent Event, EventContext Context, ActorFixture.EventRecord Record);

	private static StatueEvent Instantiate() => GD.Load<PackedScene>(StatueEventScene).Instantiate<StatueEvent>();

	private Rig MakeRig(float minDistance = -1f, float spacing = -1f)
	{
		EventContext context = ActorFixture.ContextFor(
			this, DefFactory.Event(GD.Load<PackedScene>(StatueEventScene), 25f, "statues"), null, BuildingAt);
		StatueEvent statues = Adopt(Instantiate());
		statues.SetPhysicsProcess(false);
		if (minDistance >= 0f)
		{
			statues.MinDistance = minDistance;
		}

		if (spacing >= 0f)
		{
			statues.StatueSpacing = spacing;
		}

		ActorFixture.EventRecord record = ActorFixture.WatchEvent(statues);
		statues.Begin(context);
		return new Rig(statues, context, record);
	}

	private static List<Statue> Statues(StatueEvent statues) => [.. statues.Objectives().Cast<Statue>()];

	public void TestSpawnsTheStatuesAwayFromTheBuildingAndThePlayer()
	{
		Rig rig = MakeRig();
		List<Statue> statues = Statues(rig.Event);
		Assert.Eq(statues.Count, 4);
		Rect2 inner = rig.Context.Bounds.Grow(-48f);
		foreach (Statue statue in statues)
		{
			Assert.True(statue.GlobalPosition.DistanceTo(rig.Context.Building!.GlobalPosition) >= 250f, "far from the building");
			Assert.True(statue.GlobalPosition.DistanceTo(rig.Context.Player!.GlobalPosition) >= 250f, "far from the player");
			Assert.True(inner.HasPoint(statue.GlobalPosition), "inside the arena margin");
			Assert.AlmostEq(statue.Health.Current(), 60.0);
			Assert.True(statue.GetParent() == rig.Event, "event-owned");
		}

		Assert.SequenceEq(rig.Record.Progress, new[] { "Statues 0/4" });
	}

	public void TestDestroyingEveryStatueSucceedsOnce()
	{
		Rig rig = MakeRig();
		List<Statue> statues = Statues(rig.Event);
		for (int i = 0; i < 3; i++)
		{
			ActorFixture.Kill(this, statues[i].Entity);
		}

		Assert.Eq(rig.Record.Progress[^1], "Statues 3/4");
		Assert.Eq(rig.Event.Objectives().Count, 1);
		Assert.True(rig.Event.IsRunning);
		ActorFixture.Kill(this, statues[3].Entity);
		Assert.Eq(rig.Record.Succeeded, 1);
		Assert.Eq(rig.Record.Failed, 0);
		Assert.False(rig.Event.IsRunning);
	}

	public async Task TestTimeUpRemovesTheRemainingStatuesAndFails()
	{
		Rig rig = MakeRig();
		List<Statue> statues = Statues(rig.Event);
		ActorFixture.Kill(this, statues[0].Entity);
		rig.Event.Step(24f);
		Assert.True(rig.Event.IsRunning);
		rig.Event.Step(1.5f);
		Assert.Eq(rig.Record.Failed, 1);
		for (int i = 1; i < 4; i++)
		{
			Assert.True(statues[i].IsRemoved, $"statue {i} removed");
		}

		Assert.Eq(rig.Event.Objectives().Count, 0);
		await Settle();
		Assert.Eq(Host.GetTree().GetNodesInGroup(Groups.Enemies).Count, 0);
	}

	public void TestAbortRemovesTheStatuesWithoutAResult()
	{
		Rig rig = MakeRig();
		List<Statue> statues = Statues(rig.Event);
		rig.Event.Abort();
		Assert.Eq(rig.Record.Succeeded, 0);
		Assert.Eq(rig.Record.Failed, 0);
		Assert.True(statues.All(s => s.IsRemoved));
	}

	public void TestImpossibleDistanceStillPlacesEveryStatue()
	{
		Assert.Eq(Statues(MakeRig(100000f).Event).Count, 4, "falls back to the best spot found, never hangs");
	}

	public void TestStatuesKeepTheirSpacingFromEachOther()
	{
		List<Statue> statues = Statues(MakeRig(0f, 600f).Event);
		Assert.Eq(statues.Count, 4);
		for (int i = 0; i < statues.Count; i++)
		{
			for (int j = i + 1; j < statues.Count; j++)
			{
				Assert.True(statues[i].GlobalPosition.DistanceTo(statues[j].GlobalPosition) >= 600f, $"statues {i} and {j} apart");
			}
		}
	}

	public void TestImpossibleStatueSpacingStillPlacesEveryStatue()
	{
		Assert.Eq(Statues(MakeRig(0f, 100000f).Event).Count, 4, "falls back to the best spot found, never hangs");
	}

	public void TestEventControllersDepthSortTheirChildren()
	{
		Assert.True(Adopt(Instantiate()).YSortEnabled);
	}

	public void TestValidateReportsMissingSceneAndBadNumbers()
	{
		StatueEvent statues = Instantiate();
		Assert.NoErrors(statues.Validate(), "scene as shipped");
		statues.StatueScene = null;
		statues.StatueCount = 0;
		statues.MinDistance = -1f;
		Assert.Eq(statues.Validate().Count, 3);
		statues.Free();
	}

	public void TestValidateRejectsASceneWhoseRootIsNotAStatue()
	{
		StatueEvent statues = Instantiate();
		statues.StatueScene = GD.Load<PackedScene>("res://port/src/Actors/Projectile/projectile.tscn");
		var errors = statues.Validate();
		Assert.Eq(errors.Count, 1);
		Assert.True(errors[0].Contains("not a Statue"));
		statues.Free();
	}

	public void TestAWideSpacingIsHonouredByRetrying()
	{
		Rig rig = MakeRig(900f);
		List<Statue> statues = Statues(rig.Event);
		Assert.Eq(statues.Count, 4);
		foreach (Statue statue in statues)
		{
			Assert.True(statue.GlobalPosition.DistanceTo(rig.Context.Building!.GlobalPosition) >= 900f, "far from the building");
			Assert.True(statue.GlobalPosition.DistanceTo(rig.Context.Player!.GlobalPosition) >= 900f, "far from the player");
		}
	}
}
