using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;
using VampireSurvivor.Ui;

namespace VampireSurvivor.Tests.Ui;

public class HudTest : TestCase
{
	private const string HudScene = "res://port/src/Ui/Hud/hud.tscn";

	private sealed record Rig(Hud Hud, ActorEntity PlayerEntity, Wallet Wallet);

	private static Health HealthFor(ActorEntity entity)
	{
		var health = new Health();
		entity.AddChild(health);
		health.Bind(entity);
		return health;
	}

	private Rig MakeRig()
	{
		var stats = new StatBlock { MaxHealth = 50f };
		ActorEntity player = ActorFixture.BareActor(this, stats);
		ActorEntity building = ActorFixture.BareActor(this, stats);
		var wallet = new Wallet();
		StageClock clock = Adopt(new StageClock());
		clock.SetPhysicsProcess(false);
		Hud hud = Adopt(GD.Load<PackedScene>(HudScene).Instantiate<Hud>());
		hud.Bind(HealthFor(player), HealthFor(building), wallet, clock, new Timeline(100f, [30f]));
		return new Rig(hud, player, wallet);
	}

	public void TestBarsShowInitialHealthAndFollowDamage()
	{
		Rig rig = MakeRig();
		Assert.AlmostEq(rig.Hud.PlayerBar.Value, 50.0);
		Assert.AlmostEq(rig.Hud.PlayerBar.MaxValue, 50.0);
		DamageEffects.Hit(ActorFixture.BareActor(this, new StatBlock()), rig.PlayerEntity, 20f);
		Assert.AlmostEq(rig.Hud.PlayerBar.Value, 30.0);
	}

	public void TestWalletLabelFollowsTheWallet()
	{
		Rig rig = MakeRig();
		Assert.Eq(rig.Hud.WalletLabel.Text, "Gold 0   Stone 0   Ticket 0");
		rig.Wallet.Add(CurrencyKind.Gold, 7);
		rig.Wallet.Add(CurrencyKind.Stone, 2);
		rig.Wallet.Add(CurrencyKind.Ticket, 1);
		Assert.Eq(rig.Hud.WalletLabel.Text, "Gold 7   Stone 2   Ticket 1");
	}

	public void TestShowResultDisplaysOutcomeAndTotals()
	{
		Rig rig = MakeRig();
		rig.Hud.ShowResult(new RunResult { Outcome = RunOutcome.Cleared, Kills = 3, Gold = 40 });
		Label label = rig.Hud.ResultLabel;
		Assert.True(label.Visible);
		Assert.True(label.Text.Contains("STAGE CLEARED"), label.Text);
		Assert.True(label.Text.Contains("Kills 3"), label.Text);
		Assert.True(label.Text.Contains("Gold 40"), label.Text);
		Assert.True(label.Text.Contains("R: restart"), label.Text);
		rig.Hud.ShowResult(new RunResult { Outcome = RunOutcome.Failed });
		Assert.True(label.Text.Contains("GAME OVER"), label.Text);
	}

	public void TestAFreedHudStopsFollowingTheWallet()
	{
		Rig rig = MakeRig();
		rig.Hud.Free();
		rig.Wallet.Add(CurrencyKind.Gold, 5);
		Assert.Eq(rig.Wallet.Gold, 5, "the wallet keeps working; the freed HUD's label is never touched");
	}
}
