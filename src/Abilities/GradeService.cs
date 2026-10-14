using System;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Abilities;

/// <summary>
/// The shop rules: promote (None -> Common buys; Common..Unique re-roll from the next grade's pool; Legendary -> Revealed
/// keeps the ability) and reroll (same grade, a different ability preferred). Pure logic over the wallet, the run's rng,
/// the loadout, the pool and the grade table. Every action is atomic: check applicability, check the price, roll, spend,
/// then write the slot; a refused action changes nothing. While the wallet holds a promotion ticket, every applicable
/// action costs exactly one ticket.
/// </summary>
public sealed class GradeService(Loadout loadout, AbilityPool pool, GradeTable table, Wallet wallet, RunRng rng)
{
	/// <summary>Raised after every attempt, successful or not, so a view can react (flash a refused slot).</summary>
	public event Action<int, GradeResult>? Resolved;

	/// <summary>Price of the next promotion of slot <paramref name="index"/>; null when none is possible.</summary>
	public Price? PromoteCost(int index)
	{
		AbilitySlot? slot = loadout.Slot(index);
		return slot is null ? null : TicketFirst(table.PromotePrice(slot.Grade));
	}

	/// <summary>Price of rerolling slot <paramref name="index"/>; null when the slot is empty or revealed.</summary>
	public Price? RerollCost(int index)
	{
		AbilitySlot? slot = loadout.Slot(index);
		return slot is null || slot.IsEmpty ? null : TicketFirst(table.RerollPrice(slot.Grade));
	}

	public GradeResult Promote(int index) => Resolve(index, TryPromote(index));

	public GradeResult Reroll(int index) => Resolve(index, TryReroll(index));

	private GradeResult TryPromote(int index)
	{
		AbilitySlot? slot = loadout.Slot(index);
		Price? price = PromoteCost(index);
		if (slot is null || price is null)
		{
			return GradeResult.Failed(GradeOutcome.NotApplicable);
		}

		if (!price.Value.IsAffordable(wallet))
		{
			return GradeResult.Failed(GradeOutcome.NotEnough);
		}

		Grade target = slot.Grade.Next();

		// The revealed form is the same ability enhanced: deterministic, never rolled.
		AbilityDef? def = target == Grade.Revealed ? slot.Def : pool.Roll(target, rng);
		return def is null ? GradeResult.Failed(GradeOutcome.EmptyPool) : Commit(index, price.Value, def, target);
	}

	private GradeResult TryReroll(int index)
	{
		AbilitySlot? slot = loadout.Slot(index);
		Price? price = RerollCost(index);
		if (slot is null || price is null)
		{
			return GradeResult.Failed(GradeOutcome.NotApplicable);
		}

		if (!price.Value.IsAffordable(wallet))
		{
			return GradeResult.Failed(GradeOutcome.NotEnough);
		}

		AbilityDef? def = pool.Roll(slot.Grade, rng, slot.Def);
		return def is null ? GradeResult.Failed(GradeOutcome.EmptyPool) : Commit(index, price.Value, def, slot.Grade);
	}

	private GradeResult Commit(int index, Price price, AbilityDef def, Grade grade)
	{
		if (!wallet.TrySpend(price.Kind, price.Amount))
		{
			return GradeResult.Failed(GradeOutcome.NotEnough);
		}

		loadout.SetSlot(index, def, grade);
		return GradeResult.Ok(def, grade);
	}

	private GradeResult Resolve(int index, GradeResult result)
	{
		Resolved?.Invoke(index, result);
		return result;
	}

	/// <summary>Applicability comes first (a null price stays null); then a held ticket replaces the table price.</summary>
	private Price? TicketFirst(Price? price) =>
		price is null || wallet.Tickets < 1 ? price : new Price(CurrencyKind.Ticket, 1);
}
