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

public class GroundSlamTest : TestCase
{
	public const string SlamPath = "res://port/data/bosses/patterns/ground_slam.tres";
	private static readonly Vector2 PlayerAt = new(150f, -60f);

	private sealed record Rig(Player Player, BossActor Boss, Node Layer);

	private Rig MakeRig()
	{
		Player player = ActorFixture.Player(this);
		player.GlobalPosition = PlayerAt;
		var slam = DefFactory.Fresh<GroundSlamDef>(SlamPath);
		BossActor boss = ActorFixture.Boss(this, player, DefFactory.Boss(GD.Load<PackedScene>(BossActorTest.BossScene), slam), new Vector2(-400f, 0f));
		return new Rig(player, boss, player.Context!.ProjectileLayer);
	}

	public async Task TestTheSlamHitsThePlayerOnlyAfterTheWarning()
	{
		Rig rig = MakeRig();
		var (building, _) = ActorFixture.HurtTarget(this, PlayerAt + new Vector2(30f, 0f), PhysicsLayers.AllyHurt);
		Assert.True(ActorFixture.Fire(rig.Boss.Patterns[0]));
		Assert.Eq(ActorFixture.LiveChildren<Telegraph>(rig.Layer).Count, 1);
		rig.Boss.Entity.Tick(0.6);
		await Settle();
		Assert.AlmostEq(rig.Player.Health.Current(), 100.0, 0.001, "still a warning");
		Assert.Eq(ActorFixture.LiveChildren<AreaStrike>(rig.Layer).Count, 0);
		Assert.True(rig.Boss.IsCasting);
		rig.Boss.Entity.Tick(0.5);
		Assert.Eq(ActorFixture.LiveChildren<AreaStrike>(rig.Layer).Count, 1);
		Assert.Eq(ActorFixture.LiveChildren<Telegraph>(rig.Layer).Count, 0);
		Assert.False(rig.Boss.IsCasting, "the slam completes when it strikes");
		await Settle();
		Assert.AlmostEq(rig.Player.Health.Current(), 70.0);
		Assert.AlmostEq(building.Attributes[Stats.Health].DisplayValue, 100.0, 0.001, "the building is spared");
	}

	public async Task TestTheSlamLandsWhereThePlayerWas()
	{
		Rig rig = MakeRig();
		ActorFixture.Fire(rig.Boss.Patterns[0]);
		rig.Player.GlobalPosition = PlayerAt + new Vector2(400f, 0f);
		rig.Boss.Entity.Tick(1.1);
		await Settle();
		Assert.AlmostEq(rig.Player.Health.Current(), 100.0, 0.001, "stepped out of the circle");
	}

	public async Task TestThePlayerGoneDuringTheWarningHitsNobody()
	{
		Rig rig = MakeRig();
		var (building, _) = ActorFixture.HurtTarget(this, PlayerAt + new Vector2(20f, 0f), PhysicsLayers.AllyHurt);
		ActorFixture.Fire(rig.Boss.Patterns[0]);
		rig.Player.Context!.Targets.Player = null;
		rig.Boss.Entity.Tick(1.1);
		await Settle();
		Assert.Eq(ActorFixture.LiveChildren<AreaStrike>(rig.Layer).Count, 0, "no unfiltered strike");
		Assert.False(rig.Boss.IsCasting);
		Assert.AlmostEq(building.Attributes[Stats.Health].DisplayValue, 100.0, 0.001);
	}

	public void TestValidateReportsMissingPartsAndBadNumbers()
	{
		var slam = DefFactory.Fresh<GroundSlamDef>(SlamPath);
		Assert.NoErrors(slam.Validate(), "as shipped");
		slam.Radius = 0f;
		slam.Damage = -1f;
		Assert.Eq(slam.Validate().Count, 2);
		slam.Radius = 90f;
		slam.Damage = 30f;
		slam.StrikeScene = GD.Load<PackedScene>(BossActorTest.BossScene);
		Assert.Eq(slam.Validate().Count, 1, "a strike scene whose root is not an AreaStrike is rejected");
	}
}
