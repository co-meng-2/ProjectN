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

	public void TestNullAndInvalidInnateAbilitiesAreReported()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.InnateAbilities.Add(null!);
		profile.InnateAbilities.Add(new ProbeAbilityDef { Tag = string.Empty });
		Assert.Eq(profile.Validate().Count, 2);
	}
}
