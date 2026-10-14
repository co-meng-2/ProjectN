using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class SkillNodeDefTest : TestCase
{
	public void TestAValidNodeHasNoErrors() => Assert.NoErrors(DefFactory.SkillNode("a").Validate(), "passive node");

	public void TestASlotOnlyNodeIsValid()
	{
		SkillNodeDef node = DefFactory.SkillNode("slot", null, 1, [50]);
		node.Passive = null;
		node.SlotBonus = 1;
		Assert.NoErrors(node.Validate(), "slot node");
	}

	public void TestEmptyIdOrNameIsAnError()
	{
		SkillNodeDef node = DefFactory.SkillNode("a");
		node.Id = string.Empty;
		Assert.Eq(node.Validate().Count, 1, "id");
		SkillNodeDef unnamed = DefFactory.SkillNode("a");
		unnamed.DisplayName = string.Empty;
		Assert.Eq(unnamed.Validate().Count, 1, "name");
	}

	public void TestMaxLevelBelowOneIsAnError()
	{
		SkillNodeDef node = DefFactory.SkillNode("a", null, 0, []);
		node.Passive = null;
		node.SlotBonus = 1;
		Assert.Eq(node.Validate().Count, 1);
	}

	public void TestCostsMustMatchMaxLevel() => Assert.Eq(DefFactory.SkillNode("a", null, 3, [10, 20]).Validate().Count, 1);

	public void TestANonPositiveCostIsAnError() => Assert.Eq(DefFactory.SkillNode("a", null, 2, [10, 0]).Validate().Count, 1);

	public void TestANodeMustDoSomething()
	{
		SkillNodeDef node = DefFactory.SkillNode("a");
		node.Passive = null;
		Assert.Eq(node.Validate().Count, 1);
	}

	public void TestANegativeSlotBonusIsAnError()
	{
		SkillNodeDef node = DefFactory.SkillNode("a");
		node.SlotBonus = -1;
		Assert.Eq(node.Validate().Count, 1);
	}

	public void TestANodeCannotRequireItself() => Assert.Eq(DefFactory.SkillNode("a", ["a"]).Validate().Count, 1);

	public void TestPassiveProblemsAreReportedThroughThePassiveCheck()
	{
		SkillNodeDef zero = DefFactory.SkillNode("a");
		zero.Passive!.ByLevel = DefFactory.CurveOf(0f, 0f);
		Assert.Eq(zero.Validate().Count, 1, "every level 0");
		SkillNodeDef sixPoints = DefFactory.SkillNode("b");
		sixPoints.Passive!.ByLevel = DefFactory.CurveOf(1f, 2f, 3f, 4f, 5f, 6f);
		Assert.Eq(sixPoints.Validate().Count, 1, "the curve has one point per purchasable level, not six");
	}
}
