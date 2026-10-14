using System.Collections.Generic;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class AbilityContextTest : TestCase
{
	private AbilityContext MakeContext()
	{
		return new AbilityContext(
			Adopt(new Node2D()), Adopt(new Mover()), Adopt(new TargetProvider()), Adopt(new Node2D()), new Wallet());
	}

	public void TestRequestAnimationReachesSubscribers()
	{
		AbilityContext context = MakeContext();
		var seen = new List<(StringName, Vector2)>();
		context.AnimationRequested += (animation, facing) => seen.Add((animation, facing));
		context.RequestAnimation(AnimNames.Attack, Vector2.Left);
		Assert.Eq(seen.Count, 1);
		Assert.Eq(seen[0].Item1, AnimNames.Attack);
		Assert.Eq(seen[0].Item2, Vector2.Left);
	}

	public void TestRequestAnimationWithoutSubscribersIsHarmless()
	{
		MakeContext().RequestAnimation(AnimNames.Attack, Vector2.Right);
	}

	public void TestActorEntityTryActivateRunsAGrantedAbilityByTagThenCoolsDown()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		var probe = new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe, Cooldown = DefFactory.CurveOf(1f, 1f, 1f, 1f, 1f, 1f) };
		actor.Grant(probe, 1);
		Assert.True(actor.TryActivate(GameplayTags.AbilityProbe));
		Assert.Eq(probe.Starts, 1);
		Assert.False(actor.TryActivate(GameplayTags.AbilityProbe), "cooling down");
		actor.Tick(1.1);
		Assert.True(actor.TryActivate(GameplayTags.AbilityProbe));
	}

	public void TestTryActivateWithoutAGrantReturnsFalse()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		Assert.False(actor.TryActivate(GameplayTags.AbilityProbe));
	}
}
