using System.Collections.Generic;
using VampireSurvivor.Defs;
using VampireSurvivor.Meta.SkillTree;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Meta;

public class ProfileBuilderTest : TestCase
{
	private static SkillTreeDef Tree()
	{
		SkillNodeDef slot = DefFactory.SkillNode("slot", ["a"], 2, [10, 20]);
		slot.Passive = null;
		slot.SlotBonus = 1;
		return DefFactory.Tree(DefFactory.SkillNode("a"), slot);
	}

	private static PlayerProfile Build(PlayerProfile profile, Dictionary<string, int> levels, SkillTreeDef? tree = null) =>
		ProfileBuilder.Build(profile, tree ?? Tree(), levels);

	public void TestWithoutPurchasesTheProfileEqualsTheBase()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.Abilities.Add(DefFactory.VitalityAbility());
		PlayerProfile built = Build(profile, []);
		Assert.True(built.Stats == profile.Stats);
		Assert.True(built.Visual == profile.Visual);
		Assert.Eq(built.SlotCount, profile.SlotCount);
		Assert.Eq(built.Abilities.Count, 1);
		Assert.Eq(built.InnateAbilities.Count, profile.InnateAbilities.Count);
		Assert.Eq(built.SkillBonuses.Count, 0);
	}

	public void TestTheBaseProfileIsNeverMutated()
	{
		PlayerProfile profile = DefFactory.Profile();
		int slotsBefore = profile.SlotCount;
		Build(profile, new() { ["a"] = 2, ["slot"] = 2 });
		Assert.Eq(profile.SlotCount, slotsBefore);
		Assert.Eq(profile.SkillBonuses.Count, 0);
	}

	public void TestEachBoughtPassiveNodeBecomesABonusAtItsLevel()
	{
		SkillTreeDef tree = Tree();
		PlayerProfile built = Build(DefFactory.Profile(), new() { ["a"] = 2 }, tree);
		Assert.Eq(built.SkillBonuses.Count, 1);
		Assert.True(built.SkillBonuses[0].Passive == tree.Nodes[0].Passive);
		Assert.Eq(built.SkillBonuses[0].Level, 2);
	}

	public void TestSlotBonusesAddUpPerLevel()
	{
		PlayerProfile profile = DefFactory.Profile();
		PlayerProfile built = Build(profile, new() { ["slot"] = 2 });
		Assert.Eq(built.SlotCount, profile.SlotCount + 2);
		Assert.Eq(built.SkillBonuses.Count, 0, "a slot-only node carries no passive");
	}

	public void TestSlotBonusesStopAtTheMaximumSlotCount()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.SlotCount = PlayerProfile.MaxSlots - 1;
		PlayerProfile built = Build(profile, new() { ["slot"] = 2 });
		Assert.Eq(built.SlotCount, PlayerProfile.MaxSlots, "each slot needs its own cooldown tag");
		Assert.NoErrors(built.Validate());
	}

	public void TestASavedLevelAboveMaxIsClamped() => Assert.Eq(Build(DefFactory.Profile(), new() { ["a"] = 99 }).SkillBonuses[0].Level, 2);

	public void TestAnIdNotInTheTreeIsIgnored() => Assert.Eq(Build(DefFactory.Profile(), new() { ["ghost"] = 3 }).SkillBonuses.Count, 0);

	public void TestABuiltProfileValidates() => Assert.NoErrors(Build(DefFactory.Profile(), new() { ["a"] = 1, ["slot"] = 1 }).Validate(), "built profile");
}
