using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Abilities;

public class BladeSweepTest : TestCase
{
	private const string StrikeScene = "res://src/Components/area_strike.tscn";

	public static BladeSweepDef MakeSweep() => new()
	{
		Tag = GameplayTags.AbilitySweep,
		Cooldown = DefFactory.CurveOf(1.6f, 1.4f, 1.2f, 1f, 0.85f, 0.7f),
		StrikeScene = GD.Load<PackedScene>(StrikeScene),
		Radius = DefFactory.CurveOf(40f, 48f, 56f, 66f, 78f, 95f),
		Damage = DefFactory.CurveOf(10f, 14f, 19f, 25f, 33f, 45f),
		StrikeSeconds = 0.2f,
	};

	private (ActorEntity Entity, AbilityContext Context, AbilityGrant Grant) Rig(int level, Vector2 at)
	{
		ActorEntity entity = ActorFixture.BareActor(this, new StatBlock());
		AbilityContext context = ActorFixture.Context(this, entity, at);
		return (entity, context, entity.Grant(MakeSweep(), level));
	}

	private static bool Fire(AbilityGrant grant) => grant.Ability!.Activate(out AbilityActivationFailures _);

	private static List<AreaStrike> Strikes(AbilityContext context) => [.. context.ProjectileLayer.GetChildren().OfType<AreaStrike>()];

	private Node2D EnemyAt(Vector2 at)
	{
		Node2D enemy = Adopt(new Node2D { GlobalPosition = at });
		enemy.AddToGroup(Groups.Enemies);
		return enemy;
	}

	public void TestSweepsOnlyWhenAnEnemyIsWithinTheLevelRadius()
	{
		var (_, context, grant) = Rig(1, new Vector2(100f, 50f));
		Node2D enemy = EnemyAt(new Vector2(160f, 50f));
		Fire(grant);
		Assert.Eq(Strikes(context).Count, 0, "60 px is outside a 40 px sweep");
		enemy.GlobalPosition = new Vector2(130f, 50f);
		Assert.True(Fire(grant), "a miss started no cooldown");
		List<AreaStrike> strikes = Strikes(context);
		Assert.Eq(strikes.Count, 1);
		Assert.AlmostEq(((CircleShape2D)strikes[0].HitShape.Shape).Radius, 40.0);
		Assert.Eq(strikes[0].GlobalPosition, new Vector2(100f, 50f), "centred on the owner");
	}

	public void TestCooldownFollowsTheCurve()
	{
		var (entity, context, grant) = Rig(2, Vector2.Zero);
		EnemyAt(new Vector2(30f, 0f));
		Assert.True(Fire(grant));
		Assert.Eq(Strikes(context).Count, 1);
		entity.Tick(1.0);
		Assert.False(Fire(grant), "still cooling down (1.4 s at level 2)");
		entity.Tick(0.5);
		Assert.True(Fire(grant));
		Assert.Eq(Strikes(context).Count, 2);
	}

	public async Task TestDamageAndRadiusScaleWithLevel()
	{
		var (_, context, grant) = Rig(6, Vector2.Zero);
		var (victim, _) = ActorFixture.HurtTarget(this, new Vector2(80f, 0f), inEnemyGroup: true);
		Assert.True(Fire(grant));
		Assert.Eq(Strikes(context).Count, 1, "80 px is inside the level-6 radius of 95");
		await Settle();
		Assert.AlmostEq(victim.Attributes[Stats.Health].DisplayValue, 55.0, 0.01, "level 6 damage is 45");
	}

	public void TestValidateReportsMissingData()
	{
		BladeSweepDef sweep = MakeSweep();
		Assert.NoErrors(sweep.Validate(), "as shipped");
		sweep.Radius = null;
		sweep.StrikeScene = null;
		Assert.Eq(sweep.Validate().Count, 2);
		sweep.StrikeSeconds = 0f;
		Assert.Eq(sweep.Validate().Count, 3);
	}
}
