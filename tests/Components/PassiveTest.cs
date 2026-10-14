using Gamesmiths.Forge.Effects;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class PassiveTest : TestCase
{
	private static double Display(ActorEntity actor, string key) => actor.Attributes[key].DisplayValue;

	public void TestPercentPassiveFollowsTheGradeCurve()
	{
		float[] expected = [1.1f, 1.2f, 1.35f, 1.5f, 1.7f, 2.0f];
		for (int level = 1; level <= 6; level++)
		{
			ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
			actor.ApplyPassive(DefFactory.Might(), level);
			Assert.AlmostEq(Display(actor, Stats.Attack), expected[level - 1], 0.0005, $"level {level}");
		}
	}

	public void TestFlatPassiveAddsDisplayUnits()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock { MaxHealth = 100f });
		actor.ApplyPassive(DefFactory.Vitality(), 3);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 170.0);
	}

	public void TestVitalityDoesNotHeal()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock { MaxHealth = 100f });
		actor.ApplyPassive(DefFactory.Vitality(), 6);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 340.0);
		Assert.AlmostEq(Display(actor, Stats.Health), 100.0);
	}

	public void TestRemovingTheOnlyPassiveRestoresTheStat()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ActiveEffectHandle? handle = actor.ApplyPassive(DefFactory.Might(), 4);
		Assert.NotNull(handle);
		actor.RemovePassive(handle);
		Assert.AlmostEq(Display(actor, Stats.Attack), 1.0);
	}

	public void TestRemovingVitalityPullsHealthBackToTheBaseMax()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock { MaxHealth = 100f });
		ActiveEffectHandle? handle = actor.ApplyPassive(DefFactory.Vitality(), 6);
		actor.FillHealth();
		Assert.AlmostEq(Display(actor, Stats.Health), 340.0);
		actor.RemovePassive(handle);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 100.0);
		Assert.AlmostEq(Display(actor, Stats.Health), 100.0);
	}

	public void TestReapplyAtAnotherLevelUsesTheNewLevel()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ActiveEffectHandle? first = actor.ApplyPassive(DefFactory.Might(), 1);
		actor.RemovePassive(first);
		actor.ApplyPassive(DefFactory.Might(), 4);
		Assert.AlmostEq(Display(actor, Stats.Attack), 1.5, 0.0005);
	}

	public void TestTwoSlotsWithTheSamePassiveStack()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		PassiveDef might = DefFactory.Might();
		ActiveEffectHandle? a = actor.ApplyPassive(might, 1);
		ActiveEffectHandle? b = actor.ApplyPassive(might, 2);
		Assert.AlmostEq(Display(actor, Stats.Attack), 1.3, 0.0005, "1.0 + 0.1 + 0.2");
		actor.RemovePassive(a);
		Assert.AlmostEq(Display(actor, Stats.Attack), 1.2, 0.0005, "only the level-2 slot remains");
		actor.RemovePassive(b);
		Assert.AlmostEq(Display(actor, Stats.Attack), 1.0, 0.0005);
	}

	public void TestRemovingANullOrAlreadyRemovedHandleIsHarmless()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ActiveEffectHandle? handle = actor.ApplyPassive(DefFactory.Might(), 2);
		actor.RemovePassive(handle);
		actor.RemovePassive(handle);
		actor.RemovePassive(null);
		Assert.AlmostEq(Display(actor, Stats.Attack), 1.0, 0.0005);
	}
}
