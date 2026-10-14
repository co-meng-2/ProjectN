using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class DropTableTest : TestCase
{
	private static DropEntry MakeEntry(CurrencyKind kind, float chance, int low, int high) => new()
	{
		Kind = kind,
		Chance = chance,
		MinAmount = low,
		MaxAmount = high,
		Icon = DefFactory.Texture(8, 8),
	};

	public void TestCertainEntryAlwaysDropsWithinRange()
	{
		var table = new DropTable();
		table.Entries.Add(MakeEntry(CurrencyKind.Gold, 1f, 2, 4));
		var rng = new RunRng(11);
		for (int i = 0; i < 20; i++)
		{
			var rolls = table.Roll(rng);
			Assert.Eq(rolls.Count, 1);
			Assert.Eq(rolls[0].Kind, CurrencyKind.Gold);
			Assert.True(rolls[0].Amount >= 2 && rolls[0].Amount <= 4);
		}
	}

	public void TestImpossibleEntryNeverDrops()
	{
		var table = new DropTable();
		table.Entries.Add(MakeEntry(CurrencyKind.Stone, 0f, 1, 1));
		Assert.Eq(table.Roll(new RunRng(1)).Count, 0);
	}

	public void TestValidateFlagsInvertedRangeAndBadChance()
	{
		var table = new DropTable();
		table.Entries.Add(MakeEntry(CurrencyKind.Gold, 1.5f, 5, 2));
		Assert.Eq(table.Validate().Count, 2);
	}

	public void TestNullEntryIsReportedAndSkippedWhenRolling()
	{
		var table = new DropTable();
		table.Entries.Add(null!);
		table.Entries.Add(MakeEntry(CurrencyKind.Gold, 1f, 1, 1));
		Assert.Eq(table.Validate().Count, 1);
		Assert.Eq(table.Roll(new RunRng(5)).Count, 1);
	}
}
