using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Components;

/// <summary>
/// The body plumbing Enemy and Boss share: sizing the three collision circles from one radius, wiring the contact-damage
/// component, and the physical part of dying. The actor keeps its own signals and extras.
/// </summary>
public sealed class ActorBody(
	Node2D actor,
	ActorEntity entity,
	ActorVisual visual,
	Hurtbox hurtbox,
	Hitbox contactHitbox,
	CollisionShape2D bodyShape)
{
	public const float HurtboxPadding = 2f;
	public const float ContactPadding = 4f;

	/// <summary>Sizes the body, hurtbox and contact circles (each a CircleShape2D) around <paramref name="radius"/>.</summary>
	public void SizeShapes(float radius, CollisionShape2D hurtShape, CollisionShape2D contactShape)
	{
		((CircleShape2D)bodyShape.Shape).Radius = radius;
		((CircleShape2D)hurtShape.Shape).Radius = radius + HurtboxPadding;
		((CircleShape2D)contactShape.Shape).Radius = radius + ContactPadding;
	}

	public void WireContact(ContactDamage contact, float damage, float interval)
	{
		contact.Bind(contactHitbox, entity, damage, interval);
	}

	/// <summary>
	/// Leaves the enemy group, stops the actor and every node in <paramref name="stopped"/> processing, disables the colliders,
	/// schedules the entity cleanup, plays the death animation and frees the actor after <paramref name="freeDelay"/>.
	/// The caller emits its own <c>Died</c> afterwards.
	/// </summary>
	public void Die(IEnumerable<Node> stopped, float freeDelay)
	{
		actor.RemoveFromGroup(Groups.Enemies);
		actor.SetPhysicsProcess(false);
		foreach (Node node in stopped)
		{
			node.SetPhysicsProcess(false);
		}

		hurtbox.SetDeferred(Area2D.PropertyName.Monitorable, false);
		contactHitbox.SetDeferred(Area2D.PropertyName.Monitoring, false);
		bodyShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		entity.CleanupDeferred();
		visual.PlayOnce(AnimNames.Death);
		actor.GetTree().CreateTimer(freeDelay).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(actor))
			{
				actor.QueueFree();
			}
		};
	}
}
