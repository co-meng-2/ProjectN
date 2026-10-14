using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Boss;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Actors;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Boss;

public class ChargeTest : TestCase
{
	public const string ChargePath = "res://data/bosses/patterns/charge.tres";
	private static readonly Vector2 BossAt = new(-100f, 40f);
	private static readonly Vector2 PlayerAt = new(150f, 40f);

	private sealed record Rig(Player Player, BossActor Boss, ChargeDef Charge, Node Layer);

	private Rig MakeRig()
	{
		Player player = ActorFixture.Player(this);
		player.GlobalPosition = PlayerAt;
		var charge = DefFactory.Fresh<ChargeDef>(ChargePath);
		BossActor boss = ActorFixture.Boss(this, player, DefFactory.Boss(GD.Load<PackedScene>(BossActorTest.BossScene), charge), BossAt);
		return new Rig(player, boss, charge, player.Context!.ProjectileLayer);
	}

	public async Task TestWarnsAlongALineThenChargesThroughThePlayerOnce()
	{
		Rig rig = MakeRig();
		var (building, _) = ActorFixture.HurtTarget(this, new Vector2(50f, 40f), PhysicsLayers.AllyHurt);
		Assert.True(ActorFixture.Fire(rig.Boss.Patterns[0]));
		Assert.True(rig.Boss.IsCasting);
		var warnings = ActorFixture.LiveChildren<Telegraph>(rig.Layer);
		Assert.Eq(warnings.Count, 1);
		Assert.Eq(warnings[0].Shape, TelegraphShape.Line);
		Assert.Eq(warnings[0].GlobalPosition, BossAt);
		Assert.AlmostEq(warnings[0].LineOffset.X, 360.0, 0.5, "toward the player, full distance");
		Assert.AlmostEq(warnings[0].LineOffset.Y, 0.0, 0.5);
		rig.Boss.Entity.Tick(0.5);
		await WaitSeconds(0.2);
		Assert.AlmostEq(rig.Boss.GlobalPosition.X, BossAt.X, 0.5, "no movement during the warning");
		Assert.AlmostEq(rig.Player.Health.Current(), 100.0);
		rig.Boss.Entity.Tick(0.3);
		Assert.Eq(ActorFixture.LiveChildren<Telegraph>(rig.Layer).Count, 0, "the warning is gone once the charge starts");
		await WaitSeconds(0.55);
		Assert.AlmostEq(rig.Player.Health.Current(), 75.0, 0.001, "25 damage");
		await WaitSeconds(0.1);
		Assert.AlmostEq(rig.Player.Health.Current(), 75.0, 0.001, "still inside the strike's life: exactly once");
		await WaitSeconds(0.3);
		Assert.True(rig.Boss.GlobalPosition.X > 200f, $"dashed about 360 px (now at {rig.Boss.GlobalPosition.X})");
		Assert.AlmostEq(building.Attributes[Stats.Health].DisplayValue, 100.0, 0.001, "the building on the line is spared");
		rig.Boss.Entity.Tick(rig.Charge.ChargeSeconds());
		Assert.False(rig.Boss.Patterns[0].Ability!.IsActive);
		Assert.False(rig.Boss.IsCasting);
	}

	public async Task TestCancellingDuringTheWarningRemovesTheTelegraph()
	{
		Rig rig = MakeRig();
		ActorFixture.Fire(rig.Boss.Patterns[0]);
		rig.Boss.Patterns[0].Ability!.Cancel();
		Assert.Eq(ActorFixture.LiveChildren<Telegraph>(rig.Layer).Count, 0);
		Assert.False(rig.Boss.IsCasting);
		rig.Boss.Entity.Tick(1.0);
		await WaitSeconds(0.3);
		Assert.True(rig.Boss.GlobalPosition.DistanceTo(BossAt) < 60f, "no dash after the abort (only the chase)");
	}

	public async Task TestCancellingMidChargeRemovesTheStrikeAndStopsTheDash()
	{
		Rig rig = MakeRig();
		ActorFixture.Fire(rig.Boss.Patterns[0]);
		rig.Boss.Entity.Tick(0.7);
		Assert.True(rig.Boss.Mover.IsDashing);
		Assert.Eq(ActorFixture.LiveChildren<AreaStrike>(rig.Boss).Count, 1);
		await WaitSeconds(0.1);
		rig.Boss.Patterns[0].Ability!.Cancel();
		Assert.False(rig.Boss.Mover.IsDashing, "the dash is cancelled");
		Assert.Eq(ActorFixture.LiveChildren<AreaStrike>(rig.Boss).Count, 0, "the riding strike is gone");
		Vector2 stoppedAt = rig.Boss.GlobalPosition;
		await WaitSeconds(0.2);
		Assert.True(rig.Boss.GlobalPosition.DistanceTo(stoppedAt) < 40f, "no more dash");
	}

	public void TestWithoutAPlayerTheChargeDoesNotStart()
	{
		Rig rig = MakeRig();
		rig.Player.Context!.Targets.Player = null;
		ActorFixture.Fire(rig.Boss.Patterns[0]);
		Assert.False(rig.Boss.Patterns[0].Ability!.IsActive);
		Assert.Eq(ActorFixture.LiveChildren<Telegraph>(rig.Layer).Count, 0);
	}

	public async Task TestThePlayerGoneDuringTheWarningHitsNobody()
	{
		Rig rig = MakeRig();
		var (building, _) = ActorFixture.HurtTarget(this, BossAt, PhysicsLayers.AllyHurt);
		ActorFixture.Fire(rig.Boss.Patterns[0]);
		rig.Player.Context!.Targets.Player = null;
		rig.Boss.Entity.Tick(0.8);
		await Settle();
		Assert.Eq(ActorFixture.LiveChildren<AreaStrike>(rig.Boss).Count, 0, "no unfiltered strike");
		Assert.False(rig.Boss.Patterns[0].Ability!.IsActive);
		Assert.AlmostEq(building.Attributes[Stats.Health].DisplayValue, 100.0, 0.001);
	}

	public void TestValidateReportsMissingPartsAndBadNumbers()
	{
		var charge = DefFactory.Fresh<ChargeDef>(ChargePath);
		Assert.NoErrors(charge.Validate(), "as shipped");
		Assert.AlmostEq(charge.ChargeSeconds(), 360.0 / 520.0);
		charge.StrikeScene = null;
		charge.Speed = 0f;
		Assert.Eq(charge.Validate().Count, 2);
		charge.StrikeScene = GD.Load<PackedScene>(BossActorTest.BossScene);
		Assert.Eq(charge.Validate().Count, 2, "a strike scene whose root is not an AreaStrike is rejected");
	}
}
