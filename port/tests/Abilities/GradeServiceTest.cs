using System.Collections.Generic;
using VampireSurvivor.Abilities;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Abilities;

public class GradeServiceTest : TestCase
{
	private Loadout _loadout = null!;
	private Wallet _wallet = null!;

	private static AbilityDef Def(string id, Grade minGrade = Grade.Common) =>
		DefFactory.Ability(id, DefFactory.Vitality(), minGrade: minGrade);

	private static AbilityDef[] Single() => [Def("only")];

	private static AbilityDef[] Pair() => [Def("first"), Def("second")];

	private GradeService Build(AbilityDef[] defs, int gold, int stone, long seed = 3)
	{
		_loadout = new Loadout(2);
		_wallet = new Wallet();
		_wallet.Add(CurrencyKind.Gold, gold);
		_wallet.Add(CurrencyKind.Stone, stone);
		return new GradeService(_loadout, new AbilityPool(defs), DefFactory.GradeTable(), _wallet, new RunRng(seed));
	}

	public void TestBuyingAnEmptySlotCosts100GoldAndRollsACommon()
	{
		GradeService service = Build(Single(), 250, 0);
		GradeResult result = service.Promote(0);
		Assert.Eq(result.Outcome, GradeOutcome.Ok);
		Assert.Eq(result.Grade, Grade.Common);
		Assert.Eq(result.Def!.Id.ToString(), "only");
		Assert.Eq(_loadout.Slot(0)!.Def!.Id.ToString(), "only");
		Assert.Eq(_loadout.Slot(0)!.Grade, Grade.Common);
		Assert.Eq(_wallet.Gold, 150);
		Assert.True(_loadout.Slot(1)!.IsEmpty, "other slot untouched");
	}

	public void TestPromotionClimbsWithRisingGoldCosts()
	{
		GradeService service = Build(Single(), 1500, 0);
		foreach (Grade expected in new[] { Grade.Common, Grade.Rare, Grade.Epic, Grade.Unique, Grade.Legendary })
		{
			Assert.Eq(service.Promote(0).Outcome, GradeOutcome.Ok);
			Assert.Eq(_loadout.Slot(0)!.Grade, expected);
		}

		Assert.Eq(_wallet.Gold, 0, "100 + 150 + 250 + 400 + 600");
	}

	public void TestPromoteCostIsThePriceOfTheNextStep()
	{
		AbilityDef[] defs = Single();
		GradeService service = Build(defs, 0, 0);
		Assert.Eq(service.PromoteCost(0)!.Value.Amount, 100);
		_loadout.SetSlot(0, defs[0], Grade.Epic);
		Assert.Eq(service.PromoteCost(0), new Price(CurrencyKind.Gold, 400));
		_loadout.SetSlot(0, defs[0], Grade.Legendary);
		Assert.Eq(service.PromoteCost(0)!.Value.Kind, CurrencyKind.Stone);
		Assert.Eq(service.RerollCost(0)!.Value.Amount, 2);
	}

	public void TestPromoteWithoutEnoughGoldChangesNothing()
	{
		GradeService service = Build(Single(), 99, 5);
		var changes = new List<int>();
		_loadout.SlotChanged += (index, _) => changes.Add(index);
		GradeResult result = service.Promote(0);
		Assert.Eq(result.Outcome, GradeOutcome.NotEnough);
		Assert.Null(result.Def);
		Assert.True(_loadout.Slot(0)!.IsEmpty);
		Assert.Eq(_wallet.Gold, 99);
		Assert.Eq(_wallet.Stone, 5);
		Assert.Eq(changes.Count, 0);
	}

	public void TestPromoteIntoAnEmptyPoolSpendsNothing()
	{
		GradeService service = Build([Def("rare_only", Grade.Rare)], 500, 0);
		Assert.Eq(service.Promote(0).Outcome, GradeOutcome.EmptyPool, "nothing can be Common");
		Assert.Eq(_wallet.Gold, 500);
		Assert.True(_loadout.Slot(0)!.IsEmpty);
	}

