using System.Collections.Generic;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class HealthTest : TestCase
{
	private static StatBlock MakeStats(float maxHealth) => new() { MaxHealth = maxHealth };

	private Health HealthFor(ActorEntity actor)
	{
		var health = new Health();
		actor.AddChild(health);
		return health;
	}

	public void TestBindEmitsInitialValues()
	{
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(50f));
		Health health = HealthFor(victim);
		var seen = new List<(float, float)>();
		health.Changed += (current, maximum) => seen.Add((current, maximum));
		health.Bind(victim);
		Assert.SequenceEq(seen, new[] { (50f, 50f) });
	}

	public void TestDamageEmitsDamagedAndChanged()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(50f));
		Health health = HealthFor(victim);
		health.Bind(victim);
		var damages = new List<float>();
		var changes = new List<float>();
		health.Damaged += amount => damages.Add(amount);
		health.Changed += (current, _) => changes.Add(current);
		DamageEffects.Hit(attacker, victim, 12f);
		Assert.SequenceEq(damages, new[] { 12f });
		Assert.SequenceEq(changes, new[] { 38f });
		Assert.AlmostEq(health.Current(), 38.0);
	}

	public void TestLethalDamageEmitsDiedExactlyOnce()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(20f));
		Health health = HealthFor(victim);
		health.Bind(victim);
		int deaths = 0;
		health.Died += () => deaths++;
		DamageEffects.Hit(attacker, victim, 500f);
		DamageEffects.Hit(attacker, victim, 500f);
		Assert.Eq(deaths, 1);
		Assert.True(health.IsDead);
	}

	public void TestMaxHealthChangesEmitChangedWithoutDamage()
	{
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		Health health = HealthFor(victim);
		health.Bind(victim);
		int damaged = 0;
		var maxima = new List<float>();
		health.Damaged += _ => damaged++;
		health.Changed += (_, maximum) => maxima.Add(maximum);
		TestEffects.Flat(victim, Stats.MaxHealth, 5000);
		Assert.Eq(damaged, 0, "raising max is not damage");
		Assert.SequenceEq(maxima, new[] { 150f });
	}

	public void TestFreeingTheHealthNodeStopsItsSubscriptions()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(50f));
		Health health = HealthFor(victim);
		health.Bind(victim);
		health.Free();
		DamageEffects.Hit(attacker, victim, 10f);
		Assert.AlmostEq(victim.Attributes[Stats.Health].DisplayValue, 40.0);
	}
}
