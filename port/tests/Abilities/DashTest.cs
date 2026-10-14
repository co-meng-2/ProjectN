using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Abilities;

public class DashTest : TestCase
{
	private static DashDef MakeDash() => new()
	{
		Tag = GameplayTags.AbilityDash,
		Cooldown = DefFactory.CurveOf(1.2f, 1.2f, 1.2f, 1.2f, 1.2f, 1.2f),
		Speed = 480f,
		Duration = 0.18f,
	};

	private (ActorEntity Entity, AbilityContext Context, AbilityGrant Grant) Rig()
	{
		ActorEntity entity = ActorFixture.BareActor(this, new StatBlock());
		AbilityContext context = ActorFixture.Context(this, entity, Vector2.Zero);
		return (entity, context, entity.Grant(MakeDash(), 1));
	}

	private bool Attacked(ActorEntity target)
	{
		ActorEntity attacker = ActorFixture.BareActor(this, new StatBlock());
		double before = target.Attributes[Stats.Health].DisplayValue;
		DamageEffects.Hit(attacker, target, 10f);
		return target.Attributes[Stats.Health].DisplayValue < before;
	}

	public void TestDashIsInvulnerableOnlyWhileActive()
	{
		var (entity, _, grant) = Rig();
		Assert.True(grant.Ability!.Activate(out AbilityActivationFailures _));
		Assert.False(Attacked(entity), "damage refused while dashing");
		entity.Tick(0.3);
		Assert.True(Attacked(entity), "damage accepted once the dash is over");
	}

	public void TestDashMovesTheMoverForItsDurationThenStops()
	{
		var (entity, context, grant) = Rig();
		context.Mover.SetDirection(Vector2.Up);
		grant.Ability!.Activate(out AbilityActivationFailures _);
		Assert.True(context.Mover.IsDashing);
		entity.Tick(0.1);
		Assert.True(context.Mover.IsDashing, "0.1 s of 0.18 s");
		entity.Tick(0.1);
		Assert.False(grant.Ability.IsActive, "the instance ended");
	}

	public void TestDashCoolsDownAfterwards()
	{
		var (entity, _, grant) = Rig();
		Assert.True(grant.Ability!.Activate(out AbilityActivationFailures _));
		entity.Tick(0.3);
		Assert.False(grant.Ability.Activate(out AbilityActivationFailures _), "on cooldown");
		entity.Tick(1.0);
		Assert.True(grant.Ability.Activate(out AbilityActivationFailures _));
	}

	public void TestRevokingMidDashStopsTheDashAndTheInvulnerability()
	{
		var (entity, context, grant) = Rig();
		grant.Ability!.Activate(out AbilityActivationFailures _);
		entity.Tick(0.05);
		Assert.True(context.Mover.IsDashing);
		entity.Revoke(grant);
		Assert.False(context.Mover.IsDashing, "the dash was cancelled");
		Assert.True(Attacked(entity), "and so was the invulnerability");
	}

	public void TestDashDefOwnsTheInvulnerableTagByDefault()
	{
		Assert.SequenceEq(new DashDef().OwnedTags, new[] { GameplayTags.StateInvulnerable });
	}
}