	public void TestRevealKeepsTheSameAbilityAndCostsStone()
	{
		AbilityDef[] defs = Pair();
		GradeService service = Build(defs, 0, 25);
		_loadout.SetSlot(0, defs[1], Grade.Legendary);
		for (int i = 0; i < 10; i++)
		{
			GradeOutcome expected = i == 0 ? GradeOutcome.Ok : GradeOutcome.NotApplicable;
			Assert.Eq(service.Promote(0).Outcome, expected, "revealed is the top");
		}

		Assert.True(_loadout.Slot(0)!.Def == defs[1], "same ability, never rolled");
		Assert.Eq(_loadout.Slot(0)!.Grade, Grade.Revealed);
		Assert.Eq(_wallet.Stone, 15, "charged exactly once");
		Assert.Null(service.PromoteCost(0));
	}

	public void TestRevealWithoutEnoughStoneIsRefused()
	{
		AbilityDef[] defs = Single();
		GradeService service = Build(defs, 1000, 9);
		_loadout.SetSlot(0, defs[0], Grade.Legendary);
		Assert.Eq(service.Promote(0).Outcome, GradeOutcome.NotEnough);
		Assert.Eq(_loadout.Slot(0)!.Grade, Grade.Legendary);
		Assert.Eq(_wallet.Gold, 1000, "gold is not an alternative currency");
	}

	public void TestPromotionRerollsFromTheTargetGradePool()
	{
		AbilityDef[] defs = Pair();
		GradeService service = Build(defs, 100000, 0);
		var seen = new HashSet<string>();
		for (int i = 0; i < 16; i++)
		{
			_loadout.SetSlot(0, defs[0], Grade.Common);
			Assert.Eq(service.Promote(0).Outcome, GradeOutcome.Ok);
			Assert.Eq(_loadout.Slot(0)!.Grade, Grade.Rare);
			seen.Add(_loadout.Slot(0)!.Def!.Id.ToString());
		}

		Assert.True(seen.Contains("second"), "promotion may change the ability (16 seeded tries)");
	}

	public void TestRerollCostsStoneAndPrefersADifferentAbility()
	{
		AbilityDef[] defs = Pair();
		GradeService service = Build(defs, 0, 10);
		_loadout.SetSlot(0, defs[0], Grade.Epic);
		AbilityDef[] expected = [defs[1], defs[0], defs[1]];
		for (int i = 0; i < 3; i++)
		{
			Assert.Eq(service.Reroll(0).Outcome, GradeOutcome.Ok);
			Assert.Eq(_loadout.Slot(0)!.Grade, Grade.Epic, "same grade");
			Assert.True(_loadout.Slot(0)!.Def == expected[i], "alternates between the two");
		}

		Assert.Eq(_wallet.Stone, 4);
	}

	public void TestRerollOnASingleAbilityPoolKeepsTheAbilityButStillCharges()
	{
		AbilityDef[] defs = Single();
		GradeService service = Build(defs, 0, 4);
		_loadout.SetSlot(0, defs[0], Grade.Rare);
		var changes = new List<int>();
		_loadout.SlotChanged += (index, _) => changes.Add(index);
		Assert.Eq(service.Reroll(0).Outcome, GradeOutcome.Ok);
		Assert.True(_loadout.Slot(0)!.Def == defs[0]);
		Assert.Eq(_wallet.Stone, 2);
		Assert.SequenceEq(changes, new[] { 0 }, "re-emitted so the installer reinstalls a fresh instance");
	}

	public void TestRerollIsRefusedOnEmptyAndRevealedSlotsAndWithoutStone()
	{
		AbilityDef[] defs = Single();
		GradeService service = Build(defs, 0, 1);
		Assert.Eq(service.Reroll(0).Outcome, GradeOutcome.NotApplicable, "empty slot");
		Assert.Null(service.RerollCost(0));
		_loadout.SetSlot(0, defs[0], Grade.Revealed);
		Assert.Eq(service.Reroll(0).Outcome, GradeOutcome.NotApplicable, "revealed");
		_loadout.SetSlot(0, defs[0], Grade.Rare);
		Assert.Eq(service.Reroll(0).Outcome, GradeOutcome.NotEnough, "1 stone < 2");
		Assert.Eq(_wallet.Stone, 1);
		Assert.Eq(_loadout.Slot(0)!.Grade, Grade.Rare);
	}

