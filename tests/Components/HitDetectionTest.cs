using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Components;

public class HitDetectionTest : TestCase
{
	private T MakeArea<T>(T area, int layer, int mask)
		where T : Area2D
	{
		area.CollisionLayer = (uint)layer;
		area.CollisionMask = (uint)mask;
		var shape = new CollisionShape2D { Shape = new CircleShape2D { Radius = 10f } };
		area.AddChild(shape);
		return Adopt(area);
	}

	private (Hitbox Hitbox, Hurtbox Hurtbox) Pair()
	{
		Hitbox hitbox = MakeArea(new Hitbox(), 0, PhysicsLayers.EnemyHurt);
		Hurtbox hurtbox = MakeArea(new Hurtbox(), PhysicsLayers.EnemyHurt, 0);
		return (hitbox, hurtbox);
	}

	public async Task TestHitboxReportsOverlappingHurtbox()
	{
		var (hitbox, hurtbox) = Pair();
		var entered = new List<Hurtbox>();
		hitbox.HurtboxEntered += found => entered.Add(found);
		await Settle();
		Assert.SequenceEq(entered, new[] { hurtbox });
		Assert.SequenceEq(hitbox.OverlappingHurtboxes(), new[] { hurtbox });
	}

	public async Task TestOncePerTargetIgnoresReentryUntilReset()
	{
		var (hitbox, hurtbox) = Pair();
		var entered = new List<Hurtbox>();
		hitbox.HurtboxEntered += found => entered.Add(found);
		await Settle();
		hurtbox.Position = new Vector2(1000, 0);
		await Settle();
		hurtbox.Position = Vector2.Zero;
		await Settle();
		Assert.Eq(entered.Count, 1, "re-entry ignored");
		hurtbox.Position = new Vector2(1000, 0);
		await Settle();
		hitbox.Reset();
		hurtbox.Position = Vector2.Zero;
		await Settle();
		Assert.Eq(entered.Count, 2, "re-entry counted after reset");
	}

	public async Task TestNonHurtboxAreasAreIgnored()
	{
		Hitbox hitbox = MakeArea(new Hitbox(), 0, PhysicsLayers.EnemyHurt);
		MakeArea(new Area2D(), PhysicsLayers.EnemyHurt, 0);
		await Settle();
		Assert.Eq(hitbox.OverlappingHurtboxes().Count, 0);
	}
}
