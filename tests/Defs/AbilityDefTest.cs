using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class AbilityDefTest : TestCase
{
	private static ProbeAbilityDef Probe() => new() { Tag = GameplayTags.AbilityProbe };

	public void TestValidPassiveActiveAndHybridDefsReportNoErrors()
	{
		Assert.NoErrors(DefFactory.VitalityAbility().Validate(), "passive");
		Assert.NoErrors(DefFactory.Ability("probe", active: Probe()).Validate(), "active");
		Assert.NoErrors(DefFactory.Ability("hybrid", DefFactory.Vitality(), Probe()).Validate(), "hybrid");
	}

	public void TestDefNeedsIdNameIconAndSomeContent()
	{
		Assert.Eq(new AbilityDef().Validate().Count, 4, string.Join(", ", new AbilityDef().Validate()));
	}

	public void TestMinGradeMustBeRollable()
	{
		AbilityDef def = DefFactory.VitalityAbility();
		def.MinGrade = Grade.None;
		Assert.Eq(def.Validate().Count, 1);
		def.MinGrade = Grade.Revealed;
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestNestedPassiveAndActiveErrorsPropagate()
	{
		AbilityDef def = DefFactory.VitalityAbility();
		def.Passive!.ByLevel = null;
		Assert.Eq(def.Validate().Count, 1, "passive");
		def = DefFactory.Ability("bad_active", active: new ProbeAbilityDef { Tag = string.Empty });
		Assert.Eq(def.Validate().Count, 1, "active");
	}

	public void TestIconForUsesTheRevealedIconOnlyWhenRevealedAndPresent()
	{
		AbilityDef def = DefFactory.VitalityAbility();
		Assert.True(def.IconFor(Grade.Revealed) == def.Icon, "no revealed icon: fall back");
		def.RevealedIcon = DefFactory.Texture(16, 16);
		Assert.True(def.IconFor(Grade.Legendary) == def.Icon);
		Assert.True(def.IconFor(Grade.Revealed) == def.RevealedIcon);
	}
}
