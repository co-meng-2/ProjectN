using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class CleanupTest : TestCase
{
	public void TestCleanupEndsRunningAbilitiesAndRemovesEveryEffect()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		var probe = new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe, Cooldown = DefFactory.CurveOf(1f, 1f, 1f, 1f, 1f, 1f), StayActive = true };
		AbilityGrant grant = actor.Grant(probe, 1);
		actor.ApplyPassive(DefFactory.Might(), 3);
		Assert.True(grant.Ability!.Activate(out AbilityActivationFailures _));

		actor.Cleanup();

		Assert.Eq(probe.Ends, 1, "the running instance was ended");
		Assert.Eq(System.Linq.Enumerable.Count(actor.EffectsManager.GetActiveEffects()), 0);
		Assert.Eq(actor.Abilities.GrantedAbilities.Count, 0);
	}

	public void TestFreeingACleanedUpActorLeavesOtherEntitiesTicking()
	{
		ActorEntity doomed = ActorFixture.BareActor(this, new StatBlock());
		ActorEntity survivor = ActorFixture.BareActor(this, new StatBlock());
		doomed.Grant(new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe, Cooldown = DefFactory.CurveOf(1f, 1f, 1f, 1f, 1f, 1f) }, 1);
		survivor.ApplyPassive(DefFactory.Might(), 2);

		doomed.Cleanup();
		doomed.Free();
		survivor.Tick(0.5);

		Assert.AlmostEq(survivor.Attributes[Stats.Attack].DisplayValue, 1.2, 0.0005);
	}

	public async System.Threading.Tasks.Task TestCleanupDeferredRunsAfterTheCurrentCallback()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		actor.ApplyPassive(DefFactory.Might(), 3);
		actor.CleanupDeferred();
		Assert.Eq(System.Linq.Enumerable.Count(actor.EffectsManager.GetActiveEffects()), 1, "not yet");
		await NextFrame();
		Assert.Eq(System.Linq.Enumerable.Count(actor.EffectsManager.GetActiveEffects()), 0, "after the frame");
	}

	public void TestCleanupTwiceIsHarmless()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		actor.ApplyPassive(DefFactory.Might(), 3);
		actor.Cleanup();
		actor.Cleanup();
		Assert.True(GodotObject.IsInstanceValid(actor));
	}
}
