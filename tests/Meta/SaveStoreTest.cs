using Godot;
using VampireSurvivor.Meta;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Meta;

public class SaveStoreTest : TestCase
{
	private const string Dir = "user://test_save_store";
	private const string Path = Dir + "/save.json";

	public override void BeforeEach() => SaveFiles.Reset(Dir);

	public override void AfterEach() => SaveFiles.Reset(Dir);

	private static bool HasBackup() => FileAccess.FileExists(Path + ".bak");

	public void TestMissingFileGivesDefaultsAndNoBackup()
	{
		Assert.Eq(SaveStore.LoadOrDefault(Path).Gem, 0L);
		Assert.False(HasBackup());
	}

	public void TestWriteThenLoadRoundTrips()
	{
		var data = new SaveData { Gem = 12 };
		data.Settings.MasterVolume = 0.4f;
		Assert.True(SaveStore.Write(Path, data));
		SaveData loaded = SaveStore.LoadOrDefault(Path);
		Assert.Eq(loaded.Gem, 12L);
		Assert.AlmostEq(loaded.Settings.MasterVolume, 0.4);
	}

	public void TestWriteLeavesNoTempFileAndOverwritesAnExistingSave()
	{
		Assert.True(SaveStore.Write(Path, new SaveData { Gem = 1 }));
		Assert.True(SaveStore.Write(Path, new SaveData { Gem = 2 }));
		Assert.False(FileAccess.FileExists(Path + ".tmp"));
		Assert.Eq(SaveStore.LoadOrDefault(Path).Gem, 2L);
	}

	public void TestWriteToAMissingDirectoryFailsWithoutCrashing()
	{
		Assert.False(SaveStore.Write(Dir + "/missing_dir/save.json", new SaveData()));
	}

	public void TestCorruptJsonIsMovedToBackupAndDefaultsAreUsed()
	{
		SaveFiles.WriteText(Path, "{ not json");
		Assert.Eq(SaveStore.LoadOrDefault(Path).Gem, 0L);
		Assert.False(FileAccess.FileExists(Path), "the unreadable file is moved away");
		Assert.Eq(FileAccess.GetFileAsString(Path + ".bak"), "{ not json", "the player's bytes are kept");
	}

	public void TestValidJsonThatIsNotAnObjectIsTreatedAsCorrupt()
	{
		foreach (string text in new[] { "[]", "42", "null", "\"hi\"" })
		{
			SaveFiles.Reset(Dir);
			SaveFiles.WriteText(Path, text);
			Assert.Eq(SaveStore.LoadOrDefault(Path).Gem, 0L, text);
			Assert.True(HasBackup(), $"backup for {text}");
		}
	}

	public void TestEmptyFileIsTreatedAsCorrupt()
	{
		SaveFiles.WriteText(Path, string.Empty);
		Assert.Eq(SaveStore.LoadOrDefault(Path).Gem, 0L);
		Assert.True(HasBackup());
	}

	public void TestWrongFieldTypeIsTreatedAsCorrupt()
	{
		SaveFiles.WriteText(Path, """{"version": 1, "gem": "lots"}""");
		Assert.Eq(SaveStore.LoadOrDefault(Path).Gem, 0L);
		Assert.True(HasBackup());
	}

	public void TestFutureVersionIsTreatedAsCorruptAndPreserved()
	{
		SaveFiles.WriteText(Path, """{"version": 999, "gem": 5}""");
		Assert.Eq(SaveStore.LoadOrDefault(Path).Gem, 0L);
		Assert.True(FileAccess.GetFileAsString(Path + ".bak").Contains("999"));
	}

	public void TestASecondCorruptFileReplacesAnExistingBackup()
	{
		SaveFiles.WriteText(Path + ".bak", "old backup");
		SaveFiles.WriteText(Path, "garbage");
		Assert.Eq(SaveStore.LoadOrDefault(Path).Gem, 0L);
		Assert.Eq(FileAccess.GetFileAsString(Path + ".bak"), "garbage");
	}

	public void TestGemStoredAsAFloatInTheFileLoads()
	{
		SaveFiles.WriteText(Path, """{"version": 1, "gem": 5.0}""");
		Assert.Eq(SaveStore.LoadOrDefault(Path).Gem, 5L);
		Assert.False(HasBackup());
	}

