using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class WaveScheduleTest : TestCase
{
	private static WaveEntry MakeEntry(float time, int count = 5) => new()
	{
		Time = time,
		Count = count,
		Enemy = DefFactory.Enemy(),
	};

	public void TestSortedEntriesOrdersByTime()
	{
		var schedule = new WaveSchedule();
		schedule.Entries.Add(MakeEntry(40f));
		schedule.Entries.Add(MakeEntry(10f));
		Assert.Eq(schedule.SortedEntries()[0].Time, 10f);
		Assert.SequenceEq(schedule.Times(), new[] { 10f, 40f });
	}

	public void TestWaveAfterStageEndIsAnError()
	{
		var schedule = new WaveSchedule();
		schedule.Entries.Add(MakeEntry(500f));
		Assert.Eq(schedule.ValidateAgainst(100f).Count, 1);
	}

	public void TestWaveAtExactlyTheStageEndIsAnError()
	{
		var schedule = new WaveSchedule();
		schedule.Entries.Add(MakeEntry(100f));
		Assert.Eq(schedule.ValidateAgainst(100f).Count, 1);
	}

	public void TestNonPositiveCountAndNullEnemyAreErrors()
	{
		var schedule = new WaveSchedule();
		WaveEntry badCount = MakeEntry(5f, 0);
		WaveEntry noEnemy = MakeEntry(6f);
		noEnemy.Enemy = null;
		schedule.Entries.Add(badCount);
		schedule.Entries.Add(noEnemy);
		Assert.Eq(schedule.ValidateAgainst(100f).Count, 2);
	}
}
