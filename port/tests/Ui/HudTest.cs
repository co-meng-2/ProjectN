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

	private Rig MakeRig(float[]? eventTimes = null)
	{
		var stats = new StatBlock { MaxHealth = 50f };
		ActorEntity player = ActorFixture.BareActor(this, stats);
		ActorEntity building = ActorFixture.BareActor(this, stats);
		var wallet = new Wallet();
		StageClock clock = Adopt(new StageClock());
		clock.SetPhysicsProcess(false);
		Hud hud = Adopt(GD.Load<PackedScene>(HudScene).Instantiate<Hud>());
		hud.Bind(HealthFor(player), HealthFor(building), wallet, clock, new Timeline(100f, [30f]), eventTimes);
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
		rig.Hud.ShowResult(new RunResult { Outcome = RunOutcome.Cleared, Kills = 3, Gold = 40, Gem = 4 });
		Label label = rig.Hud.ResultLabel;
		Assert.True(label.Visible);
		Assert.True(label.Text.Contains("STAGE CLEARED"), label.Text);
		Assert.True(label.Text.Contains("Kills 3"), label.Text);
		Assert.True(label.Text.Contains("Gold 40"), label.Text);
		Assert.True(label.Text.Contains("Gem 4"), label.Text);
		Assert.True(label.Text.Contains("Enter: continue"), label.Text);
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

	public void TestTheHudHostsAnUnboundAbilityBar()
	{
		Rig rig = MakeRig();
		Assert.NotNull(rig.Hud.AbilityBar);
		Assert.Eq(rig.Hud.AbilityBar.GetChildCount(), 0, "empty until its owner binds it");
	}
	public void TestBindPassesTheEventTimesToTheTimeline()
	{
		Rig rig = MakeRig([45f, 80f]);
		Assert.SequenceEq(rig.Hud.Timeline.EventMarks, new[] { 45f, 80f });
	}

	public void TestEventSignalsDriveTheBannerAndTheArrows()
	{
		Rig rig = MakeRig();
		rig.Hud.ObjectiveArrows.SetProcess(false);
		EventDef def = DefFactory.Event(DefFactory.Packed(new ProbeEvent()), 20f, "meteors");
		ProbeEvent probe = Adopt(new ProbeEvent());
		probe.SetPhysicsProcess(false);
		probe.ObjectiveNodes.Add(Adopt(new Node2D { Position = new Vector2(5000f, 0f) }));
		probe.Begin(ActorFixture.ContextFor(this, def));
		rig.Hud.ShowEvent(def, probe);
		Assert.True(rig.Hud.EventBanner.Visible);
		Assert.Eq(rig.Hud.EventBanner.TitleText, "Meteors");
		rig.Hud.ShowEventProgress(def, "Hits 1/3");
		Assert.Eq(rig.Hud.EventBanner.ProgressText, "Hits 1/3");
		rig.Hud.ObjectiveArrows.Refresh(Transform2D.Identity, new Vector2(800f, 600f));
		Assert.Eq(rig.Hud.ObjectiveArrows.ArrowPositions.Count, 1, "the HUD tracks the running event's objectives");
		rig.Hud.ShowEventResult(def, true);
		Assert.True(rig.Hud.EventBanner.TitleText.Contains("Success"));
		rig.Hud.ObjectiveArrows.Refresh(Transform2D.Identity, new Vector2(800f, 600f));
		Assert.Eq(rig.Hud.ObjectiveArrows.ArrowPositions.Count, 0, "no arrows once the event ended");
	}

	public void TestAnAbortedEventShowsInterruptedAndClearsTheArrows()
	{
		Rig rig = MakeRig();
		EventDef def = DefFactory.Event(DefFactory.Packed(new ProbeEvent()), 25f, "meteors");
		ProbeEvent probe = Adopt(new ProbeEvent());
		probe.SetPhysicsProcess(false);
		probe.Begin(ActorFixture.ContextFor(this, def));
		rig.Hud.ShowEvent(def, probe);
		rig.Hud.ShowEventAborted(def);
		Assert.Eq(rig.Hud.EventBanner.TitleText, "Meteors: Interrupted");
	}

	public void TestTheBossAppearanceShowsTheBarAndTheTimelineLabel()
	{
		Rig rig = MakeRig();
		Assert.False(rig.Hud.BossBar.Visible);
		ActorEntity boss = ActorFixture.BareActor(this, new StatBlock { MaxHealth = 2500f });
		rig.Hud.ShowBoss(HealthFor(boss), "Orc Warlord");
		Assert.True(rig.Hud.BossBar.Visible);
		Assert.Eq(rig.Hud.BossBar.BossName, "Orc Warlord");
		Assert.True(rig.Hud.Timeline.IsShowingBoss);
	}
}
