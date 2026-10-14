using System.Collections.Generic;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Boss;

public class PatternBehaviorTest : TestCase
{
	private sealed record Rig(ActorEntity Entity, AbilityGrant Grant, ProbePatternDef Pattern, List<bool> Ended);

	private Rig MakeRig(bool hold)
	{
		ActorEntity entity = ActorFixture.BareActor(this, new StatBlock());
		ActorFixture.Context(this, entity, Vector2.Zero);
		var pattern = new ProbePatternDef { Hold = hold };
		AbilityGrant grant = entity.Grant(pattern, 1);
		var ended = new List<bool>();
		entity.AbilityEnded += (_, cancelled) => ended.Add(cancelled);
		return new Rig(entity, grant, pattern, ended);
	}

	private static bool Casting(ActorEntity entity) => entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.StateCasting));

	public void TestAnInstantPatternRunsOnceAndEnds()
	{
		Rig rig = MakeRig(false);
		Assert.True(ActorFixture.Fire(rig.Grant));
		Assert.Eq(rig.Pattern.Starts, 1);
		Assert.False(rig.Grant.Ability!.IsActive);
		Assert.False(Casting(rig.Entity));
		Assert.Eq(rig.Ended.Count, 1);
	}

	public void TestAHeldPatternCastsUntilItCompletes()
	{
		Rig rig = MakeRig(true);
		ActorFixture.Fire(rig.Grant);
		Assert.True(rig.Grant.Ability!.IsActive);
		Assert.True(Casting(rig.Entity), "owned tag while running");
		rig.Entity.Tick(0.25);
		rig.Entity.Tick(0.5);
		Assert.AlmostEq(rig.Pattern.Stepped, 0.75, 0.001);
		rig.Pattern.Release();
		Assert.False(rig.Grant.Ability.IsActive);
		Assert.False(Casting(rig.Entity));
		Assert.Eq(rig.Ended.Count, 1);
		rig.Entity.Tick(1.0);
		Assert.AlmostEq(rig.Pattern.Stepped, 0.75, 0.001, "a finished pattern does not advance");
	}

	public void TestCancellingCallsTheAbortHookAndStripsTheCastingTag()
	{
		Rig rig = MakeRig(true);
		ActorFixture.Fire(rig.Grant);
		rig.Grant.Ability!.Cancel();
		Assert.Eq(rig.Pattern.Aborts, 1);
		Assert.False(Casting(rig.Entity));
		Assert.Eq(rig.Ended.Count, 1);
		rig.Pattern.Release();
		Assert.Eq(rig.Ended.Count, 1, "releasing after the abort changes nothing");
		Assert.True(ActorFixture.Fire(rig.Grant), "can run again");
		Assert.Eq(rig.Pattern.Starts, 2);
	}

	public void TestARefusingPatternNeverCasts()
	{
		Rig rig = MakeRig(true);
		rig.Pattern.Refuse = true;
		ActorFixture.Fire(rig.Grant);
		Assert.Eq(rig.Pattern.Starts, 0);
		Assert.False(rig.Grant.Ability!.IsActive);
		Assert.False(Casting(rig.Entity));
		Assert.Eq(rig.Pattern.Aborts, 0, "a refusal is not an abort");
	}
}
