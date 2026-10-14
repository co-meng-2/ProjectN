using System.Collections.Generic;
using VampireSurvivor.Meta;
using VampireSurvivor.Meta.SkillTree;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Meta;

/// <summary>a: 2 levels, costs 10/20. b: 1 level, cost 5, requires a.</summary>
public class SkillTreeProgressTest : TestCase
{
	private static (SkillTreeProgress Progress, SaveData Save) Setup(long gem, Dictionary<string, int>? levels = null)
	{
		var save = new SaveData { Gem = gem };
		foreach (KeyValuePair<string, int> entry in levels ?? [])
		{
			save.SetLevel(entry.Key, entry.Value);
		}

		var tree = DefFactory.Tree(DefFactory.SkillNode("a"), DefFactory.SkillNode("b", ["a"], 1, [5]));
		return (new SkillTreeProgress(tree, save), save);
	}

	public void TestANodeWithoutRequirementsIsAvailable() => Assert.Eq(Setup(0).Progress.Status(0), SkillStatus.Available);

	public void TestANodeIsLockedUntilEveryRequirementHasALevel()
	{
		Assert.Eq(Setup(0).Progress.Status(1), SkillStatus.Locked);
		Assert.Eq(Setup(0, new() { ["a"] = 1 }).Progress.Status(1), SkillStatus.Available);
	}

	public void TestANodeAtMaxLevelIsMaxed()
	{
		SkillTreeProgress progress = Setup(0, new() { ["a"] = 2 }).Progress;
		Assert.Eq(progress.Status(0), SkillStatus.Maxed);
		Assert.Eq(progress.NextCost(0), -1);
	}

	public void TestNextCostFollowsTheCurrentLevel()
	{
		Assert.Eq(Setup(0).Progress.NextCost(0), 10);
		Assert.Eq(Setup(0, new() { ["a"] = 1 }).Progress.NextCost(0), 20);
	}

	public void TestBuyDeductsGemAndRaisesTheLevel()
	{
		var (progress, save) = Setup(25);
		Assert.True(progress.Buy(0));
		Assert.Eq(save.Gem, 15L);
		Assert.Eq(save.LevelOf("a"), 1);
		Assert.False(progress.Buy(0), "level 2 costs 20 but only 15 is left");
		Assert.Eq(save.Gem, 15L);
		Assert.Eq(save.LevelOf("a"), 1);
	}

	public void TestBuyFailsWithoutEnoughGemAndChangesNothing()
	{
		var (progress, save) = Setup(9);
		Assert.False(progress.CanBuy(0));
		Assert.False(progress.Buy(0));
		Assert.Eq(save.Gem, 9L);
		Assert.Eq(save.LevelOf("a"), 0);
	}

	public void TestBuyFailsOnALockedNodeEvenWithPlentyOfGem()
	{
		var (progress, save) = Setup(1000);
		Assert.False(progress.Buy(1));
		Assert.Eq(save.Gem, 1000L);
		Assert.Eq(save.LevelOf("b"), 0);
	}

	public void TestBuyFailsOnAMaxedNode()
	{
		var (progress, save) = Setup(1000, new() { ["a"] = 2 });
		Assert.False(progress.Buy(0));
		Assert.Eq(save.Gem, 1000L);
		Assert.Eq(save.LevelOf("a"), 2);
	}

	public void TestBuyingTheRequirementOpensTheChild()
	{
		var (progress, save) = Setup(30);
		progress.Buy(0);
		Assert.Eq(progress.Status(1), SkillStatus.Available);
		Assert.True(progress.Buy(1));
		Assert.Eq(save.Gem, 15L);
	}

	public void TestAnOutOfRangeIndexIsLockedAndCannotBeBought()
	{
		SkillTreeProgress progress = Setup(1000).Progress;
		Assert.Eq(progress.Status(5), SkillStatus.Locked);
		Assert.Eq(progress.Status(-1), SkillStatus.Locked);
		Assert.Eq(progress.NextCost(5), -1);
		Assert.False(progress.Buy(5));
	}

	public void TestASavedLevelAboveMaxReadsAsMaxed() => Assert.Eq(Setup(0, new() { ["a"] = 9 }).Progress.Status(0), SkillStatus.Maxed);
}
