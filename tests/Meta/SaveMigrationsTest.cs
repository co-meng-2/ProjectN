using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using VampireSurvivor.Meta;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Meta;

public class SaveMigrationsTest : TestCase
{
	public void TestShippedStepsCoverEveryVersionBelowCurrent()
	{
		Assert.Eq(SaveMigrations.Steps().Count, SaveMigrations.CurrentVersion - 1, "one step per past version");
	}

	public void TestCurrentVersionIsReturnedUnchanged()
	{
		JsonObject migrated = SaveMigrations.Migrate(SaveJson.Object($$"""{"version": {{SaveMigrations.CurrentVersion}}, "gem": 4}"""))!;
		SaveJson.Eq(migrated["gem"], "4");
		SaveJson.Eq(migrated["version"], SaveMigrations.CurrentVersion.ToString());
	}

	public void TestOldSaveIsUpgradedThroughEveryStep()
	{
		var steps = new List<Func<JsonObject, JsonObject>>
		{
			d =>
			{
				d["gem"] = d["coins"]?.DeepClone() ?? 0;
				d.Remove("coins");
				return d;
			},
			d =>
			{
				d["extra"] = true;
				return d;
			},
		};
		JsonObject migrated = SaveMigrations.MigrateWith(SaveJson.Object("""{"version": 1, "coins": 7}"""), steps, 3)!;
		SaveJson.Eq(migrated, """{"version": 3, "gem": 7, "extra": true}""");
	}

	public void TestFutureVersionIsRejected()
	{
		Assert.Null(SaveMigrations.Migrate(SaveJson.Object($$"""{"version": {{SaveMigrations.CurrentVersion + 1}}}""")));
	}

	public void TestMissingOrInvalidVersionIsRejected()
	{
		Assert.Null(SaveMigrations.Migrate(new JsonObject()), "missing");
		Assert.Null(SaveMigrations.Migrate(SaveJson.Object("""{"version": "1"}""")), "string");
		Assert.Null(SaveMigrations.Migrate(SaveJson.Object("""{"version": 0}""")), "zero");
		Assert.Null(SaveMigrations.Migrate(SaveJson.Object("""{"version": 1.5}""")), "fraction");
	}

	public void TestJsonStyleFloatVersionIsAccepted() => Assert.NotNull(SaveMigrations.Migrate(SaveJson.Object("""{"version": 1.0}""")));

	public void TestMigrationDoesNotMutateItsInput()
	{
		JsonObject source = SaveJson.Object("""{"version": 1, "gem": 1}""");
		SaveMigrations.Migrate(source);
		SaveJson.Eq(source, """{"version": 1, "gem": 1}""");
	}

	public void TestReadIntAcceptsWholeNumbersOnly()
	{
		Assert.Eq(SaveMigrations.ReadInt(JsonNode.Parse("5")), (long?)5);
		Assert.Eq(SaveMigrations.ReadInt(JsonNode.Parse("5.0")), (long?)5);
		Assert.Null(SaveMigrations.ReadInt(JsonNode.Parse("2.5")));
		Assert.Null(SaveMigrations.ReadInt(JsonNode.Parse("\"5\"")));
		Assert.Null(SaveMigrations.ReadInt(null));
		Assert.Null(SaveMigrations.ReadInt(JsonNode.Parse("1.0e30")));
		Assert.Null(SaveMigrations.ReadInt(JsonNode.Parse("true")));
		Assert.Null(SaveMigrations.ReadInt(JsonNode.Parse("[5]")));
	}

	public void TestCurrentVersionIsThree() => Assert.Eq(SaveMigrations.CurrentVersion, 3);

	public void TestAV1SaveUpgradesAndKeepsEveryField()
	{
		JsonObject migrated = SaveMigrations.Migrate(SaveJson.Object("""{"version": 1, "gem": 4, "settings": {"master_volume": 0.5}}"""))!;
		SaveJson.Eq(migrated, """{"version": 3, "gem": 4, "settings": {"master_volume": 0.5}, "cleared_stages": [], "skill_levels": {}}""");
	}

	public void TestAV2SaveKeepsItsClearedStages()
	{
		SaveJson.Eq(SaveMigrations.Migrate(SaveJson.Object("""{"version": 2, "cleared_stages": ["stage_01"]}"""))!["cleared_stages"], """["stage_01"]""");
	}

	public void TestAV2SaveUpgradesToV3AndKeepsEveryField()
	{
		JsonObject migrated = SaveMigrations.Migrate(SaveJson.Object("""{"version": 2, "gem": 7, "cleared_stages": ["stage_01"], "settings": {"master_volume": 0.5}}"""))!;
		SaveJson.Eq(migrated, """{"version": 3, "gem": 7, "cleared_stages": ["stage_01"], "settings": {"master_volume": 0.5}, "skill_levels": {}}""");
	}

	public void TestAV3SaveKeepsItsSkillLevels()
	{
		SaveJson.Eq(SaveMigrations.Migrate(SaveJson.Object("""{"version": 3, "skill_levels": {"vitality": 2}}"""))!["skill_levels"], """{"vitality": 2}""");
	}
}
