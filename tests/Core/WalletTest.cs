using System.Collections.Generic;
using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Core;

public class WalletTest : TestCase
{
	public void TestAddIncreasesBalanceAndEmitsChanged()
	{
		var wallet = new Wallet();
		var emitted = new List<(int, int, int)>();
		wallet.Changed += (gold, stone, tickets) => emitted.Add((gold, stone, tickets));
		wallet.Add(CurrencyKind.Gold, 5);
		wallet.Add(CurrencyKind.Stone, 2);
		Assert.Eq(wallet.Gold, 5);
		Assert.Eq(wallet.Stone, 2);
		Assert.SequenceEq(emitted, new[] { (5, 0, 0), (5, 2, 0) });
	}

	public void TestAddIgnoresNonPositiveAmounts()
	{
		var wallet = new Wallet();
		wallet.Add(CurrencyKind.Gold, 0);
		wallet.Add(CurrencyKind.Gold, -3);
		Assert.Eq(wallet.Gold, 0);
	}

	public void TestTrySpendSucceedsWhenAffordable()
	{
		var wallet = new Wallet();
		wallet.Add(CurrencyKind.Gold, 10);
		Assert.True(wallet.TrySpend(CurrencyKind.Gold, 4));
		Assert.Eq(wallet.Balance(CurrencyKind.Gold), 6);
	}

	public void TestTrySpendFailsWithoutChangingBalance()
	{
		var wallet = new Wallet();
		wallet.Add(CurrencyKind.Stone, 1);
		Assert.False(wallet.TrySpend(CurrencyKind.Stone, 2));
		Assert.False(wallet.TrySpend(CurrencyKind.Stone, -1));
		Assert.Eq(wallet.Stone, 1);
	}

	public void TestTicketsAreAThirdBalance()
	{
		var wallet = new Wallet();
		var emitted = new List<(int, int, int)>();
		wallet.Changed += (gold, stone, tickets) => emitted.Add((gold, stone, tickets));
		wallet.Add(CurrencyKind.Ticket, 2);
		Assert.Eq(wallet.Tickets, 2);
		Assert.Eq(wallet.Balance(CurrencyKind.Ticket), 2);
		Assert.Eq(wallet.Gold, 0, "tickets are not gold");
		Assert.True(wallet.TrySpend(CurrencyKind.Ticket, 1));
		Assert.False(wallet.TrySpend(CurrencyKind.Ticket, 2), "only one left");
		Assert.Eq(wallet.Tickets, 1);
		Assert.SequenceEq(emitted, new[] { (0, 0, 2), (0, 0, 1) });
	}

	public void TestPriceIsAffordableComparesTheMatchingBalance()
	{
		var wallet = new Wallet();
		wallet.Add(CurrencyKind.Gold, 5);
		Assert.True(new Price(CurrencyKind.Gold, 5).IsAffordable(wallet));
		Assert.False(new Price(CurrencyKind.Gold, 6).IsAffordable(wallet));
		Assert.False(new Price(CurrencyKind.Stone, 1).IsAffordable(wallet));
	}
}