	public void TestAV1FileLoadsMigratedWithoutABackup()
	{
		SaveFiles.WriteText(Path, """{"version": 1, "gem": 5, "settings": {"master_volume": 0.5}}""");
		SaveData data = SaveStore.LoadOrDefault(Path);
		Assert.Eq(data.Gem, 5L);
		Assert.AlmostEq(data.Settings.MasterVolume, 0.5);
		Assert.Eq(data.ClearedStages.Count, 0);
		Assert.False(HasBackup());
	}

	public void TestClearedStagesSurviveWriteAndLoad()
	{
		var data = new SaveData();
		data.MarkCleared("stage_01");
		Assert.True(SaveStore.Write(Path, data));
		Assert.SequenceEq(SaveStore.LoadOrDefault(Path).ClearedStages, new[] { "stage_01" });
	}

	public void TestMalformedClearedStagesAreTreatedAsCorrupt()
	{
		foreach (string text in new[] { """{"version": 2, "cleared_stages": [1]}""", """{"version": 2, "cleared_stages": "stage_01"}""" })
		{
			SaveFiles.Reset(Dir);
			SaveFiles.WriteText(Path, text);
			Assert.Eq(SaveStore.LoadOrDefault(Path).ClearedStages.Count, 0, text);
			Assert.True(HasBackup(), $"backup for {text}");
		}
	}

	public void TestAV2FileLoadsMigratedWithoutABackup()
	{
		SaveFiles.WriteText(Path, """{"version": 2, "gem": 5, "cleared_stages": ["stage_01"]}""");
		SaveData data = SaveStore.LoadOrDefault(Path);
		Assert.Eq(data.Gem, 5L);
		Assert.SequenceEq(data.ClearedStages, new[] { "stage_01" });
		Assert.Eq(data.SkillLevels.Count, 0);
		Assert.False(HasBackup());
	}

	public void TestSkillLevelsSurviveWriteAndLoad()
	{
		var data = new SaveData();
		data.SetLevel("vitality", 2);
		Assert.True(SaveStore.Write(Path, data));
		Assert.Eq(SaveStore.LoadOrDefault(Path).LevelOf("vitality"), 2);
	}

	public void TestMalformedSkillLevelsAreTreatedAsCorrupt()
	{
		foreach (string text in new[] { """{"version": 3, "skill_levels": [1]}""", """{"version": 3, "skill_levels": {"vitality": -1}}""", """{"version": 3, "skill_levels": {"vitality": "x"}}""" })
		{
			SaveFiles.Reset(Dir);
			SaveFiles.WriteText(Path, text);
			Assert.Eq(SaveStore.LoadOrDefault(Path).SkillLevels.Count, 0, text);
			Assert.True(HasBackup(), $"backup for {text}");
		}
	}

	public void TestDuplicateKeysOrTrailingGarbageAreTreatedAsCorrupt()
	{
		foreach (string text in new[] { """{"version": 3} trailing""", """{"version": 3, "gem": 1, "gem": 2}""" })
		{
			SaveFiles.Reset(Dir);
			SaveFiles.WriteText(Path, text);
			Assert.Eq(SaveStore.LoadOrDefault(Path).Gem, 0L, text);
			Assert.True(HasBackup(), $"backup for {text}");
		}
	}

	public void TestADuplicateKeyInsideANestedObjectIsTreatedAsCorrupt()
	{
		foreach (string text in new[]
		{
			"""{"version": 3, "gem": 7, "settings": {"master_volume": 0.5, "master_volume": 0.6}}""",
			"""{"version": 3, "gem": 7, "skill_levels": {"a": 1, "a": 2}}""",
			"""{"version": 3, "gem": 7, "cleared_stages": [{"x": 1, "x": 2}]}""",
		})
		{
			SaveFiles.Reset(Dir);
			SaveFiles.WriteText(Path, text);
			Assert.Eq(SaveStore.LoadOrDefault(Path).Gem, 0L, text);
			Assert.True(HasBackup(), $"backup for {text}");
		}
	}
}
