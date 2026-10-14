using System.Collections.Generic;
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

public class VolleyTest : TestCase
{
	private const string ProjectileScene = "res://src/Actors/Projectile/projectile.tscn";

	private static VolleyDef MakeVolley(float count = 2f) => new()
	{
		Tag = GameplayTags.AbilityVolley,
		Cooldown = DefFactory.CurveOf(2.4f, 2.1f, 1.8f, 1.5f, 1.2f, 0.9f),
		ProjectileScene = GD.Load<PackedScene>(ProjectileScene),
		Projectile = new ProjectileDef { Texture = DefFactory.Texture(8, 8), Speed = 420f, Lifetime = 0.7f },
		ArrowCount = DefFactory.CurveOf(count, count, count, count, count, count),
		Damage = DefFactory.CurveOf(8f, 10f, 13f, 17f, 22f, 30f),
		AttackRange = 300f,
	};

	private (ActorEntity Entity, AbilityContext Context, AbilityGrant Grant, VolleyDef Def) Rig(int level = 1, float count = 2f)
	{
		ActorEntity entity = ActorFixture.BareActor(this, new StatBlock());
		AbilityContext context = ActorFixture.Context(this, entity, Vector2.Zero);
		VolleyDef def = MakeVolley(count);
		return (entity, context, entity.Grant(def, level), def);
	}

	private static bool Fire(AbilityGrant grant) => grant.Ability!.Activate(out AbilityActivationFailures _);

	private Node2D MakeEnemy(Vector2 at)
	{
		var node = Adopt(new Node2D { GlobalPosition = at });
		node.AddToGroup(Groups.Enemies);
		return node;
	}

	public void TestFiresOneArrowAtEachOfTheNearestEnemies()
	{
		var (_, context, grant, _) = Rig(count: 2f);
		MakeEnemy(new Vector2(100, 0));
		MakeEnemy(new Vector2(0, 150));
		MakeEnemy(new Vector2(500, 0));
		Assert.True(Fire(grant));
		Assert.Eq(context.ProjectileLayer.GetChildCount(), 2);
	}

	public void TestFewerEnemiesThanArrowsFiresOnlyAtThoseEnemies()
	{
		var (_, context, grant, _) = Rig(count: 5f);
		MakeEnemy(new Vector2(100, 0));
		Assert.True(Fire(grant));
		Assert.Eq(context.ProjectileLayer.GetChildCount(), 1);
	}

	public void TestNoEnemyInRangeFiresNothingAndStartsNoCooldown()
	{
		var (entity, context, grant, _) = Rig();
		MakeEnemy(new Vector2(900, 0));
		Assert.True(Fire(grant), "the activation itself succeeds");
		Assert.Eq(context.ProjectileLayer.GetChildCount(), 0);
		Assert.False(entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.CooldownVolley)), "a miss costs nothing");
		Assert.True(Fire(grant), "so it can try again immediately");
	}

	public void TestAShotStartsTheCooldownWhichScalesWithTheLevel()
	{
		var (entity, _, grant, _) = Rig(level: 6);
		MakeEnemy(new Vector2(100, 0));
		Assert.True(Fire(grant));
		Assert.True(entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.CooldownVolley)));
		entity.Tick(0.5);
		Assert.False(Fire(grant), "level 6 cooldown is 0.9 s");
		entity.Tick(0.5);
		Assert.True(Fire(grant));
	}

	public void TestAnimationIsRequestedTowardTheFirstTarget()
	{
		var (_, context, grant, _) = Rig();
		var seen = new List<(StringName, Vector2)>();
		context.AnimationRequested += (animation, facing) => seen.Add((animation, facing));
		MakeEnemy(new Vector2(100, 0));
		Fire(grant);
		Assert.Eq(seen.Count, 1);
		Assert.Eq(seen[0].Item1, AnimNames.Attack);
		Assert.AlmostEq(seen[0].Item2.X, 1.0, 0.001);
	}

	public async Task TestDamageComesFromTheLevelCurveAndTheShootersAttack()
	{
		var (_, _, grant, _) = Rig(level: 3, count: 1f);
		var (victim, _) = ActorFixture.HurtTarget(this, new Vector2(100, 0), maxHealth: 100f, inEnemyGroup: true);
		Fire(grant);
		await WaitSeconds(0.5);
		Assert.AlmostEq(victim.Attributes[Stats.Health].DisplayValue, 87.0, 0.01, "level 3 damage is 13");
	}

	public void TestValidateReportsMissingPieces()
	{
		var def = new VolleyDef { Tag = GameplayTags.AbilityVolley };
		Assert.True(def.Validate().Count >= 3, "scene/projectile, curves and range are all missing");
		Assert.NoErrors(MakeVolley().Validate());
	}
}
