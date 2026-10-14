using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class AreaStrikeTest : TestCase
{
	private const string StrikeScene = "res://port/src/Components/area_strike.tscn";

	private AreaStrike MakeStrike() => Adopt(GD.Load<PackedScene>(StrikeScene).Instantiate<AreaStrike>());

	private static double HealthOf(ActorEntity entity) => entity.Attributes[Stats.Health].DisplayValue;

	public async Task TestStrikeDamagesEachHurtboxInRadiusOnceThenExpires()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, new StatBlock());
		var (nearA, hurtA) = ActorFixture.HurtTarget(this, new Vector2(20f, 0f));
		var (nearB, _) = ActorFixture.HurtTarget(this, new Vector2(-30f, 10f));
		var (far, _) = ActorFixture.HurtTarget(this, new Vector2(200f, 0f));
		AreaStrike strike = MakeStrike();
		strike.Strike(attacker, 15f, 50f, 0.3f);
		await Settle();
		Assert.AlmostEq(HealthOf(nearA), 85.0);
		Assert.AlmostEq(HealthOf(nearB), 85.0);
		Assert.AlmostEq(HealthOf(far), 100.0);
		var holder = (Node2D)hurtA.GetParent();
		holder.GlobalPosition = new Vector2(400f, 0f);
		await Settle();
		holder.GlobalPosition = new Vector2(20f, 0f);
		await Settle();
		Assert.AlmostEq(HealthOf(nearA), 85.0, 0.001, "once per target");
		await WaitSeconds(0.4);
		Assert.False(GodotObject.IsInstanceValid(strike), "freed when the lifetime expires");
	}

	public void TestStrikeRadiusIsAppliedToItsOwnShape()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, new StatBlock());
		AreaStrike first = MakeStrike();
		AreaStrike second = MakeStrike();
		first.Strike(attacker, 1f, 95f, 0.3f);
		second.Strike(attacker, 1f, 40f, 0.3f);
		Assert.AlmostEq(((CircleShape2D)first.HitShape.Shape).Radius, 95.0);
		Assert.AlmostEq(((CircleShape2D)second.HitShape.Shape).Radius, 40.0, 0.001, "shapes are local to each instance");
	}

	public async Task TestOnlyActorNarrowsTheStrikeToOneTarget()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, new StatBlock());
		var (chosen, chosenHurt) = ActorFixture.HurtTarget(this, new Vector2(10f, 0f));
		var (other, _) = ActorFixture.HurtTarget(this, new Vector2(-10f, 0f));
		AreaStrike strike = MakeStrike();
		strike.Strike(attacker, 15f, 50f, 0.3f, onlyActor: chosenHurt.Actor);
		await Settle();
		Assert.AlmostEq(HealthOf(chosen), 85.0);
		Assert.AlmostEq(HealthOf(other), 100.0, 0.001, "not the chosen actor");
	}
}
