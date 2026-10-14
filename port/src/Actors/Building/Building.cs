using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Actors;

/// <summary>The structure the player defends. Emits <see cref="Destroyed"/> when its health reaches zero.</summary>
public partial class Building : StaticBody2D
{
	[Signal]
	public delegate void DestroyedEventHandler();

	[Export]
	public ActorEntity Entity { get; set; } = null!;

	[Export]
	public Health Health { get; set; } = null!;

	[Export]
	public Hurtbox Hurtbox { get; set; } = null!;

	[Export]
	public Sprite2D Sprite { get; set; } = null!;

	public override void _Ready() => AddToGroup(Groups.Building);

	/// <summary>Call after the building has entered the tree.</summary>
	public void Setup(Texture2D texture, StatBlock stats)
	{
		Sprite.Texture = texture;
		Entity.ApplyStats(stats);
		Health.Bind(Entity);
		Hurtbox.Bind(Entity, this);
		Health.Died += () => EmitSignal(SignalName.Destroyed);
	}
}
