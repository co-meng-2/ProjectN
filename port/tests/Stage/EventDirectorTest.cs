using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class EventDirectorTest : TestCase
{
	private sealed class Record
	{
		public List<(EventDef Def, StageEvent Event)> Started { get; } = [];

		public List<(EventDef Def, string Text)> Progress { get; } = [];

		public List<(EventDef Def, bool Success)> Ended { get; } = [];

		public List<EventDef> Aborted { get; } = [];
	}

	private sealed record Rig(EventDirector Director, StageClock Clock, Node2D Layer, Record Record);

	private static PackedScene ProbeScene(bool endless = false) => DefFactory.Packed(new ProbeEvent { Endless = endless });

	private static EventDef[] Pool(PackedScene? scene = null) => [DefFactory.Event(scene ?? ProbeScene(), 20f)];

	/// <summary>A director over <paramref name="pool"/> with <paramref name="count"/> events in [10, 60], gap 20, stage 100 s.</summary>
	private Rig MakeRig(EventDef[] pool, int count, long seed = 5)
	{
		StageDef stageDef = DefFactory.Stage(100f);
		stageDef.Events = DefFactory.Schedule(pool, count, 10f, 60f, 20f);
		Node2D layer = Adopt(new Node2D());
		StageClock clock = Adopt(new StageClock());
		clock.SetPhysicsProcess(false);
		EventDirector director = Adopt(new EventDirector());
		var record = new Record();
		director.EventStarted += (def, started) => record.Started.Add((def, started));
		director.EventProgressed += (def, text) => record.Progress.Add((def, text));
		director.EventEnded += (def, success) => record.Ended.Add((def, success));
		director.EventAborted += record.Aborted.Add;
		director.Bind(
			stageDef,
			clock,
			def => new EventContext(layer, null!, null!, null, null, new RunRng(1), default, 0f, def),
			new RunRng(seed),
			[]);
		clock.Start(100f);
		return new Rig(director, clock, layer, record);
	}

	public void TestTimesAreRolledOnceFromTheSeededSchedule()
	{
		EventDirector first = MakeRig(Pool(), 2, 5).Director;
		float[] times = first.EventTimes();
		Assert.Eq(times.Length, 2);
		Assert.True(times[0] >= 10f && times[1] <= 60f);
		Assert.True(times[1] - times[0] >= 20f);
		Assert.SequenceEq(MakeRig(Pool(), 2, 5).Director.EventTimes(), times, "same seed, same times");
		Assert.Eq(first.PlannedDefs().Count, 2);
	}

	public void TestAnEventStartsOnTheTickThatCrossesItsTime()
	{
		EventDef[] pool = Pool();
		Rig rig = MakeRig(pool, 1);
		float time = rig.Director.EventTimes()[0];
		rig.Clock.Step(time - 0.5f);
		Assert.Eq(rig.Record.Started.Count, 0);
		rig.Clock.Step(1f);
		Assert.Eq(rig.Record.Started.Count, 1);
		Assert.True(rig.Record.Started[0].Def == pool[0]);
		var started = (ProbeEvent)rig.Record.Started[0].Event;
		Assert.True(started.GetParent() == rig.Layer, "placed on the context's layer");
		Assert.Eq(started.Begins, 1);
		Assert.True(rig.Director.Running == started);
		Assert.True(started.Context!.Def == pool[0]);
		Assert.Eq(rig.Record.Progress.Count, 1, "progress is relayed");
		Assert.Eq(rig.Record.Progress[0].Text, "Probe probe");
	}

	public async Task TestEventsRunOneAtATime()
	{
		Rig rig = MakeRig(Pool(ProbeScene(endless: true)), 2);
		rig.Clock.Step(rig.Director.EventTimes()[1] + 1f);
		Assert.Eq(rig.Record.Started.Count, 1, "the second waits while the first runs");
		((ProbeEvent)rig.Record.Started[0].Event).Finish(true);
		Assert.Eq(rig.Record.Ended.Count, 1);
		Assert.True(rig.Record.Ended[0].Success);
		Assert.Eq(rig.Record.Started.Count, 1, "the next one starts deferred, not inside the ending callback");
		await NextFrame();
		Assert.Eq(rig.Record.Started.Count, 2, "the waiting event starts once the first has ended");
		Assert.True(rig.Director.Running == rig.Record.Started[1].Event);
		Assert.True(rig.Record.Started[1].Event.IsRunning);
	}

	public void TestSuccessAndFailureAreRelayedAndTheEventIsFreed()
	{
		Rig rig = MakeRig(Pool(), 1);
		rig.Clock.Step(rig.Director.EventTimes()[0] + 0.1f);
		var started = (ProbeEvent)rig.Record.Started[0].Event;
		started.Finish(false);
		Assert.Eq(rig.Record.Ended.Count, 1);
		Assert.False(rig.Record.Ended[0].Success, "failure");
		Assert.Null(rig.Director.Running);
		Assert.True(started.IsQueuedForDeletion());
	}

	public async Task TestAbortRunningEndsWithoutRewardAndDropsWaitingEvents()
	{
		Rig rig = MakeRig(Pool(ProbeScene(endless: true)), 2);
		rig.Clock.Step(rig.Director.EventTimes()[1] + 1f);
		var first = (ProbeEvent)rig.Record.Started[0].Event;
		rig.Director.AbortRunning();
		Assert.Eq(first.Cleanups, 1);
		Assert.True(first.IsQueuedForDeletion(), "the aborted controller is freed");
		Assert.Eq(rig.Record.Ended.Count, 0, "an aborted event is not a result");
		Assert.Eq(rig.Record.Aborted.Count, 1);
		Assert.True(rig.Record.Aborted[0] == first.Context!.Def);
		Assert.Null(rig.Director.Running);
		rig.Director.AbortRunning();
		Assert.Eq(rig.Record.Aborted.Count, 1, "nothing left to abort");
		rig.Clock.Step(5f);
		await NextFrame();
		Assert.Eq(rig.Record.Started.Count, 1, "the waiting event was dropped");
	}

	public async Task TestNoEventStartsInOvertime()
	{
		Rig rig = MakeRig(Pool(), 1);
		float[] before = rig.Director.EventTimes();
		rig.Clock.Step(before[0] + 0.1f);
		((ProbeEvent)rig.Record.Started[0].Event).Finish(true);
		for (int i = 0; i < 10; i++)
		{
			rig.Clock.Step(20f);
		}

		await NextFrame();
		Assert.True(rig.Clock.Elapsed > 100f, "well past the stage end");
		Assert.Eq(rig.Record.Started.Count, 1);
		Assert.Null(rig.Director.Running);
		Assert.SequenceEq(rig.Director.EventTimes(), before, "the plan is untouched by overtime");
	}

	public void TestWithoutAScheduleThereAreNoEvents()
	{
		StageClock clock = Adopt(new StageClock());
		clock.SetPhysicsProcess(false);
		EventDirector director = Adopt(new EventDirector());
		int started = 0;
		director.EventStarted += (_, _) => started++;
		director.Bind(DefFactory.Stage(100f), clock, _ => null!, new RunRng(1), []);
		clock.Start(100f);
		clock.Step(150f);
		Assert.Eq(director.EventTimes().Length, 0);
		Assert.Eq(started, 0);
	}

	public void TestValidateDefsRequiresAStageEventRoot()
	{
		Assert.NoErrors(EventDirector.ValidateDefs(DefFactory.Schedule(Pool())), "probe");
		var errors = EventDirector.ValidateDefs(DefFactory.Schedule([DefFactory.Event(DefFactory.Packed(new Node2D()), 20f, "plain")]));
		Assert.Eq(errors.Count, 1);
		Assert.True(errors[0].Contains("plain"));
		Assert.NoErrors(EventDirector.ValidateDefs(null), "no schedule");
	}

	public void TestAbortingFromAnEventStartedListenerNeverBeginsTheEvent()
	{
		Rig rig = MakeRig(Pool(ProbeScene(endless: true)), 1);
		rig.Director.EventStarted += (_, _) => rig.Director.AbortRunning();
		rig.Clock.Step(rig.Director.EventTimes()[0] + 1f);
		var started = (ProbeEvent)rig.Record.Started[0].Event;
		Assert.Eq(started.Begins, 0, "aborted before it began");
		Assert.Eq(started.Cleanups, 0);
		Assert.False(started.IsRunning);
		Assert.True(started.IsQueuedForDeletion());
		Assert.Eq(rig.Record.Aborted.Count, 1);
		Assert.Eq(rig.Record.Ended.Count, 0);
		Assert.Null(rig.Director.Running);
	}
}