	public void TestOutOfRangeSlotIsNotApplicable()
	{
		GradeService service = Build(Single(), 1000, 100);
		Assert.Eq(service.Promote(7).Outcome, GradeOutcome.NotApplicable);
		Assert.Eq(service.Reroll(-1).Outcome, GradeOutcome.NotApplicable);
		Assert.Null(service.PromoteCost(7));
		Assert.Null(service.RerollCost(-1));
		Assert.Eq(_wallet.Gold, 1000);
	}

	public void TestResolvedIsRaisedForEveryAttempt()
	{
		GradeService service = Build(Single(), 100, 0);
		var outcomes = new List<(int, GradeOutcome)>();
		service.Resolved += (index, result) => outcomes.Add((index, result.Outcome));
		service.Promote(0);
		service.Promote(0);
		service.Reroll(1);
		Assert.SequenceEq(
			outcomes,
			new[] { (0, GradeOutcome.Ok), (0, GradeOutcome.NotEnough), (1, GradeOutcome.NotApplicable) });
	}

	public void TestATicketPaysForAPurchaseBeforeAnyGold()
	{
		GradeService service = Build(Single(), 500, 0);
		_wallet.Add(CurrencyKind.Ticket, 1);
		Assert.Eq(service.PromoteCost(0), new Price(CurrencyKind.Ticket, 1));
		Assert.Eq(service.Promote(0).Outcome, GradeOutcome.Ok);
		Assert.Eq(_loadout.Slot(0)!.Grade, Grade.Common);
		Assert.Eq(_wallet.Tickets, 0);
		Assert.Eq(_wallet.Gold, 500, "gold untouched while a ticket was held");
		Assert.Eq(service.PromoteCost(0), new Price(CurrencyKind.Gold, 150), "back to the table price");
	}

	public void TestOneTicketPaysAnyShopActionEvenWithoutCurrency()
	{
		AbilityDef[] defs = Pair();
		GradeService service = Build(defs, 0, 0);
		_wallet.Add(CurrencyKind.Ticket, 3);
		_loadout.SetSlot(0, defs[0], Grade.Legendary);
		Assert.Eq(service.Promote(0).Outcome, GradeOutcome.Ok, "reveal for a ticket");
		Assert.Eq(_loadout.Slot(0)!.Grade, Grade.Revealed);
		Assert.Eq(_wallet.Tickets, 2);
		_loadout.SetSlot(1, defs[0], Grade.Epic);
		Assert.Eq(service.Reroll(1).Outcome, GradeOutcome.Ok, "reroll for a ticket");
		Assert.True(_loadout.Slot(1)!.Def == defs[1]);
		Assert.Eq(_wallet.Tickets, 1);
		Assert.Eq(service.Promote(1).Outcome, GradeOutcome.Ok, "gold promotion for a ticket");
		Assert.Eq(_loadout.Slot(1)!.Grade, Grade.Unique);
		Assert.Eq(_wallet.Tickets, 0);
		Assert.Eq(service.Promote(1).Outcome, GradeOutcome.NotEnough, "no ticket and no gold left");
	}

	public void TestTicketsAreNotSpentOnRefusedActions()
	{
		AbilityDef[] defs = [Def("rare_only", Grade.Rare)];
		GradeService service = Build(defs, 0, 0);
		_wallet.Add(CurrencyKind.Ticket, 1);
		Assert.Eq(service.Promote(0).Outcome, GradeOutcome.EmptyPool, "nothing can be Common");
		Assert.Null(service.RerollCost(0), "an empty slot cannot be rerolled, ticket or not");
		Assert.Eq(service.Reroll(0).Outcome, GradeOutcome.NotApplicable);
		_loadout.SetSlot(1, defs[0], Grade.Revealed);
		Assert.Null(service.PromoteCost(1));
		Assert.Eq(service.Promote(1).Outcome, GradeOutcome.NotApplicable);
		Assert.Eq(_wallet.Tickets, 1, "never spent on a refused action");
	}
}
