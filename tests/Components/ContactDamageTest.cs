using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class ContactDamageTest : TestCase
{
	private T MakeArea<T>(T area, int layer, int mask)
		where T : Area2D
	{
		area.CollisionLayer = (uint)layer;
		area.CollisionMask = (uint)mask;
		area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 10f } });
		return Adopt(area);
	}

	private (ContactDamage Contact, ActorEntity Attacker, ActorEntity Victim, Hurtbox Hurtbox) Rig()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, new StatBlock());
		ActorEntity victim = ActorFixture.BareActor(this, new StatBlock());
		Hitbox hitbox = MakeArea(new Hitbox(), 0, PhysicsLayers.AllyHurt);
		hitbox.OncePerTarget = false;
		Hurtbox hurtbox = MakeArea(new Hurtbox(), PhysicsLayers.AllyHurt, 0);
		hurtbox.Bind(victim, hurtbox);
		var contact = Adopt(new ContactDamage());
		contact.SetPhysicsProcess(false);
		contact.Bind(hitbox, attacker, 10f, 1f);
		return (contact, attacker, victim, hurtbox);
	}

	private static double HealthOf(ActorEntity entity) => entity.Attributes[Stats.Health].DisplayValue;

	public async Task TestDamagesOverlappingHurtboxThenWaitsForTheInterval()
	{
		var (contact, _, victim, _) = Rig();
		await Settle();
		var attacks = new List<int>();
		contact.Attacked += targets => attacks.Add(targets.Count);
		contact.Step(0.0);
		Assert.AlmostEq(HealthOf(victim), 90.0);
		contact.Step(0.5);
		Assert.AlmostEq(HealthOf(victim), 90.0, 0.001, "still cooling down");
		contact.Step(0.6);
		Assert.AlmostEq(HealthOf(victim), 80.0);
		Assert.SequenceEq(attacks, new[] { 1, 1 });
	}

	public async Task TestNothingHappensWithoutOverlap()
	{
		var (contact, _, victim, hurtbox) = Rig();
		hurtbox.Position = new Vector2(1000, 0);
		await Settle();
		contact.Step(0.0);
		Assert.AlmostEq(HealthOf(victim), 100.0);
	}

	public async Task TestAFreedVictimOrSourceDoesNotThrow()
	{
		var (contact, attacker, victim, _) = Rig();
		await Settle();
		victim.Free();
		contact.Step(0.0);

		var (contact2, attacker2, victim2, _) = Rig();
		await Settle();
		attacker2.Free();
		contact2.Step(0.0);
		Assert.AlmostEq(HealthOf(victim2), 100.0, 0.001, "a freed source deals no damage");
		Assert.True(GodotObject.IsInstanceValid(attacker));
	}
}
