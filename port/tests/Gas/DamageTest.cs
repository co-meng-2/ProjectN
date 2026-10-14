using Gamesmiths.Forge.Godot.Nodes;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Gas;

public class DamageTest : TestCase
{
	private static StatBlock MakeStats(float maxHealth, float attack = 1f) => new() { MaxHealth = maxHealth, Attack = attack };

	private static double HealthOf(ActorEntity actor) => actor.Attributes[Stats.Health].DisplayValue;

	public void TestDamageScalesWithSourceAttack()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f, 2f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		DamageEffects.Hit(attacker, victim, 10f);
		Assert.AlmostEq(HealthOf(victim), 80.0);
	}

	public void TestFractionalAttackKeepsTwoDecimalsOfDamage()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f, 1.35f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		DamageEffects.Hit(attacker, victim, 8f);
		Assert.Eq(victim.Attributes[Stats.Health].CurrentValue, 8920, "8.0 x 1.35 = 10.80 damage -> 89.20 health");
	}

	public void TestDamageIsTruncatedOnceBelowTheHundredth()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f, 1.5f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		DamageEffects.Hit(attacker, victim, 0.03f);
		Assert.Eq(victim.Attributes[Stats.Health].CurrentValue, 10_000 - 4, "0.03 x 1.5 = 0.045 -> 4 raw, truncated");
	}

	public void TestDamageCannotPushHealthBelowZero()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(30f));
		DamageEffects.Hit(attacker, victim, 500f);
		Assert.AlmostEq(HealthOf(victim), 0.0);
	}

	public void TestZeroOrNegativeDamageNeverHeals()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		TestEffects.Flat(victim, Stats.Health, -3000);
		DamageEffects.Hit(attacker, victim, 0f);
		DamageEffects.Hit(attacker, victim, -50f);
		Assert.AlmostEq(HealthOf(victim), 70.0);
	}

	public void TestHittingAnEntityWithoutVitalsIsANoOp()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		var bare = Adopt(new ForgeEntity());
		DamageEffects.Hit(attacker, bare, 10f);
		Assert.True(bare.IsInsideTree());
	}

	public void TestEachHitRemovesDamageFromTheSameVictimCumulatively()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		DamageEffects.Hit(attacker, victim, 10f);
		DamageEffects.Hit(attacker, victim, 15f);
		Assert.AlmostEq(HealthOf(victim), 75.0);
	}

	public void TestInvulnerableTargetTakesNoDamageUntilTheTagIsRemoved()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		Gamesmiths.Forge.Effects.ActiveEffectHandle? tag = TestEffects.GrantTag(victim, GameplayTags.StateInvulnerable);
		DamageEffects.Hit(attacker, victim, 10f);
		Assert.AlmostEq(HealthOf(victim), 100.0, 0.001, "refused while the tag is present");
		victim.EffectsManager.RemoveEffect(tag!);
		DamageEffects.Hit(attacker, victim, 10f);
		Assert.AlmostEq(HealthOf(victim), 90.0);
	}

	public void TestAnInvulnerableSourceStillDealsDamage()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		TestEffects.GrantTag(attacker, GameplayTags.StateInvulnerable);
		DamageEffects.Hit(attacker, victim, 10f);
		Assert.AlmostEq(HealthOf(victim), 90.0);
	}
}
