using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class StageDefTest : TestCase
{
	public void TestFactoryStageIsValidEvenWithoutTrickleEnemies()
	{
		Assert.NoErrors(DefFactory.Stage().Validate(), "an empty trickle list is allowed");
	}

	public void TestStageCollectsErrorsFromNestedDefs()
	{
		StageDef def = DefFactory.Stage();
		def.BuildingStats!.MaxHealth = 0f;
		EnemyDef broken = DefFactory.Enemy();
		broken.Stats = null;
		def.TrickleEnemies.Add(broken);
		Assert.True(def.Validate().Count >= 2, string.Join("; ", def.Validate()));
	}

	public void TestStageRequiresPositiveDurationAndTextures()
	{
		StageDef def = DefFactory.Stage(0f);
		def.GroundTexture = null;
		Assert.Eq(def.Validate().Count, 2, string.Join("; ", def.Validate()));
	}

	public void TestStageValidatesWavesAgainstItsOwnDuration()
	{
		StageDef def = DefFactory.Stage(60f);
		def.Waves!.Entries.Add(new WaveEntry { Time = 90f, Count = 3, Enemy = DefFactory.Enemy() });
		Assert.Eq(def.Validate().Count, 1, string.Join("; ", def.Validate()));
	}

	public void TestStageRequiresASaneEconomy()
	{
		StageDef def = DefFactory.Stage();
		def.StartGold = -1;
		def.DropMultiplier = 0f;
		def.TrickleCap = -1;
		Assert.Eq(def.Validate().Count, 3, string.Join("; ", def.Validate()));
	}
}
