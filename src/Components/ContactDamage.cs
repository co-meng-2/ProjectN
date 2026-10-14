using System.Collections.Generic;
using Godot;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Components;

/// <summary>Periodically damages every hurtbox overlapping a hitbox (enemy melee, hazards).</summary>
public partial class ContactDamage : Node
{
	private Hitbox? _hitbox;
	private ActorEntity? _source;
	private float _damage;
	private float _interval = 1f;
	private double _cooldown;

	[Signal]
	public delegate void AttackedEventHandler(Godot.Collections.Array<Hurtbox> targets);

	public void Bind(Hitbox hitbox, ActorEntity source, float damage, float interval)
	{
		_hitbox = hitbox;
		_source = source;
		_damage = damage;
		_interval = interval;
	}

	public void Step(double delta)
	{
		_cooldown = System.Math.Max(_cooldown - delta, 0.0);
		if (_cooldown > 0.0 || !IsInstanceValid(_hitbox) || !IsInstanceValid(_source))
		{
			return;
		}

		List<Hurtbox> targets = _hitbox!.OverlappingHurtboxes();
		if (targets.Count == 0)
		{
			return;
		}

		foreach (Hurtbox target in targets)
		{
			if (target.Entity is { } entity && IsInstanceValid(entity))
			{
				DamageEffects.Hit(_source!, entity, _damage);
			}
		}

		_cooldown = _interval;
		EmitSignal(SignalName.Attacked, new Godot.Collections.Array<Hurtbox>(targets));
	}

	public override void _PhysicsProcess(double delta) => Step(delta);
}
