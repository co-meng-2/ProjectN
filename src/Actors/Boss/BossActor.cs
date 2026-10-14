using System;
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Actors;

/// <summary>
/// The end-of-stage boss, configured by a <see cref="BossDef"/>: chases its target, damages allies it touches and owns its
/// pattern abilities (granted from the def). It stands still while it carries state.casting. When a pattern runs is
/// decided by a BossBrain the stage attaches. Not an enemy: no kill count, no drops. (Named BossActor because a class
/// Boss could not be referenced from the Boss namespace.)
/// </summary>
public partial class BossActor : CharacterBody2D
{
	private const float FreeDelayAfterDeath = 1f;

	private readonly List<AbilityGrant> _patterns = [];
	private readonly List<float> _weights = [];
	private Node2D? _target;
	private ActorBody? _body;

	[Signal]
	public delegate void DiedEventHandler(BossActor boss);

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
	public CollisionShape2D BodyShape { get; set; } = null!;

	[Export]
	public CollisionShape2D HurtShape { get; set; } = null!;

	[Export]
	public CollisionShape2D ContactShape { get; set; } = null!;

	public BossDef? Def { get; private set; }

	public AbilityContext? Context => Entity.Context;

	/// <summary>The granted patterns, in def order; the brain activates them through these handles.</summary>
	public IReadOnlyList<AbilityGrant> Patterns => _patterns;

	public IReadOnlyList<float> PatternWeights => _weights;

	public bool IsCasting => Entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.StateCasting));

	/// <summary>Call after the boss has entered the tree. <paramref name="summon"/> spawns an enemy through the stage.</summary>
	public void Setup(BossDef def, Node2D? target, TargetProvider targets, Node projectileLayer, Action<EnemyDef, Vector2>? summon = null)
	{
		Def = def;
		_target = target;
		Entity.ApplyStats(def.Stats!);
		Health.Bind(Entity);
		Hurtbox.Bind(Entity, this);
		Mover.Bind(this, Entity);
		_body = new ActorBody(this, Entity, Visual, Hurtbox, ContactHitbox, BodyShape);
		_body.WireContact(ContactDamage, def.ContactDamage, def.ContactInterval);
		Visual.Apply(def.Visual!, def.VisualScale, def.Tint);
		_body.SizeShapes(def.BodyRadius, HurtShape, ContactShape);
		Entity.Context = new AbilityContext(this, Mover, targets, projectileLayer, null) { Summon = summon };
		foreach (BossPatternEntry? entry in def.Patterns)
		{
			if (entry?.Pattern is null)
			{
				continue;
			}

			_patterns.Add(Entity.Grant(entry.Pattern, 1));
			_weights.Add(entry.Weight);
		}

		Health.Damaged += _ => Visual.FlashHit();
		Health.Died += OnDied;
		AddToGroup(Groups.Enemies);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsCasting || !IsInstanceValid(_target))
		{
			Mover.SetDirection(Vector2.Zero);
			Visual.SetMoving(Mover.IsDashing);
			return;
		}

		Vector2 toTarget = _target!.GlobalPosition - GlobalPosition;
		Mover.SetDirection(toTarget.Normalized());
		Visual.Face(toTarget.X);
		Visual.SetMoving(true);
	}

	private void OnDied()
	{
		// Synchronous, so a running pattern removes its telegraphs and strikes at once (the entity cleanup is deferred).
		foreach (AbilityGrant grant in _patterns)
		{
			if (grant.Ability is { IsValid: true, IsActive: true })
			{
				grant.Ability.Cancel();
			}
		}

		_body!.Die([.. GetChildren()], FreeDelayAfterDeath); // mover, contact damage, the attached brain
		EmitSignal(SignalName.Died, this);
	}
}
