using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class StageEventTest : TestCase
{
	private (ProbeEvent Event, ActorFixture.EventRecord Record, EventDef Def) Begun(float timeLimit = 12f)
	{
		EventDef def = DefFactory.Event(DefFactory.Packed(new ProbeEvent()), timeLimit);
		ProbeEvent probe = Adopt(new ProbeEvent());
		probe.SetPhysicsProcess(false);
		ActorFixture.EventRecord record = ActorFixture.WatchEvent(probe);
		probe.Begin(ActorFixture.ContextFor(this, def));
		return (probe, record, def);
	}

	public void TestBeginStartsTheTimeLimitAndReportsProgress()
	{
		var (probe, record, def) = Begun(12f);
		Assert.True(probe.IsRunning);
		Assert.AlmostEq(probe.RemainingTime, 12.0);
		Assert.Eq(probe.Begins, 1);
		Assert.SequenceEq(record.Progress, new[] { "Probe probe" });
		Assert.True(probe.Context!.Def == def);
	}

	public void TestRunningOutOfTimeFailsOnceAndCleansUp()
	{
		var (probe, record, _) = Begun(12f);
		probe.Step(5f);
		Assert.AlmostEq(probe.RemainingTime, 7.0);
		Assert.True(probe.IsRunning);
		probe.Step(8f);
		Assert.Eq(record.Failed, 1);
		Assert.Eq(record.Succeeded, 0);
		Assert.Eq(probe.Cleanups, 1);
		Assert.False(probe.IsRunning);
		Assert.True(probe.HasEnded);
		probe.Step(5f);
		Assert.Eq(record.Failed, 1, "no second ending");
	}

	public void TestSuccessEndsExactlyOnce()
	{
		var (probe, record, _) = Begun();
		probe.Finish(true);
		probe.Finish(true);
		probe.Finish(false);
		probe.Step(100f);
		Assert.Eq(record.Succeeded, 1);
		Assert.Eq(record.Failed, 0);
		Assert.Eq(probe.Cleanups, 1);
	}

	public void TestAbortCleansUpWithoutAResult()
	{
		var (probe, record, _) = Begun();
		probe.Abort();
		probe.Abort();
		probe.Finish(true);
		Assert.Eq(probe.Cleanups, 1);
		Assert.Eq(record.Succeeded, 0);
		Assert.Eq(record.Failed, 0);
		Assert.True(probe.HasEnded);
	}

	public void TestBeginTwiceIsIgnored()
	{
		var (probe, _, _) = Begun(12f);
		probe.Step(4f);
		probe.Begin(ActorFixture.ContextFor(this, DefFactory.Event(DefFactory.Packed(new ProbeEvent()), 99f)));
		Assert.Eq(probe.Begins, 1);
		Assert.AlmostEq(probe.RemainingTime, 8.0);
	}

	public void TestAnOverrideOfTimeUpKeepsTheEventRunning()
	{
		EventDef def = DefFactory.Event(DefFactory.Packed(new ProbeEvent()), 3f);
		ProbeEvent probe = Adopt(new ProbeEvent { Endless = true });
		probe.SetPhysicsProcess(false);
		probe.Begin(ActorFixture.ContextFor(this, def));
		probe.Step(10f);
		Assert.True(probe.IsRunning, "the subclass decided what time up means");
		Assert.AlmostEq(probe.RemainingTime, 0.0);
	}
}
