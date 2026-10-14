using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class EnemyDefTest : TestCase
{
	public void TestFactoryEnemyIsValid()
	{
		Assert.NoErrors(DefFactory.Enemy().Validate());
	}

	public void TestEmptyIdAndNullSceneAreReported()
	{
		EnemyDef def = DefFactory.Enemy();
		def.Id = string.Empty;
		def.Scene = null;
		Assert.Eq(def.Validate().Count, 2);
	}

	public void TestNonPositiveNumbersAreOneError()
	{
		EnemyDef def = DefFactory.Enemy();
		def.BodyRadius = 0f;
		def.ContactInterval = 0f;
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestNullVisualAndStatsAreReportedAndNestedErrorsPropagate()
	{
		EnemyDef def = DefFactory.Enemy();
		def.Visual = null;
		def.Stats = new StatBlock { MaxHealth = 0f };
		Assert.Eq(def.Validate().Count, 2, "null visual + the stat block's own error");
	}
}
