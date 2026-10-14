using VampireSurvivor.Meta;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Meta;

public class StageProgressTest : TestCase
{
	private static StageProgress Progress(params string[] cleared)
	{
		var save = new SaveData();
		foreach (string id in cleared)
		{
			save.MarkCleared(id);
		}

		return new StageProgress(DefFactory.Catalog("a", "b", "c"), save);
	}

	public void TestTheFirstStageIsAlwaysAvailable()
	{
		StageProgress progress = Progress();
		Assert.Eq(progress.Status(0), StageStatus.Available);
		Assert.True(progress.IsUnlocked(0));
	}

	public void TestLaterStagesAreLockedUntilThePreviousIsCleared()
	{
		StageProgress progress = Progress();
		Assert.Eq(progress.Status(1), StageStatus.Locked);
		Assert.False(progress.IsUnlocked(1));
		Assert.Eq(progress.Status(2), StageStatus.Locked);
	}

	public void TestClearingAStageUnlocksTheNextOneOnly()
	{
		StageProgress progress = Progress("a");
		Assert.Eq(progress.Status(0), StageStatus.Cleared);
		Assert.Eq(progress.Status(1), StageStatus.Available);
		Assert.Eq(progress.Status(2), StageStatus.Locked);
	}

	public void TestAClearedStageStaysPlayable() => Assert.True(Progress("a").IsUnlocked(0));

	public void TestAnIdNotInTheCatalogIsIgnored()
	{
		StageProgress progress = Progress("ghost");
		Assert.Eq(progress.Status(0), StageStatus.Available);
		Assert.Eq(progress.Status(1), StageStatus.Locked);
	}

	public void TestAnOutOfRangeIndexIsLocked()
	{
		StageProgress progress = Progress("a", "b", "c");
		Assert.Eq(progress.Status(3), StageStatus.Locked);
		Assert.Eq(progress.Status(-1), StageStatus.Locked);
		Assert.False(progress.IsUnlocked(3));
	}
}
