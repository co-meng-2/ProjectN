using System;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Actors;

/// <summary>Generic enemy configured by an <see cref="EnemyDef"/>: chases a target and damages allies it touches.</summary>
public partial class Enemy : CharacterBody2D
{
	private const float FreeDelayAfterDeath = 0.6f;

	private Node2D? _target;
	private ActorBody? _body;

	[Signal]
	public delegate void DiedEventHandler(Enemy enemy);

	public event Action<DropRoll, Vector2>? LootDropped;

	[Export]
	public ActorEntity Entity { get; set; } = null!;

	[Export]
	public Health Health { get; set; } = null!;

	[Export]
	public Hurtbox Hurtbox { get; set; } = null!;

	[Export]
	public Hitbox ContactHitbox { get; set; } = null!;

	[Export]
	public ContactDamage ContactDamage { get; set; } = null!;

	[Export]
	public Mover Mover { get; set; } = null!;

	[Export]
	public ActorVisual Visual { get; set; } = null!;

	[Export]
	public LootDropper LootDropper { get; set; } = null!;

	[Export]
	public CollisionShape2D BodyShape { get; set; } = null!;

	[Export]
	public CollisionShape2D HurtShape { get; set; } = null!;

	[Export]
	public CollisionShape2D ContactShape { get; set; } = null!;

	public EnemyDef? Def { get; private set; }

	/// <summary>Call after the enemy has entered the tree.</summary>
	public void Setup(EnemyDef def, Node2D? target, RunRng rng)
	{
		Def = def;
		_target = target;
		Entity.ApplyStats(def.Stats!);
		Health.Bind(Entity);
		Hurtbox.Bind(Entity, this);
		Mover.Bind(this, Entity);
		_body = new ActorBody(this, Entity, Visual, Hurtbox, ContactHitbox, BodyShape);
		_body.WireContact(ContactDamage, def.ContactDamage, def.ContactInterval);
		LootDropper.Bind(Health, def.Drops, rng, this);
		Visual.Apply(def.Visual!, def.VisualScale, def.Tint);
		_body.SizeShapes(def.BodyRadius, HurtShape, ContactShape);
		Health.Damaged += _ => Visual.FlashHit();
		Health.Died += OnDied;
		LootDropper.DropRequested += (roll, position) => LootDropped?.Invoke(roll, position);
		ContactDamage.Attacked += _ => Visual.PlayOnce(AnimNames.Attack);
		AddToGroup(Groups.Enemies);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(_target))
		{
			Mover.SetDirection(Vector2.Zero);
			Visual.SetMoving(false);
			return;
		}

		Vector2 toTarget = _target!.GlobalPosition - GlobalPosition;
		Mover.SetDirection(toTarget.Normalized());
		Visual.Face(toTarget.X);
		Visual.SetMoving(true);
	}

	private void OnDied()
	{
		_body!.Die([Mover, ContactDamage], FreeDelayAfterDeath);
		EmitSignal(SignalName.Died, this);
	}
}
