using System;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Actors;

/// <summary>
/// A destructible event objective: an entity with health and a hurtbox on the enemy hurt layer, in the enemy group so
/// auto-aim shoots it. Not an enemy: no kill count, no event.kill, no drops. Drawn as a stone pillar that darkens as it
/// takes damage.
/// </summary>
public partial class Statue : Node2D
{
	[Signal]
	public delegate void DestroyedEventHandler(Statue statue);

	[Export]
	public ActorEntity Entity { get; set; } = null!;

	[Export]
	public Health Health { get; set; } = null!;

	[Export]
	public Hurtbox Hurtbox { get; set; } = null!;

	[Export]
	public float Radius { get; set; } = 16f;

	[Export]
	public Color StoneColor { get; set; } = new(0.62f, 0.6f, 0.56f);

	[Export]
	public Color CrackedColor { get; set; } = new(0.3f, 0.26f, 0.24f);

	public bool IsRemoved { get; private set; }

	/// <summary>Call after the statue has entered the tree.</summary>
	public void Setup(StatBlock stats)
	{
		Entity.ApplyStats(stats);
		Health.Bind(Entity);
		Hurtbox.Bind(Entity, this);
		Health.Changed += OnHealthChanged;
		Health.Died += OnDied;
		AddToGroup(Groups.Enemies);
		QueueRedraw();
	}

	/// <summary>Takes the statue out of play without counting it as destroyed (event cleanup). Safe inside physics callbacks.</summary>
	public void Remove()
	{
		if (IsRemoved)
		{
			return;
		}

		IsRemoved = true;
		RemoveFromGroup(Groups.Enemies);
		Hurtbox.SetDeferred(Area2D.PropertyName.Monitorable, false);
		Entity.CleanupDeferred();
		QueueFree();
	}

	public override void _Draw()
	{
		float ratio = Health.Maximum() > 0f ? Math.Clamp(Health.Current() / Health.Maximum(), 0f, 1f) : 1f;
		Color color = CrackedColor.Lerp(StoneColor, ratio);
		DrawRect(new Rect2(-Radius, -Radius * 0.4f, Radius * 2f, Radius * 0.6f), color.Darkened(0.25f));
		DrawRect(new Rect2(-Radius * 0.6f, -Radius * 2.4f, Radius * 1.2f, Radius * 2.1f), color);
		DrawCircle(new Vector2(0f, -Radius * 2.6f), Radius * 0.55f, color);
	}

	private void OnDied()
	{
		if (IsRemoved)
		{
			return;
		}

		Remove();
		EmitSignal(SignalName.Destroyed, this);
	}

	private void OnHealthChanged(float current, float maximum) => QueueRedraw();
}
