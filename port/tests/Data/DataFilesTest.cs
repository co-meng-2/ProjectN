using System.Collections.Generic;
using System.Linq;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Boss;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Events;
using VampireSurvivor.Gas;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Boss;
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
	public void TestTheEliteOrcMatchesTheSpec()
	{
		EnemyDef elite = GD.Load<EnemyDef>("res://port/data/enemies/elite_orc.tres");
		Assert.NoErrors(elite.Validate(), "elite_orc");
		Assert.Eq(elite.Id.ToString(), "elite_orc");
		Assert.AlmostEq(elite.Stats!.MaxHealth, 300.0);
		Assert.AlmostEq(elite.Stats.MoveSpeed, 70.0);
		Assert.AlmostEq(elite.VisualScale, 1.8);
		Assert.Eq(elite.TargetPolicy, TargetPolicy.Player);
		Assert.AlmostEq(elite.ContactDamage, 15.0);
		Assert.True(elite.Tint.R > 0.9f && elite.Tint.G < 0.5f && elite.Tint.B < 0.5f, "red tint");
	}

	public void TestStage01SchedulesThreeEventsAndTheOrcWarlord()
	{
		StageDef stage = GD.Load<StageDef>(Stage01);
		EventSchedule events = stage.Events!;
		Assert.Eq(events.Count, 3);
		Assert.AlmostEq(events.WindowStart, 20.0);
		Assert.AlmostEq(events.WindowEnd, 155.0);
		Assert.AlmostEq(events.MinGap, 30.0);
		Assert.AlmostEq(events.WaveClearance, 5.0);
		Assert.SequenceEq(events.Pool.Select(d => d.Id.ToString()).Order(), new[] { "elites", "meteors", "statues" });
		Assert.NoErrors(EventDirector.ValidateDefs(events), "event controllers");
		Assert.NoErrors(BossBrain.ValidateDef(stage.Boss), "boss patterns");
		Assert.NoErrors(BossDirector.ValidateDef(stage.Boss), "boss director");
		for (int seed = 1; seed <= 10; seed++)
		{
			Assert.Eq(events.RollTimes(stage.Duration, stage.Waves!.Times(), new RunRng(seed)).Length, 3, $"seed {seed}");
		}

		Assert.Eq(stage.Boss!.DisplayName, "Orc Warlord");
	}

	public void TestEventLimitsAndRewardsMatchTheSpec()
	{
		foreach (var (id, limit, gold, stone, tickets) in new[] { ("statues", 25f, 150, 2, 1), ("meteors", 20f, 100, 3, 1), ("elites", 25f, 200, 2, 1) })
		{
			EventDef def = GD.Load<EventDef>($"res://port/data/events/{id}.tres");
			Assert.NoErrors(def.Validate(), id);
			Assert.Eq(def.Id.ToString(), id);
			Assert.AlmostEq(def.TimeLimit, limit, 0.001, id);
			Assert.SequenceEq(new[] { def.Reward!.Gold, def.Reward.Stone, def.Reward.Tickets }, new[] { gold, stone, tickets }, id);
		}
	}

	public void TestEventControllersCarryTheSpecNumbers()
	{
		var statues = GD.Load<PackedScene>("res://port/src/Events/statue_event.tscn").Instantiate<StatueEvent>();
		Assert.Eq(statues.StatueCount, 4);
		Assert.AlmostEq(statues.StatueStats!.MaxHealth, 60.0);
		Assert.AlmostEq(statues.MinDistance, 250.0);
		statues.Free();
		var meteors = GD.Load<PackedScene>("res://port/src/Events/meteor_event.tscn").Instantiate<MeteorEvent>();
		Assert.SequenceEq(new[] { meteors.Interval, meteors.Warning, meteors.Radius, meteors.Damage, meteors.LandingSpread }, new[] { 1.2f, 0.9f, 60f, 15f, 120f });
		Assert.Eq(meteors.MaxHits, 3);
		meteors.Free();
		var elites = GD.Load<PackedScene>("res://port/src/Events/elite_event.tscn").Instantiate<EliteEvent>();
		Assert.Eq(elites.Count, 3);
		Assert.Eq(elites.EliteEnemy!.Id.ToString(), "elite_orc");
		elites.Free();
	}

	public void TestTheOrcWarlordMatchesTheSpec()
	{
		BossDef boss = GD.Load<BossDef>("res://port/data/bosses/orc_warlord.tres");
		Assert.NoErrors(boss.Validate(), "orc_warlord");
		Assert.AlmostEq(boss.Stats!.MaxHealth, 2500.0);
		Assert.AlmostEq(boss.Stats.MoveSpeed, 60.0);
		Assert.AlmostEq(boss.VisualScale, 3.0);
		Assert.AlmostEq(boss.ContactDamage, 20.0);
		Assert.AlmostEq(boss.PatternGap, 2.0);
		Assert.Eq(boss.Order, BossOrder.Random);
		Assert.Eq(boss.Scene!.ResourcePath, "res://port/src/Actors/Boss/boss.tscn");
		Assert.True(boss.Tint.V < 0.7f, "dark tint");
		foreach (BossPatternEntry entry in boss.Patterns)
		{
			Assert.AlmostEq(entry.Weight, 1.0);
			Assert.True(entry.Pattern!.OwnedTags.Contains(GameplayTags.StateCasting), entry.Pattern.Tag);
		}

		Assert.SequenceEq(
			boss.Patterns.Select(e => e.Pattern!.Tag).Order(),
			new[] { "ability.boss.burst", "ability.boss.charge", "ability.boss.slam", "ability.boss.summon" });
	}

	public void TestTheBossPatternsCarryTheSpecNumbers()
	{
		var charge = GD.Load<ChargeDef>(ChargeTest.ChargePath);
		Assert.SequenceEq(new[] { charge.Warning, charge.Speed, charge.Distance, charge.Damage }, new[] { 0.7f, 520f, 360f, 25f });
		var burst = GD.Load<RadialBurstDef>(RadialBurstTest.BurstPath);
		Assert.Eq(burst.Count, 12);
		Assert.AlmostEq(burst.Damage, 10.0);
		Assert.AlmostEq(burst.Projectile!.Speed, 220.0);
		Assert.AlmostEq(burst.Projectile.Lifetime, 2.0);
		Assert.Eq(burst.Projectile.Hits, ProjectileTeam.Allies);
		var summon = GD.Load<SummonDef>(SummonTest.SummonPath);
		Assert.Eq(summon.Count, 4);
		Assert.AlmostEq(summon.Radius, 80.0);
		Assert.Eq(summon.Enemy!.Id.ToString(), "orc_swarmer");
		var slam = GD.Load<GroundSlamDef>(GroundSlamTest.SlamPath);
		Assert.SequenceEq(new[] { slam.Warning, slam.Radius, slam.Damage }, new[] { 1f, 90f, 30f });
	}
}
