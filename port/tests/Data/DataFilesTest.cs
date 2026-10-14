using System.Collections.Generic;
using System.Linq;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Data;

/// <summary>Scans port/data instead of keeping a hand-written list (spec section 5).</summary>
public class DataFilesTest : TestCase
{
	private const string DataRoot = "res://port/data";
	private const string Stage01 = "res://port/data/stages/stage_01.tres";
	private const string Profile = "res://port/data/characters/default_profile.tres";

	public void TestEveryDataFileLoads()
	{
		List<string> files = [.. SourceTree.Files(DataRoot, ".tres")];
		Assert.True(files.Count >= 11, $"found {files.Count} files");
		foreach (string path in files)
		{
			Assert.NotNull(ResourceLoader.Load(path), path);
		}
	}

	public void TestStage01AndTheDefaultProfileValidate()
	{
		Assert.NoErrors(GD.Load<StageDef>(Stage01).Validate(), "stage_01");
		Assert.NoErrors(GD.Load<PlayerProfile>(Profile).Validate(), "default_profile");
	}

	public void TestStage01SchedulesFiveWavesInsideTheStage()
	{
		StageDef stage = GD.Load<StageDef>(Stage01);
		Assert.SequenceEq(stage.Waves!.Times(), new[] { 30f, 60f, 90f, 120f, 150f });
		Assert.AlmostEq(stage.Duration, 180.0);
	}

	public void TestStage01StartsWithTheOwnerEconomy()
	{
		StageDef stage = GD.Load<StageDef>(Stage01);
		Assert.Eq(stage.StartGold, 200);
		Assert.Eq(stage.StartStone, 0);
		Assert.AlmostEq(stage.DropMultiplier, 10000.0, 0.001, "prototype tuning, as in data/stages/stage_01.tres");
		Assert.AlmostEq(stage.BuildingStats!.MaxHealth, 500.0);
	}

	public void TestEnemyDefsUseTheGenericEnemySceneAndHaveADeathStrip()
	{
		foreach (string path in new[] { "res://port/data/enemies/orc.tres", "res://port/data/enemies/orc_swarmer.tres" })
		{
			EnemyDef def = GD.Load<EnemyDef>(path);
			Assert.Eq(def.Scene!.ResourcePath, "res://port/src/Actors/Enemy/enemy.tscn", path);
			Assert.NotNull(def.Visual!.FindStrip(AnimNames.Death), $"{path} has a death strip");
		}

		Assert.Eq(GD.Load<EnemyDef>("res://port/data/enemies/orc_swarmer.tres").TargetPolicy, TargetPolicy.Player);
	}

	public void TestTheDefaultProfileStartsWithTheArrowAndTheDash()
	{
		PlayerProfile profile = GD.Load<PlayerProfile>(Profile);
		var tags = profile.InnateAbilities.Select(a => a.Tag).ToList();
		Assert.SequenceEq(tags, new[] { "ability.arrow", "ability.dash" });
		var arrow = (VolleyDef)profile.InnateAbilities[0];
		Assert.True(arrow.AutoFire);
		Assert.AlmostEq(arrow.Damage!.Sample(1f), 12.0);
		Assert.AlmostEq(arrow.Cooldown!.Sample(6f), 0.7);
		Assert.AlmostEq(arrow.ArrowCount!.Sample(3f), 1.0);
		Assert.False(profile.InnateAbilities[1].AutoFire, "the dash is on demand");
	}

	public void TestTheShippedStageStartsWithTheDefaultProfile()
	{
		StageRoot stage = Adopt(GD.Load<PackedScene>("res://port/src/Stage/stage.tscn").Instantiate<StageRoot>());
		Assert.True(stage.Start(GD.Load<StageDef>(Stage01), GD.Load<PlayerProfile>(Profile)), "the exact call DevStage makes");
	}

	private static readonly (string File, string Stat, float Level1, float Level6)[] Passives =
	[
		("prospector", Stats.GoldGain, 1.25f, 3.5f),
		("swift_boots", Stats.MoveSpeed, 110f, 185f),
		("vitality", Stats.MaxHealth, 120f, 340f),
		("might", Stats.Attack, 1.1f, 2f),
	];

	private static AbilityDef Catalog(string file) => GD.Load<AbilityDef>($"res://port/data/abilities/{file}.tres");

	public void TestTheCatalogPassivesScaleWithTheGradeLevel()
	{
		foreach (var (file, stat, level1, level6) in Passives)
		{
			AbilityDef def = Catalog(file);
			Assert.Null(def.Active, $"{file} is a pure passive");
			ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
			double baseValue = actor.Attributes[stat].DisplayValue;
			var handle = actor.ApplyPassive(def.Passive!, 1);
			Assert.AlmostEq(actor.Attributes[stat].DisplayValue, level1, 0.001, $"{file} at level 1");
			actor.RemovePassive(handle);
			Assert.AlmostEq(actor.Attributes[stat].DisplayValue, baseValue, 0.001, $"{file} removed");
			actor.ApplyPassive(def.Passive!, 6);
			Assert.AlmostEq(actor.Attributes[stat].DisplayValue, level6, 0.001, $"{file} at level 6");
		}
	}

	public void TestThePassiveCurvesRiseStrictlyOverTheSixLevels()
	{
		foreach (var (file, _, _, _) in Passives)
		{
			Curve curve = Catalog(file).Passive!.ByLevel!;
			for (int level = 2; level <= 6; level++)
			{
				Assert.True(curve.Sample(level) > curve.Sample(level - 1), $"{file} level {level}");
			}
		}
	}

	public void TestTheProfileOffersTheSevenCatalogAbilitiesAndEachValidates()
	{
		PlayerProfile profile = GD.Load<PlayerProfile>(Profile);
		var ids = profile.Abilities.Select(a => a.Id.ToString()).ToList();
		Assert.SequenceEq(ids, new[] { "prospector", "swift_boots", "vitality", "might", "volley", "blade_sweep", "bounty" });
		foreach (AbilityDef def in profile.Abilities)
		{
			Assert.NoErrors(def.Validate(), def.Id);
			if (def.Active is not null)
			{
				Assert.NoErrors(AbilityFactory.Validate(def.Active), def.Id);
			}
		}

		Assert.True(Catalog("volley").Active is VolleyDef { AutoFire: true });
		Assert.True(Catalog("blade_sweep").Active is BladeSweepDef { AutoFire: true });
		Assert.Eq(Catalog("bounty").Active!.TriggerEventTag, GameplayTags.EventKill);
	}
}
