using System.Collections.Generic;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Tests.Stage;

public class StageClockTest : TestCase
{
	private StageClock MakeClock()
	{
		StageClock clock = Adopt(new StageClock());
		clock.SetPhysicsProcess(false);
		return clock;
	}

	public void TestTicksReportElapsedTime()
	{
		StageClock clock = MakeClock();
		var seen = new List<float>();
		clock.Ticked += seen.Add;
		clock.Start(10f);
		clock.Step(1.5f);
		clock.Step(0.5f);
		Assert.SequenceEq(seen, new[] { 1.5f, 2f });
	}

	public void TestEndedFiresOnceAndTheClockKeepsTickingIntoOvertime()
	{
		StageClock clock = MakeClock();
		int ended = 0;
		var seen = new List<float>();
		clock.Ended += () => ended++;
		clock.Ticked += seen.Add;
		clock.Start(2f);
		clock.Step(1f);
		Assert.False(clock.HasEnded);
		clock.Step(4f);
		Assert.Eq(ended, 1);
		Assert.True(clock.HasEnded);
		Assert.True(clock.IsRunning, "overtime: the boss fight runs on the same clock");
		clock.Step(1.5f);
		Assert.Eq(ended, 1, "ended fires once");
		Assert.AlmostEq(clock.Elapsed, 6.5);
		Assert.SequenceEq(seen, new[] { 1f, 5f, 6.5f });
	}

	public void TestStoppedClockDoesNotTick()
	{
		StageClock clock = MakeClock();
		var seen = new List<float>();
		clock.Ticked += seen.Add;
		clock.Start(10f);
		clock.Stop();
		clock.Step(1f);
		Assert.Eq(seen.Count, 0);
	}

	public void TestStartResetsElapsed()
	{
		StageClock clock = MakeClock();
		clock.Start(10f);
		clock.Step(4f);
		clock.Start(10f);
		Assert.AlmostEq(clock.Elapsed, 0.0);
		Assert.True(clock.IsRunning);
	}

	public void TestStartClearsTheEndedState()
	{
		StageClock clock = MakeClock();
		int ended = 0;
		clock.Ended += () => ended++;
		clock.Start(1f);
		clock.Step(2f);
		clock.Start(1f);
		Assert.False(clock.HasEnded);
		clock.Step(1f);
		Assert.Eq(ended, 2, "a restarted clock ends again");
	}
}
