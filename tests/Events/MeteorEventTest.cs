using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Events;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Events;

public class MeteorEventTest : TestCase
{
	private const string MeteorEventScene = "res://src/Events/meteor_event.tscn";
	private static readonly Vector2 PlayerAt = new(300f, 200f);

	private sealed record Rig(MeteorEvent Event, Player Player, ActorFixture.EventRecord Record);

	/// <summary>The player stands at PlayerAt; landing spread 0 makes every meteor land exactly on it.</summary>
	private Rig MakeRig(int maxHits = 3)
	{
		Player player = ActorFixture.Player(this);
		player.GlobalPosition = PlayerAt;
		var def = DefFactory.Event(GD.Load<PackedScene>(MeteorEventScene), 20f, "meteors");
		var context = ActorFixture.ContextFor(this, def, player);
		MeteorEvent meteors = Adopt(GD.Load<PackedScene>(MeteorEventScene).Instantiate<MeteorEvent>());
		meteors.SetPhysicsProcess(false);
		meteors.LandingSpread = 0f;
		meteors.MaxHits = maxHits;
		ActorFixture.EventRecord record = ActorFixture.WatchEvent(meteors);
		meteors.Begin(context);
		return new Rig(meteors, player, record);
	}

	private static int Telegraphs(MeteorEvent meteors) => ActorFixture.LiveChildren<Telegraph>(meteors).Count;

	private static int Strikes(MeteorEvent meteors) => ActorFixture.LiveChildren<AreaStrike>(meteors).Count;

	public async Task TestAWarningAppearsEveryIntervalAndHitsOnlyAfterIt()
	{
		Rig rig = MakeRig();
		Assert.SequenceEq(rig.Record.Progress, new[] { "Hits 0/3" });
		rig.Event.Step(1.1f);
		Assert.Eq(Telegraphs(rig.Event), 0, "nothing before the first interval");
		rig.Event.Step(0.15f);
		var warnings = ActorFixture.LiveChildren<Telegraph>(rig.Event);
		Assert.Eq(warnings.Count, 1);
		Assert.Eq(warnings[0].GlobalPosition, PlayerAt);
		Assert.AlmostEq(warnings[0].Radius, 60.0);
		rig.Event.Step(0.5f);
		await Settle();
		Assert.AlmostEq(rig.Player.Health.Current(), 100.0, 0.001, "still only a warning");
		rig.Event.Step(0.5f);
		Assert.Eq(Strikes(rig.Event), 1, "landed");
		Assert.Eq(Telegraphs(rig.Event), 0, "the warning is gone");
		await Settle();
		Assert.AlmostEq(rig.Player.Health.Current(), 85.0, 0.001);
		Assert.Eq(rig.Event.Hits, 1);
		Assert.Eq(rig.Record.Progress[^1], "Hits 1/3");
	}

	public async Task TestMoreThanMaxHitsFailsAtOnce()
	{
		Rig rig = MakeRig(1);
		rig.Event.Step(1.2f);
		rig.Event.Step(0.9f);
		await Settle();
		Assert.Eq(rig.Event.Hits, 1);
		Assert.True(rig.Event.IsRunning, "max_hits itself is allowed");
		rig.Event.Step(0.35f);
		rig.Event.Step(0.9f);
		await Settle();
		Assert.Eq(rig.Event.Hits, 2);
		Assert.Eq(rig.Record.Failed, 1);
		Assert.False(rig.Event.IsRunning);
	}

	public async Task TestDodgedMeteorsDoNotCountAndSurvivingSucceeds()
	{
		Rig rig = MakeRig(0);
		TestEffects.GrantTag(rig.Player.Entity, GameplayTags.StateInvulnerable);
		rig.Event.Step(1.2f);
		rig.Event.Step(0.9f);
		await Settle();
		Assert.Eq(rig.Event.Hits, 0, "an invulnerable dash dodges the meteor");
		Assert.AlmostEq(rig.Player.Health.Current(), 100.0, 0.001);
		rig.Event.Step(18f);
		Assert.Eq(rig.Record.Succeeded, 1, "survived the 20 s");
		Assert.Eq(rig.Record.Failed, 0);
	}

	public async Task TestAHitAfterTheEventEndedChangesNothing()
	{
		Rig rig = MakeRig(0);
		rig.Event.Step(1.2f);
		rig.Event.Step(0.9f);
		rig.Event.Step(18f);
		Assert.Eq(rig.Record.Succeeded, 1, "time is up before the strike reports");
		await Settle();
		Assert.Eq(rig.Record.Failed, 0, "a late hit cannot fail a finished event");
		Assert.Eq(rig.Event.Hits, 0);
	}

	public async Task TestAMeteorLandingAfterThePlayerIsGoneStrikesNothing()
	{
		Rig rig = MakeRig();
		var (building, _) = ActorFixture.HurtTarget(this, PlayerAt + new Vector2(15f, 0f), PhysicsLayers.AllyHurt);
		rig.Event.Step(1.2f);
		Assert.Eq(Telegraphs(rig.Event), 1);
		rig.Player.Free();
		rig.Event.Step(0.9f);
		await Settle();
		Assert.Eq(Strikes(rig.Event), 0, "no unfiltered strike");
		Assert.AlmostEq(building.Attributes[Stats.Health].DisplayValue, 100.0, 0.001, "the building is spared");
	}

	public async Task TestMeteorsSpareTheBuilding()
	{
		Rig rig = MakeRig();
		var (building, _) = ActorFixture.HurtTarget(this, PlayerAt + new Vector2(15f, 0f), PhysicsLayers.AllyHurt);
		rig.Event.Step(1.2f);
		rig.Event.Step(0.9f);
		await Settle();
		Assert.AlmostEq(rig.Player.Health.Current(), 85.0, 0.001);
		Assert.AlmostEq(building.Attributes[Stats.Health].DisplayValue, 100.0, 0.001, "ally hurt layer, but not the player");
	}

	public void TestAbortClearsPendingWarningsWithoutAResult()
	{
		Rig rig = MakeRig();
		rig.Event.Step(1.2f);
		Assert.Eq(Telegraphs(rig.Event), 1);
		rig.Event.Abort();
		Assert.Eq(Telegraphs(rig.Event), 0);
		rig.Event.Step(5f);
		Assert.Eq(Strikes(rig.Event), 0, "nothing lands after the abort");
		Assert.Eq(rig.Record.Succeeded, 0);
		Assert.Eq(rig.Record.Failed, 0);
	}

	public void TestValidateFlagsMissingPartsAndBadNumbers()
	{
		MeteorEvent meteors = GD.Load<PackedScene>(MeteorEventScene).Instantiate<MeteorEvent>();
		Assert.NoErrors(meteors.Validate(), "scene as shipped");
		meteors.StrikeScene = null;
		meteors.Interval = 0f;
		meteors.MaxHits = -1;
		Assert.Eq(meteors.Validate().Count, 3);
		meteors.StrikeScene = GD.Load<PackedScene>("res://src/Actors/Statue/statue.tscn");
		Assert.Eq(meteors.Validate().Count, 3, "a scene whose root is not an AreaStrike is rejected");
		meteors.Free();
	}
}
