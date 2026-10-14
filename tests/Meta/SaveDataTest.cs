using VampireSurvivor.Meta;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Meta;

public class SaveDataTest : TestCase
{
	private static SaveData? Load(string json) => SaveData.FromJson(SaveJson.Object(json));

	public void TestDefaults()
	{
		var data = new SaveData();
		Assert.Eq(data.Gem, 0L);
		Assert.AlmostEq(data.Settings.MasterVolume, 1.0);
	}

	public void TestToJsonCarriesTheCurrentVersion() => SaveJson.Eq(new SaveData().ToJson()["version"], SaveMigrations.CurrentVersion.ToString());

	public void TestRoundTrip()
	{
		var data = new SaveData { Gem = 42 };
		data.Settings.MasterVolume = 0.5f;
		SaveData? restored = SaveData.FromJson(data.ToJson());
		Assert.NotNull(restored);
		Assert.Eq(restored!.Gem, 42L);
		Assert.AlmostEq(restored.Settings.MasterVolume, 0.5);
	}

	public void TestMissingFieldsGetDefaults()
	{
		SaveData restored = Load("""{"version": 1}""")!;
		Assert.Eq(restored.Gem, 0L);
		Assert.AlmostEq(restored.Settings.MasterVolume, 1.0);
	}

	public void TestGemWrittenAsAJsonFloatLoadsAsAWholeNumber() => Assert.Eq(Load("""{"gem": 5.0}""")!.Gem, 5L);

	public void TestInvalidGemIsRejected()
	{
		Assert.Null(Load("""{"gem": 2.5}"""), "fraction");
		Assert.Null(Load("""{"gem": "5"}"""), "string");
		Assert.Null(Load("""{"gem": -1}"""), "negative");
		Assert.Null(Load("""{"gem": 1.0e30}"""), "huge");
		Assert.Null(Load("""{"gem": null}"""), "null");
	}

	public void TestInvalidSettingsAreRejected()
	{
		Assert.Null(Load("""{"settings": 3}"""), "not an object");
		Assert.Null(Load("""{"settings": {"master_volume": "x"}}"""), "bad field");
	}

	public void TestClearedStagesDefaultToEmpty() => Assert.Eq(new SaveData().ClearedStages.Count, 0);

	public void TestMarkClearedIgnoresAnIdAlreadyPresent()
	{
		var data = new SaveData();
		data.MarkCleared("stage_01");
		data.MarkCleared("stage_01");
		Assert.SequenceEq(data.ClearedStages, new[] { "stage_01" });
	}

	public void TestClearedStagesRoundTrip()
	{
		var data = new SaveData();
		data.MarkCleared("stage_01");
		data.MarkCleared("stage_02");
		Assert.SequenceEq(SaveData.FromJson(data.ToJson())!.ClearedStages, new[] { "stage_01", "stage_02" });
	}

	public void TestClearedStagesAreStoredAsPlainStrings()
	{
		var data = new SaveData();
		data.MarkCleared("stage_01");
		SaveJson.Eq(data.ToJson()["cleared_stages"], """["stage_01"]""");
	}

	public void TestAnUnknownStageIdSurvivesLoadAndSave()
	{
		SaveJson.Eq(Load("""{"cleared_stages": ["ghost_stage"]}""")!.ToJson()["cleared_stages"], """["ghost_stage"]""");
	}

	public void TestInvalidClearedStagesAreRejected()
	{
		Assert.Null(Load("""{"cleared_stages": "stage_01"}"""), "not an array");
		Assert.Null(Load("""{"cleared_stages": [1]}"""), "number entry");
		Assert.Null(Load("""{"cleared_stages": ["stage_01", null]}"""), "null entry");
	}

	public void TestSkillLevelsDefaultToEmptyAndLevelOfIsZero()
	{
		var data = new SaveData();
		Assert.Eq(data.SkillLevels.Count, 0);
		Assert.Eq(data.LevelOf("vitality"), 0);
	}

	public void TestSetLevelStoresAndZeroRemoves()
	{
		var data = new SaveData();
		data.SetLevel("vitality", 2);
		Assert.Eq(data.LevelOf("vitality"), 2);
		data.SetLevel("vitality", 0);
		Assert.False(data.SkillLevels.ContainsKey("vitality"));
	}

	public void TestSpendGemDeductsOrChangesNothing()
	{
		var data = new SaveData { Gem = 10 };
		Assert.True(data.SpendGem(4));
		Assert.Eq(data.Gem, 6L);
		Assert.False(data.SpendGem(7), "short");
		Assert.Eq(data.Gem, 6L);
		Assert.False(data.SpendGem(-1), "negative");
		Assert.Eq(data.Gem, 6L);
	}

	public void TestSkillLevelsRoundTrip()
	{
		var data = new SaveData();
		data.SetLevel("vitality", 2);
		data.SetLevel("might", 1);
		SaveData restored = SaveData.FromJson(data.ToJson())!;
		Assert.Eq(restored.LevelOf("vitality"), 2);
		Assert.Eq(restored.LevelOf("might"), 1);
	}

	public void TestSkillLevelWrittenAsAJsonFloatLoadsAsAWholeNumber() => Assert.Eq(Load("""{"skill_levels": {"vitality": 2.0}}""")!.LevelOf("vitality"), 2);

	public void TestAnUnknownSkillIdSurvivesLoadAndSave()
	{
		SaveJson.Eq(Load("""{"skill_levels": {"ghost_skill": 3}}""")!.ToJson()["skill_levels"], """{"ghost_skill": 3}""");
	}

	public void TestInvalidSkillLevelsAreRejected()
	{
		Assert.Null(Load("""{"skill_levels": [1]}"""), "not an object");
		Assert.Null(Load("""{"skill_levels": {"vitality": "2"}}"""), "string level");
		Assert.Null(Load("""{"skill_levels": {"vitality": 1.5}}"""), "fractional level");
		Assert.Null(Load("""{"skill_levels": {"vitality": -1}}"""), "negative level");
		Assert.Null(Load("""{"skill_levels": {"vitality": null}}"""), "null level");
		Assert.Null(Load("""{"skill_levels": {"vitality": 3000000000}}"""), "beyond int");
	}
}
