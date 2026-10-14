using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class BossDefTest : TestCase
{
	private static BossDef Valid() => DefFactory.Boss(null, new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe });

	public void TestFactoryBossIsValid()
	{
		Assert.NoErrors(Valid().Validate(), "boss");
	}

	public void TestEmptyBossDefReportsEveryMissingPart()
	{
		var errors = new BossDef().Validate();
		Assert.Eq(errors.Count, 7, "id, name, scene, visual, stats, gap, patterns: " + string.Join(", ", errors));
	}

	public void TestNumbersMustBePositive()
	{
		BossDef def = Valid();
		def.PatternGap = 0f;
		Assert.Eq(def.Validate().Count, 1, "pattern_gap");
		def = Valid();
		def.ContactInterval = 0f;
		def.ContactDamage = -1f;
		Assert.Eq(def.Validate().Count, 1, "one combined actor-number error");
	}

	public void TestNestedStatsAndVisualErrorsAreReported()
	{
		BossDef def = Valid();
		def.Stats!.MaxHealth = 0f;
		def.Visual = new VisualDef();
		Assert.Eq(def.Validate().Count, 2);
	}

	public void TestAPatternEntryNeedsAPatternAndAPositiveWeight()
	{
		var entry = new BossPatternEntry();
		Assert.Eq(entry.Validate().Count, 1, "no pattern");
		entry.Pattern = new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe };
		Assert.NoErrors(entry.Validate(), "probe");
		entry.Weight = 0f;
		Assert.Eq(entry.Validate().Count, 1, "weight 0");
		entry.Weight = 1f;
		entry.Pattern = new ProbeAbilityDef { Tag = string.Empty };
		Assert.Eq(entry.Validate().Count, 1, "the pattern's own errors");
	}

	public void TestBossReportsNullAndInvalidPatterns()
	{
		BossDef def = Valid();
		def.Patterns.Add(null!);
		def.Patterns.Add(new BossPatternEntry());
		Assert.Eq(def.Validate().Count, 2);
	}
}
