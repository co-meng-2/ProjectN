using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class GradeTableTest : TestCase
{
	public void TestFactoryTableValidates()
	{
		Assert.NoErrors(DefFactory.GradeTable().Validate(), "table");
	}

	public void TestPromotePriceFollowsTheLadder()
	{
		GradeTable table = DefFactory.GradeTable();
		Assert.Eq(table.PromotePrice(Grade.None), new Price(CurrencyKind.Gold, 100));
		Assert.Eq(table.PromotePrice(Grade.Common)!.Value.Amount, 150);
		Assert.Eq(table.PromotePrice(Grade.Unique)!.Value.Amount, 600);
		Assert.Eq(table.PromotePrice(Grade.Legendary), new Price(CurrencyKind.Stone, 10));
		Assert.Null(table.PromotePrice(Grade.Revealed), "revealed is the top");
	}

	public void TestRerollPriceIsStoneExceptOnEmptyAndRevealed()
	{
		GradeTable table = DefFactory.GradeTable();
		Assert.Null(table.RerollPrice(Grade.None));
		Assert.Null(table.RerollPrice(Grade.Revealed));
		Assert.Eq(table.RerollPrice(Grade.Epic), new Price(CurrencyKind.Stone, 2));
	}

	public void TestValidateReportsWrongSizeAndNegativeCostsWithoutCrashing()
	{
		var table = new GradeTable();
		Assert.Eq(table.Validate().Count, 1, "empty promote_gold");
		Assert.Null(table.PromotePrice(Grade.Rare), "a short table must not crash");
		table.PromoteGold = [100, 150, -1, 400, 600];
		table.RevealStone = -5;
		Assert.Eq(table.Validate().Count, 2);
	}

	public void TestPriceIsAffordableComparesTheRightCurrency()
	{
		var wallet = new Wallet();
		wallet.Add(CurrencyKind.Gold, 100);
		Assert.True(new Price(CurrencyKind.Gold, 100).IsAffordable(wallet));
		Assert.False(new Price(CurrencyKind.Gold, 101).IsAffordable(wallet));
		Assert.False(new Price(CurrencyKind.Stone, 1).IsAffordable(wallet));
	}
}
