using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class PlayerProfileTest : TestCase
{
	public void TestFactoryProfileIsValid()
	{
		Assert.NoErrors(DefFactory.Profile().Validate());
	}

	public void TestNullStatsAndVisualAreReported()
	{
		var profile = new PlayerProfile { Stats = null, Visual = null };
		Assert.Eq(profile.Validate().Count, 2);
	}

	public void TestSlotCountMustBePositive()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.SlotCount = 0;
		Assert.Eq(profile.Validate().Count, 1);
	}

	public void TestSlotCountAboveTheMaximumIsReported()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.SlotCount = PlayerProfile.MaxSlots + 1;
		Assert.Eq(profile.Validate().Count, 1);
		profile.SlotCount = PlayerProfile.MaxSlots;
		Assert.NoErrors(profile.Validate());
	}

	public void TestNullAndInvalidCatalogAbilitiesAreReported()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.Abilities.Add(null!);
		profile.Abilities.Add(new AbilityDef());
		Assert.Eq(profile.Validate().Count, 5, "one null + four from the empty def");
	}

	public void TestNullAndInvalidInnateAbilitiesAreReported()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.InnateAbilities.Add(null!);
		profile.InnateAbilities.Add(new ProbeAbilityDef { Tag = string.Empty });
		Assert.Eq(profile.Validate().Count, 2);
	}

	public void TestValidSkillBonusesHaveNoErrors()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.SkillBonuses.Add(new SkillBonus(DefFactory.SkillNode("a").Passive!, 2));
		Assert.NoErrors(profile.Validate(), "one bonus at the curve's last level");
	}

	public void TestANullOrPassivelessSkillBonusIsAnError()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.SkillBonuses.Add(null!);
		profile.SkillBonuses.Add(new SkillBonus(null!, 1));
		Assert.Eq(profile.Validate().Count, 2);
	}

	public void TestASkillBonusLevelOutsideItsCurveIsAnError()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.SkillBonuses.Add(new SkillBonus(DefFactory.SkillNode("a").Passive!, 0));
		profile.SkillBonuses.Add(new SkillBonus(DefFactory.SkillNode("b").Passive!, 3));
		Assert.Eq(profile.Validate().Count, 2, "below 1 and above the two-point curve");
	}
}
