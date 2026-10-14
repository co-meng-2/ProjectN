using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Components;

/// <summary>
/// A short-lived circular hit: damages every hurtbox on the target mask it overlaps, once each, draws a ring, then frees
/// itself. <c>onlyActor</c> narrows it to one actor (meteors and boss patterns hit the player but not the building).
/// The one area-hit path: Blade Sweep, meteors and boss patterns all damage through it, i.e. through DamageEffects.Hit.
/// </summary>
public partial class AreaStrike : Node2D
{
	private const float RingWidth = 2f;
	private const int RingSegments = 32;

	private ActorEntity? _attacker;
	private float _damage;
	private float _radius;
	private Node2D? _onlyActor;
	private bool _filtered;

	[Signal]
	public delegate void StruckEventHandler(Hurtbox hurtbox);

	[Export]
	public Color RingColor { get; set; } = new(1f, 0.9f, 0.6f, 0.8f);

	[Export]
	public Hitbox Hitbox { get; set; } = null!;

	[Export]
	public CollisionShape2D HitShape { get; set; } = null!;

	[Export]
	public Lifetime Lifetime { get; set; } = null!;

	public override void _Ready()
	{
		Hitbox.HurtboxEntered += OnHurtboxEntered;
		Lifetime.Expired += QueueFree;
	}

	/// <summary>Call after the strike has entered the tree.</summary>
	public void Strike(
		ActorEntity attacker,
		float damage,
		float radius,
		float seconds,
		int targetMask = PhysicsLayers.EnemyHurt,
		Node2D? onlyActor = null)
	{
		_attacker = attacker;
		_damage = damage;
		_radius = radius;
		_onlyActor = onlyActor;
		_filtered = onlyActor is not null;
		Hitbox.CollisionMask = (uint)targetMask;
		((CircleShape2D)HitShape.Shape).Radius = radius;
		Lifetime.Restart(seconds);
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_radius > 0f)
		{
			DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, RingSegments, RingColor, RingWidth);
		}
	}

	private void OnHurtboxEntered(Hurtbox hurtbox)
	{
		if (_filtered && (!IsInstanceValid(_onlyActor) || hurtbox.Actor != _onlyActor))
		{
			return;
		}

		if (!IsInstanceValid(_attacker) || hurtbox.Entity is null || !IsInstanceValid(hurtbox.Entity))
		{
			return;
		}

		if (DamageEffects.Hit(_attacker!, hurtbox.Entity, _damage))
		{
			EmitSignal(SignalName.Struck, hurtbox);
		}
	}
}
