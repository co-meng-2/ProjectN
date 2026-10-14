using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class ActorEntityTest : TestCase
{
	private static StatBlock MakeStats(float maxHealth, float attack = 1f) => new() { MaxHealth = maxHealth, Attack = attack };

	private static double Display(ActorEntity actor, string key) => actor.Attributes[key].DisplayValue;

	public void TestApplyStatsSetsHealthAndMaxHealth()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(50f));
		Assert.AlmostEq(Display(actor, Stats.Health), 50.0);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 50.0);
	}

	public void TestStatsAboveTheDefaultMaxApplyToBoth()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(150f));
		Assert.AlmostEq(Display(actor, Stats.Health), 150.0);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 150.0);
	}

	public void TestApplyStatsSetsSpeedAndAttackWithTheirScales()
	{
		var stats = new StatBlock { MaxHealth = 100f, MoveSpeed = 140f, Attack = 1.35f };
		ActorEntity actor = ActorFixture.BareActor(this, stats);
		Assert.AlmostEq(Display(actor, Stats.MoveSpeed), 140.0);
		Assert.Eq(actor.Attributes[Stats.Attack].CurrentValue, 1350);
	}

	public void TestHealthIsClampedToMaxHealth()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(50f));
		TestEffects.Apply(actor, StatEffects.SetBase(Stats.Health, 8000));
		Assert.AlmostEq(Display(actor, Stats.Health), 50.0);
	}

	public void TestAttributeSetsAreNotSharedBetweenActors()
	{
		ActorEntity first = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity second = ActorFixture.BareActor(this, MakeStats(100f));
		TestEffects.Flat(first, Stats.Health, -4000);
		Assert.AlmostEq(Display(first, Stats.Health), 60.0);
		Assert.AlmostEq(Display(second, Stats.Health), 100.0);
	}

	public void TestLoweringMaxHealthPullsHealthDown()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(100f));
		TestEffects.Flat(actor, Stats.MaxHealth, -4000);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 60.0);
		Assert.AlmostEq(Display(actor, Stats.Health), 60.0);
	}

	public void TestRaisingMaxHealthNeverHeals()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(100f));
		TestEffects.Flat(actor, Stats.Health, -3000);
		TestEffects.Flat(actor, Stats.MaxHealth, 5000);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 150.0);
		Assert.AlmostEq(Display(actor, Stats.Health), 70.0);
	}

	public void TestFillHealthRaisesHealthToMax()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(100f));
		TestEffects.Flat(actor, Stats.Health, -3000);
		actor.FillHealth();
		Assert.AlmostEq(Display(actor, Stats.Health), 100.0);
	}

	public void TestAbsurdMaxHealthIsClampedToTheAttributeLimit()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(5_000_000f));
		Assert.Eq(actor.Attributes[Stats.MaxHealth].CurrentValue, 10_000_000);
		Assert.True(actor.Attributes[Stats.Health].CurrentValue <= 10_000_000);
	}
}
