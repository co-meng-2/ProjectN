using System;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Components;

/// <summary>
/// What an actor offers its abilities. The actor builds it once, stores it on <see cref="ActorEntity.Context"/> and every
/// ability behavior reads it from there.
/// </summary>
public sealed class AbilityContext(Node2D actor, Mover mover, TargetProvider targets, Node projectileLayer, Wallet wallet)
{
	public Node2D Actor { get; } = actor;

	public Mover Mover { get; } = mover;

	public TargetProvider Targets { get; } = targets;

	public Node ProjectileLayer { get; } = projectileLayer;

	/// <summary>The run's currency; on-kill/economy abilities credit it directly.</summary>
	public Wallet Wallet { get; } = wallet;

	/// <summary>Boss summons, injected by the Stage so summoned enemies count as kills and drop loot. Null for the player.</summary>
	public Action<EnemyDef, Vector2>? Summon { get; set; }

	/// <summary>An ability asks its actor to show an animation; the actor decides how.</summary>
	public event Action<StringName, Vector2>? AnimationRequested;

	public void RequestAnimation(StringName animation, Vector2 facing) => AnimationRequested?.Invoke(animation, facing);
}
