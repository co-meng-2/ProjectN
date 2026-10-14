using System;

namespace VampireSurvivor.Core;

public enum CurrencyKind
{
	Gold,
	Stone,
	Ticket,
}

/// <summary>
/// Per-run balances: gold, stone and promotion tickets (paid by stage events; one ticket pays any shop action).
/// Gem is persistent meta currency and lives elsewhere.
/// </summary>
public sealed class Wallet
{
	public event Action<int, int, int>? Changed;

	public int Gold { get; private set; }

	public int Stone { get; private set; }

	public int Tickets { get; private set; }

	public int Balance(CurrencyKind kind) => kind switch
	{
		CurrencyKind.Gold => Gold,
		CurrencyKind.Stone => Stone,
		_ => Tickets,
	};

	public void Add(CurrencyKind kind, int amount)
	{
		if (amount <= 0)
		{
			return;
		}

		Apply(kind, Balance(kind) + amount);
	}

	public bool TrySpend(CurrencyKind kind, int amount)
	{
		if (amount < 0 || Balance(kind) < amount)
		{
			return false;
		}

		Apply(kind, Balance(kind) - amount);
		return true;
	}

	private void Apply(CurrencyKind kind, int value)
	{
		switch (kind)
		{
			case CurrencyKind.Gold:
				Gold = value;
				break;
			case CurrencyKind.Stone:
				Stone = value;
				break;
			default:
				Tickets = value;
				break;
		}

		Changed?.Invoke(Gold, Stone, Tickets);
	}
}
