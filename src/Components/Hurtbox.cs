using Godot;

namespace VampireSurvivor.Components;

/// <summary>
/// Hit-receiving area. Knows which entity and actor it protects; holds no behaviour.
/// Configure in the scene: collision_layer = the owner's hurt layer, collision_mask = 0, monitoring = false.
/// </summary>
public partial class Hurtbox : Area2D
{
	public ActorEntity? Entity { get; private set; }

	public Node2D? Actor { get; private set; }

	public void Bind(ActorEntity entity, Node2D actor)
	{
		Entity = entity;
		Actor = actor;
	}
}
