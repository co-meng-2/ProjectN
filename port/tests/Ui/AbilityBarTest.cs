using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;
using VampireSurvivor.Ui;

namespace VampireSurvivor.Tests.Ui;

public class AbilityBarTest : TestCase
{
	private const string BarScene = "res://port/src/Ui/Hud/ability_bar.tscn";

	private sealed record Rig(AbilityBar Bar, Loadout Loadout, Wallet Wallet, GradeService Service, AbilityDef Def);

	private static InputEventMouseButton Click(bool ctrl, MouseButton button = MouseButton.Left, bool pressed = true) =>
		new() { ButtonIndex = button, Pressed = pressed, CtrlPressed = ctrl };

	private Rig MakeRig(int gold = 120, int stone = 5)
	{
		AbilityDef def = DefFactory.VitalityAbility();
		var loadout = new Loadout(3);
		var wallet = new Wallet();
		wallet.Add(CurrencyKind.Gold, gold);
		wallet.Add(CurrencyKind.Stone, stone);
		var service = new GradeService(loadout, new AbilityPool([def]), DefFactory.GradeTable(), wallet, new RunRng(2));
		AbilityBar bar = Adopt(GD.Load<PackedScene>(BarScene).Instantiate<AbilityBar>());
		bar.Bind(loadout, wallet, service);
		return new Rig(bar, loadout, wallet, service, def);
	}

	private static string CostText(AbilitySlotView view) => view.Cost.Text;

	public void TestCreatesOneViewPerSlotShowingTheNextCost()
	{
		Rig rig = MakeRig();
		Assert.Eq(rig.Bar.GetChildCount(), 3);
		Assert.Null(rig.Bar.SlotView(3));
		for (int i = 0; i < 3; i++)
		{
			Assert.Eq(rig.Bar.SlotView(i)!.SlotIndex, i);
			Assert.Eq(CostText(rig.Bar.SlotView(i)!), "100g");
			Assert.Null(rig.Bar.SlotView(i)!.Icon.Texture);
		}

		rig.Loadout.SetSlot(1, rig.Def, Grade.Legendary);
		Assert.Eq(CostText(rig.Bar.SlotView(1)!), "10s");
		Assert.True(rig.Bar.SlotView(1)!.Icon.Texture == rig.Def.Icon);
		rig.Loadout.SetSlot(1, rig.Def, Grade.Revealed);
		Assert.Eq(CostText(rig.Bar.SlotView(1)!), "MAX");
		Assert.Eq(CostText(rig.Bar.SlotView(0)!), "100g", "other slots untouched");
	}

	public void TestClickEmitsPromoteAndCtrlClickEmitsReroll()
	{
		Rig rig = MakeRig();
		var promotes = new List<int>();
		var rerolls = new List<int>();
		rig.Bar.PromoteRequested += promotes.Add;
		rig.Bar.RerollRequested += rerolls.Add;
		rig.Bar.SlotView(1)!._GuiInput(Click(false));
		rig.Bar.SlotView(2)!._GuiInput(Click(true));
		rig.Bar.SlotView(0)!._GuiInput(Click(false, MouseButton.Right));
		rig.Bar.SlotView(0)!._GuiInput(Click(true, MouseButton.Left, pressed: false));
		rig.Bar.SlotView(0)!._GuiInput(new InputEventKey());
		Assert.SequenceEq(promotes, new[] { 1 });
		Assert.SequenceEq(rerolls, new[] { 2 });
	}

	public void TestTheBarOnlyEmitsRequests()
	{
		Rig rig = MakeRig();
		rig.Bar.SlotView(0)!._GuiInput(Click(false));
		rig.Bar.SlotView(0)!._GuiInput(Click(true));
		Assert.Eq(rig.Wallet.Gold, 120, "no spending from the view");
		Assert.True(rig.Loadout.Slot(0)!.IsEmpty, "no slot change from the view");
	}

	public void TestUnaffordableSlotsAreDimmedAndFollowTheWallet()
	{
		Rig rig = MakeRig(120, 0);
		Assert.False(rig.Bar.SlotView(0)!.IsDimmed, "120 gold covers 100");
		rig.Wallet.TrySpend(CurrencyKind.Gold, 100);
		Assert.True(rig.Bar.SlotView(0)!.IsDimmed, "20 gold left");
		Assert.True(rig.Bar.SlotView(2)!.IsDimmed, "every empty slot");
		Assert.True(rig.Bar.SlotView(2)!.Cost.Modulate.A < 1f, "an empty slot is visibly dimmed");
		rig.Wallet.Add(CurrencyKind.Gold, 100);
		Assert.AlmostEq(rig.Bar.SlotView(2)!.Cost.Modulate.A, 1.0, 0.001, "affordable slot not dimmed");
		Assert.False(rig.Bar.SlotView(0)!.IsDimmed);
		rig.Loadout.SetSlot(0, rig.Def, Grade.Legendary);
		Assert.True(rig.Bar.SlotView(0)!.IsDimmed, "10 stone needed, 0 owned");
		rig.Loadout.SetSlot(0, rig.Def, Grade.Revealed);
		Assert.False(rig.Bar.SlotView(0)!.IsDimmed, "nothing to buy is not unaffordable");
		Assert.False(rig.Bar.SlotView(0)!.TooltipText.Contains("promote"), "no promote line when fully revealed");
		Assert.True(rig.Bar.SlotView(0)!.TooltipText.Contains("Fully revealed"));
	}

