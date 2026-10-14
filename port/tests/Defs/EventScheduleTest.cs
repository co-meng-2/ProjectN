using System.Collections.Generic;
using System.Linq;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class EventScheduleTest : TestCase
{
	private static readonly float[] Stage01Waves = [30f, 60f, 90f, 120f, 150f];

	private static EventDef[] Pool(float timeLimit = 20f) => [DefFactory.Event(DefFactory.Packed(new Node2D()), timeLimit)];

	private static EventSchedule Stage01Like() => DefFactory.Schedule(Pool(25f), 3, 20f, 155f, 30f, 5f);

	public void TestAllowedSecondsSkipTheWaveClearanceAndStayInTheWindow()
	{
		EventSchedule schedule = DefFactory.Schedule(Pool(), 1, 20f, 40f, 20f, 5f);
		Assert.SequenceEq(schedule.AllowedSeconds(100f, [30f]), new[] { 20, 21, 22, 23, 24, 36, 37, 38, 39, 40 });
	}

	public void TestRolledTimesRespectWindowGapAndClearanceForManySeeds()
	{
		EventSchedule schedule = Stage01Like();
		int[] allowed = schedule.AllowedSeconds(180f, Stage01Waves);
		for (int seed = 1; seed <= 40; seed++)
		{
			float[] times = schedule.RollTimes(180f, Stage01Waves, new RunRng(seed));
			Assert.Eq(times.Length, 3, $"seed {seed}");
			for (int i = 0; i < times.Length; i++)
			{
				Assert.True(allowed.Contains((int)times[i]), $"seed {seed}: {times[i]} is allowed");
				Assert.AlmostEq(times[i], Mathf.Round(times[i]), 0.0001, "whole seconds");
				if (i > 0)
				{
					Assert.True(times[i] - times[i - 1] >= 30f, $"seed {seed}: gap");
				}
			}
		}
	}

	public void TestRollingIsSeededAndNotAlwaysTheEarliest()
	{
		EventSchedule schedule = Stage01Like();
		Assert.SequenceEq(schedule.RollTimes(180f, Stage01Waves, new RunRng(8)), schedule.RollTimes(180f, Stage01Waves, new RunRng(8)));
		var firsts = new HashSet<float>();
		for (int seed = 1; seed <= 20; seed++)
		{
			firsts.Add(schedule.RollTimes(180f, Stage01Waves, new RunRng(seed))[0]);
		}

		Assert.True(firsts.Count > 3, $"first times vary with the seed (got {firsts.Count} distinct)");
	}

	public void TestATightScheduleHasExactlyOnePlacement()
	{
		EventSchedule schedule = DefFactory.Schedule(Pool(10f), 3, 10f, 30f, 10f);
		for (int seed = 1; seed <= 5; seed++)
		{
			Assert.SequenceEq(schedule.RollTimes(100f, [], new RunRng(seed)), new[] { 10f, 20f, 30f });
		}
	}

	public void TestEachPickLeavesRoomForTheRest()
	{
		EventSchedule schedule = DefFactory.Schedule(Pool(20f), 2, 10f, 40f, 25f);
		for (int seed = 1; seed <= 30; seed++)
		{
			float[] times = schedule.RollTimes(100f, [], new RunRng(seed));
			Assert.Eq(times.Length, 2, $"seed {seed}");
			Assert.True(times[0] <= 15f, $"seed {seed}: the first event must leave room for the second (got {times[0]})");
			Assert.True(times[1] >= times[0] + 25f);
		}
	}

	public void TestAnInfeasibleScheduleRollsNothingAndFailsValidation()
	{
		EventSchedule schedule = DefFactory.Schedule(Pool(10f), 4, 10f, 30f, 10f);
		Assert.Eq(schedule.RollTimes(100f, [], new RunRng(3)).Length, 0);
		Assert.Eq(schedule.EarliestTimes(100f, []).Length, 3);
		var errors = schedule.ValidateAgainst(100f, []);
		Assert.Eq(errors.Count, 1, string.Join(", ", errors));
		Assert.True(errors[0].Contains("cannot place"));
	}

	public void TestWaveClearanceCanMakeAScheduleInfeasible()
	{
		EventSchedule schedule = DefFactory.Schedule(Pool(10f), 1, 25f, 35f, 10f, 6f);
		Assert.Eq(schedule.ValidateAgainst(100f, [30f]).Count, 1, "every second is within 6 s of the wave");
		schedule.WaveClearance = 4f;
		Assert.NoErrors(schedule.ValidateAgainst(100f, [30f]), "25 and 35 are clear");
	}

	public void TestValidationRejectsBadWindowsCountsAndShortGaps()
	{
		Assert.Eq(DefFactory.Schedule(Pool(20f), 1, 0f, 50f, 20f).ValidateAgainst(100f, []).Count, 1, "window starts at 0");
		Assert.Eq(DefFactory.Schedule(Pool(20f), 1, 10f, 100f, 20f).ValidateAgainst(100f, []).Count, 2, "window reaches the stage end");
		Assert.Eq(DefFactory.Schedule(Pool(20f), -1, 10f, 50f, 20f).ValidateAgainst(100f, []).Count, 1, "negative count");
		var errors = DefFactory.Schedule(Pool(25f), 1, 10f, 50f, 20f).ValidateAgainst(100f, []);
		Assert.Eq(errors.Count, 1, "min_gap shorter than the longest time limit");
		Assert.True(errors[0].Contains("min_gap"));
	}

	public void TestTheWindowMustLeaveTimeForTheLongestEventToFinish()
	{
		EventSchedule schedule = DefFactory.Schedule(Pool(25f), 1, 20f, 155f, 30f, 5f);
		Assert.NoErrors(schedule.ValidateAgainst(180f, Stage01Waves), "155 + 25 = 180 still finishes in time");
		schedule.WindowEnd = 160f;
		var errors = schedule.ValidateAgainst(180f, Stage01Waves);
		Assert.Eq(errors.Count, 1, "fails by this rule only: " + string.Join(", ", errors));
		Assert.True(errors[0].Contains("finish"));
	}

	public void TestValidationReportsNullAndInvalidEventDefsAndAnEmptyPool()
	{
		var pool = new List<EventDef?>(Pool()) { null, new EventDef() };
		var errors = DefFactory.Schedule(pool).ValidateAgainst(100f, []);
		Assert.Eq(errors.Count, 6, "1 null + 5 from the empty def: " + string.Join(", ", errors));
		Assert.Eq(DefFactory.Schedule([]).ValidateAgainst(100f, []).Count, 1, "count 1 with nothing to pick");
	}

	public void TestZeroCountRollsNothingAndValidates()
	{
		EventSchedule schedule = DefFactory.Schedule(Pool(), 0);
		Assert.Eq(schedule.RollTimes(100f, [], new RunRng(1)).Length, 0);
		Assert.NoErrors(schedule.ValidateAgainst(100f, []), "no events");
	}

	public void TestRollDefsNeverRepeatsWhenThereAreAlternatives()
	{
		EventDef a = DefFactory.Event(DefFactory.Packed(new Node2D()), 20f, "a");
		EventDef b = DefFactory.Event(DefFactory.Packed(new Node2D()), 20f, "b");
		List<EventDef> picks = DefFactory.Schedule([a, b, null]).RollDefs(20, new RunRng(4));
		Assert.Eq(picks.Count, 20);
		for (int i = 1; i < picks.Count; i++)
		{
			Assert.False(picks[i] == picks[i - 1], $"pick {i} repeats");
		}

		Assert.True(picks.Contains(a) && picks.Contains(b));
		List<EventDef> repeats = DefFactory.Schedule([a]).RollDefs(3, new RunRng(4));
		Assert.True(repeats.Count == 3 && repeats.All(d => d == a), "no alternative: the same def repeats");
	}
}
