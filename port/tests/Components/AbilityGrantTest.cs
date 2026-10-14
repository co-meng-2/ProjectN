using Gamesmiths.Forge.Abilities;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class AbilityGrantTest : TestCase
{
	private static ProbeAbilityDef MakeProbe(params float[] cooldowns) => new()
	{
		Tag = GameplayTags.AbilityProbe,
		Cooldown = DefFactory.CurveOf(cooldowns),
	};

	private static bool Fire(AbilityGrant grant) => grant.Ability!.Activate(out AbilityActivationFailures _);

	public void TestGrantedAbilityActivatesAndItsCooldownBlocksReactivation()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe(1f, 1f, 1f, 1f, 1f, 1f);
		AbilityGrant grant = actor.Grant(probe, 1);
		Assert.NotNull(grant.Ability);
		Assert.True(Fire(grant));
		Assert.Eq(probe.Starts, 1);
		Assert.False(Fire(grant), "cooling down");
		actor.Tick(1.1);
		Assert.True(Fire(grant), "cooldown elapsed");
		Assert.Eq(probe.Starts, 2);
	}

	public void TestCooldownScalesWithTheAbilityLevel()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef slow = MakeProbe(2f, 1.8f, 1.6f, 1.4f, 1.2f, 1f);
		AbilityGrant levelOne = actor.Grant(slow, 1);
		Assert.True(Fire(levelOne));
		actor.Tick(1.5);
		Assert.False(Fire(levelOne), "level 1 cooldown is 2 s");

		ActorEntity other = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef fast = MakeProbe(2f, 1.8f, 1.6f, 1.4f, 1.2f, 1f);
		AbilityGrant levelSix = other.Grant(fast, 6);
		Assert.True(Fire(levelSix));
		other.Tick(1.1);
		Assert.True(Fire(levelSix), "level 6 cooldown is 1 s");
	}

	public void TestRevokeRemovesTheAbilityAndItsCooldown()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe(5f, 5f, 5f, 5f, 5f, 5f);
		AbilityGrant grant = actor.Grant(probe, 1);
		Assert.True(Fire(grant));
		actor.Revoke(grant);
		Assert.False(grant.Ability!.IsValid, "the handle is dead");
		Assert.Eq(actor.Abilities.GrantedAbilities.Count, 0);

		AbilityGrant again = actor.Grant(probe, 1);
		Assert.True(Fire(again), "a re-granted ability starts with a fresh cooldown");
		Assert.Eq(probe.Starts, 2);
	}

	public void TestRevokeCancelsARunningInstance()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe(1f, 1f, 1f, 1f, 1f, 1f);
		probe.StayActive = true;
		AbilityGrant grant = actor.Grant(probe, 1);
		Assert.True(Fire(grant));
		Assert.True(grant.Ability!.IsActive);
		actor.Revoke(grant);
		Assert.Eq(probe.Ends, 1, "OnEnded ran when the grant was removed");
	}

	public void TestRevokingNullOrTwiceIsHarmless()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		AbilityGrant grant = actor.Grant(MakeProbe(1f, 1f, 1f, 1f, 1f, 1f), 1);
		actor.Revoke(grant);
		actor.Revoke(grant);
		actor.Revoke(null);
		Assert.Eq(actor.Abilities.GrantedAbilities.Count, 0);
	}

	public void TestSameDefGrantedTwiceYieldsTwoGrantsWithTheirOwnLevel()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe(1f, 1f, 1f, 1f, 1f, 1f);
		AbilityGrant first = actor.Grant(probe, 1);
		AbilityGrant second = actor.Grant(probe, 4);
		Assert.True(first.Ability != second.Ability, "two grants, two ability handles");
		Assert.Eq(first.Ability!.Level, 1);
		Assert.Eq(second.Ability!.Level, 4);
		actor.Revoke(first);
		Assert.False(first.Ability.IsValid, "the first grant is gone");
		Assert.True(second.Ability.IsValid, "revoking one grant leaves the other");
		Assert.Eq(second.Ability.Level, 4);
	}

	public void TestRevokingOneOfTwoGrantsKeepsTheSharedCooldownRunning()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe(5f, 5f, 5f, 5f, 5f, 5f);
		AbilityGrant first = actor.Grant(probe, 1);
		AbilityGrant second = actor.Grant(probe, 1);
		Assert.True(Fire(second));
		actor.Revoke(first);
		Assert.False(Fire(second), "the cooldown the owner is under survives revoking the other grant");
		actor.Revoke(second);
		AbilityGrant again = actor.Grant(probe, 1);
		Assert.True(Fire(again), "once the last grant is revoked the cooldown is cleared");
	}

	public void TestActiveAbilityDefValidateReportsMissingTagAndBadCooldown()
	{
		var noTag = new ProbeAbilityDef { Tag = string.Empty, Cooldown = DefFactory.CurveOf(1f) };
		Assert.True(noTag.Validate().Count >= 1);
		var noCurve = new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe, Cooldown = null };
		Assert.Eq(noCurve.Validate().Count, 0, "no cooldown curve means no cooldown");
		var unregistered = new ProbeAbilityDef { Tag = "ability.nonexistent", Cooldown = DefFactory.CurveOf(1f) };
		Assert.True(AbilityFactory.Validate(unregistered).Count >= 1, "tags are checked against the Forge registry");
	}
}