	public async Task TestFailedActionsFlashTheSlotAndSuccessfulOnesDoNot()
	{
		Rig rig = MakeRig(100, 0);
		Assert.Eq(rig.Service.Promote(0).Outcome, GradeOutcome.Ok);
		Assert.False(rig.Bar.SlotView(0)!.IsFlashing);
		Assert.Eq(rig.Service.Promote(1).Outcome, GradeOutcome.NotEnough);
		Assert.True(rig.Bar.SlotView(1)!.IsFlashing);
		Assert.False(rig.Bar.SlotView(0)!.IsFlashing, "only the refused slot");
		await WaitSeconds(0.4);
		Assert.False(rig.Bar.SlotView(1)!.IsFlashing, "the flash fades");
	}

	public void TestARevealedSlotUsesTheRevealedIconOrAShineTint()
	{
		Rig rig = MakeRig();
		TextureRect icon = rig.Bar.SlotView(0)!.Icon;
		rig.Loadout.SetSlot(0, rig.Def, Grade.Legendary);
		Assert.Eq(icon.Modulate, Colors.White, "not revealed: no tint");
		rig.Loadout.SetSlot(0, rig.Def, Grade.Revealed);
		Assert.True(icon.Texture == rig.Def.Icon);
		Assert.True(icon.Modulate != Colors.White, "tinted: no revealed icon");
		rig.Def.RevealedIcon = DefFactory.Texture(16, 16);
		rig.Loadout.SetSlot(0, rig.Def, Grade.Revealed);
		Assert.True(icon.Texture == rig.Def.RevealedIcon);
		Assert.Eq(icon.Modulate, Colors.White);
	}

	public async Task TestRebindingReplacesTheViewsWithoutDuplicateListeners()
	{
		Rig rig = MakeRig(100, 0);
		var second = new Loadout(2);
		var secondService = new GradeService(second, new AbilityPool([rig.Def]), DefFactory.GradeTable(), rig.Wallet, new RunRng(3));
		rig.Bar.Bind(second, rig.Wallet, secondService);
		await NextFrame();
		Assert.Eq(rig.Bar.GetChildCount(), 2);
		var promotes = new List<int>();
		rig.Bar.PromoteRequested += promotes.Add;
		rig.Bar.SlotView(0)!._GuiInput(Click(false));
		Assert.SequenceEq(promotes, new[] { 0 });

		// The old models still change, but the rebound views must not react to them.
		rig.Loadout.SetSlot(0, rig.Def, Grade.Legendary);
		Assert.Eq(rig.Service.Promote(0).Outcome, GradeOutcome.NotEnough, "10 stone needed, 0 owned");
		for (int i = 0; i < 2; i++)
		{
			Assert.Eq(CostText(rig.Bar.SlotView(i)!), "100g", "new views ignore the old loadout");
			Assert.Null(rig.Bar.SlotView(i)!.Icon.Texture);
			Assert.False(rig.Bar.SlotView(i)!.IsFlashing, "new views ignore the old service");
		}
	}

	public void TestAHeldTicketPricesEveryApplicableSlotAtOneTicket()
	{
		Rig rig = MakeRig(0, 0);
		rig.Loadout.SetSlot(1, rig.Def, Grade.Revealed);
		Assert.Eq(CostText(rig.Bar.SlotView(0)!), "100g");
		Assert.True(rig.Bar.SlotView(0)!.IsDimmed, "no gold");
		rig.Wallet.Add(CurrencyKind.Ticket, 1);
		Assert.Eq(CostText(rig.Bar.SlotView(0)!), "1T");
		Assert.Eq(CostText(rig.Bar.SlotView(2)!), "1T");
		Assert.False(rig.Bar.SlotView(0)!.IsDimmed, "the ticket covers it");
		Assert.Eq(CostText(rig.Bar.SlotView(1)!), "MAX", "nothing to buy on a revealed slot");
		Assert.True(rig.Bar.SlotView(0)!.TooltipText.Contains("1T"));
		rig.Wallet.TrySpend(CurrencyKind.Ticket, 1);
		Assert.Eq(CostText(rig.Bar.SlotView(0)!), "100g", "back to gold once the ticket is gone");
	}

	public void TestAFreedBarIgnoresItsOldModels()
	{
		Rig rig = MakeRig(100, 0);
		rig.Bar.Free();
		rig.Wallet.Add(CurrencyKind.Gold, 5);
		rig.Loadout.SetSlot(0, rig.Def, Grade.Common);
		Assert.Eq(rig.Service.Promote(1).Outcome, GradeOutcome.Ok, "the models keep working; the freed views are never touched");
	}
}
