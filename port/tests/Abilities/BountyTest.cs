using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Abilities;

public class BountyTest : TestCase
{
	public static BountyDef MakeBounty() => new()
	{
		Tag = GameplayTags.AbilityBounty,
		GoldPerKill = DefFactory.CurveOf(1f, 2f, 3f, 5f, 8f, 12f),
	};

	private (ActorEntity Entity, AbilityContext Context, AbilityGrant Grant) Rig(int level)
	{
		ActorEntity entity = ActorFixture.BareActor(this, new StatBlock());
		AbilityContext context = ActorFixture.Context(this, entity, Vector2.Zero);
		return (entity, context, entity.Grant(MakeBounty(), level));
	}

	private void Kill(ActorEntity entity) => entity.Raise<Node>(GameplayTags.EventKill, Adopt(new Node2D()));

	public void TestPaysGoldForEveryKillEventAtTheLevelAmount()
	{
		var (entity, context, _) = Rig(4);
		Kill(entity);
		Assert.Eq(context.Wallet.Gold, 5);
		entity.Raise<Node>(GameplayTags.EventKill, Adopt(new Node()));
		Assert.Eq(context.Wallet.Gold, 10, "the payload type does not matter");
		Assert.Eq(context.Wallet.Stone, 0);
	}

	public void TestLevelOneAndSixPayTheCurveEnds()
	{
		var (low, lowContext, _) = Rig(1);
		Kill(low);
		Assert.Eq(lowContext.Wallet.Gold, 1);
		var (high, highContext, _) = Rig(6);
		Kill(high);
		Assert.Eq(highContext.Wallet.Gold, 12);
	}

	public void TestIgnoresOtherEvents()
	{
		var (entity, context, _) = Rig(2);
		entity.Raise<Node>(GameplayTags.StateCasting, Adopt(new Node()));
		Assert.Eq(context.Wallet.Gold, 0);
	}

	public void TestARevokedBountyStopsPaying()
	{
		var (entity, context, grant) = Rig(2);
		entity.Revoke(grant);
		Kill(entity);
		Assert.Eq(context.Wallet.Gold, 0);
	}

	public void TestWithoutAContextItFailsQuietly()
	{
		var (entity, context, _) = Rig(2);
		entity.Context = null;
		Kill(entity);
		Assert.Eq(context.Wallet.Gold, 0);
	}

	public void TestBountyIsStartedByKillsAndNeedsItsCurve()
	{
		BountyDef bounty = MakeBounty();
		Assert.Eq(bounty.TriggerEventTag, GameplayTags.EventKill);
		Assert.False(bounty.AutoFire, "on demand, never auto-cast");
		Assert.NoErrors(AbilityFactory.Validate(bounty));
		bounty.GoldPerKill = null;
		Assert.Eq(bounty.Validate().Count, 1);
	}
}
