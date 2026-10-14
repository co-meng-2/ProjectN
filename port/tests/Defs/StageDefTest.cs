using Godot;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class StageDefTest : TestCase
{
	public void TestStageValidatesItsEventScheduleAgainstDurationAndWaves()
	{
		StageDef def = DefFactory.Stage(60f);
		def.Waves!.Entries.Add(new WaveEntry { Time = 30f, Count = 3, Enemy = DefFactory.Enemy() });
		def.Events = DefFactory.Schedule([DefFactory.Event(DefFactory.Packed(new Node2D()), 10f)], 1, 25f, 35f, 10f, 6f);
		Assert.Eq(def.Validate().Count, 1, "every allowed second collides with the wave at 30 s");
		def.Events.WaveClearance = 4f;
		Assert.NoErrors(def.Validate(), "clearance 4 leaves 25 and 35");
		def.Events = null;
		Assert.NoErrors(def.Validate(), "events are optional");
	}

	public void TestANullWaveEntryWithEventsReportsErrorsWithoutCrashing()
	{
		StageDef def = DefFactory.Stage(60f);
		def.Waves!.Entries.Add(null!);
		def.Events = DefFactory.Schedule([DefFactory.Event(DefFactory.Packed(new Node2D()), 10f)], 1, 25f, 35f, 10f);
		var errors = def.Validate();
		Assert.Eq(errors.Count, 1, string.Join(", ", errors));
		Assert.True(errors[0].Contains("null entry"));
	}

	public void TestStageValidatesItsOptionalBoss()
	{
		StageDef def = DefFactory.Stage();
		Assert.Null(def.Boss, "no boss by default");
		def.Boss = new BossDef();
		Assert.Eq(def.Validate().Count, 7, "the boss's errors are the stage's errors");
		def.Boss = DefFactory.Boss(null, new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe });
		Assert.NoErrors(def.Validate(), "valid boss");
	}

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

	public void TestStageRequiresAValidGradeTable()
	{
		StageDef def = DefFactory.Stage();
		def.GradeTable = null;
		Assert.Eq(def.Validate().Count, 1, "missing");
		def.GradeTable = new GradeTable();
		Assert.Eq(def.Validate().Count, 1, "nested: wrong promote_gold size");
	}

	public void TestStageRequiresASaneEconomy()
	{
		StageDef def = DefFactory.Stage();
		def.StartGold = -1;
		def.DropMultiplier = 0f;
		def.TrickleCap = -1;
		Assert.Eq(def.Validate().Count, 3, string.Join("; ", def.Validate()));
	}

	public void TestGemRewardIsPaidOnClearOnly()
	{
		StageDef def = DefFactory.Stage();
		def.GemReward = 7;
		Assert.Eq(def.GemFor(RunOutcome.Cleared), 7);
		Assert.Eq(def.GemFor(RunOutcome.Failed), 0);
	}

	public void TestGemRewardDefaultsToZero() => Assert.Eq(DefFactory.Stage().GemReward, 0);

	public void TestNegativeGemRewardIsAValidationError()
	{
		StageDef def = DefFactory.Stage();
		def.GemReward = -1;
		Assert.Eq(def.Validate().Count, 1);
	}
}
