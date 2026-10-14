using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Actors;

/// <summary>A straight-flying projectile that damages the hurtboxes it touches through <see cref="DamageEffects.Hit"/>.</summary>
public partial class Projectile : Node2D
{
	private ProjectileDef? _def;
	private ActorEntity? _shooter;
	private float _damage;
	private Vector2 _velocity = Vector2.Zero;
	private int _pierceLeft;
	private bool _spent;

	[Export]
	public Sprite2D Sprite { get; set; } = null!;

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

	/// <summary>Call after the projectile has entered the tree.</summary>
	public void Launch(ProjectileDef def, ActorEntity shooter, float damage, Vector2 direction)
	{
		_def = def;
		_shooter = shooter;
		_damage = damage;
		_pierceLeft = def.Pierce;
		_velocity = direction.Normalized() * def.Speed;
		Rotation = direction.Angle();
		Sprite.Texture = def.Texture;
		((CircleShape2D)HitShape.Shape).Radius = def.HitRadius;
		Hitbox.CollisionMask = (uint)def.HitMask();
		Lifetime.Restart(def.Lifetime);
	}

	public override void _PhysicsProcess(double delta) => GlobalPosition += _velocity * (float)delta;

	private void OnHurtboxEntered(Hurtbox hurtbox)
	{
		if (_spent)
		{
			return;
		}

		if (IsInstanceValid(_shooter) && hurtbox.Entity is { } victim && IsInstanceValid(victim))
		{
			DamageEffects.Hit(_shooter!, victim, _damage);
		}

		if (_pierceLeft > 0)
		{
			_pierceLeft--;
			return;
		}

		_spent = true;
		QueueFree();
	}
}
