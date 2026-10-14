namespace VampireSurvivor.Core;

/// <summary>What one shop action costs: an amount of a single currency.</summary>
public readonly record struct Price(CurrencyKind Kind, int Amount)
{
	public bool IsAffordable(Wallet wallet) => wallet.Balance(Kind) >= Amount;
}
