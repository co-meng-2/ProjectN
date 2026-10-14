using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Components;

/// <summary>
/// Hit-detecting area. Configure in the scene: collision_layer = 0, collision_mask = the target's hurt layer,
/// monitorable = false.
/// </summary>
public partial class Hitbox : Area2D
{
	private readonly HashSet<ulong> _alreadyHit = [];

	[Signal]
	public delegate void HurtboxEnteredEventHandler(Hurtbox hurtbox);

	/// <summary>When true, each hurtbox is reported once until <see cref="Reset"/> (projectiles). Contact damage polls instead.</summary>
	[Export]
	public bool OncePerTarget { get; set; } = true;

	public override void _Ready() => AreaEntered += OnAreaEntered;

	public void Reset() => _alreadyHit.Clear();

	public List<Hurtbox> OverlappingHurtboxes()
	{
		var result = new List<Hurtbox>();
		foreach (Area2D area in GetOverlappingAreas())
		{
			if (area is Hurtbox hurtbox)
			{
				result.Add(hurtbox);
			}
		}

		return result;
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area is not Hurtbox hurtbox)
		{
			return;
		}

		if (OncePerTarget && !_alreadyHit.Add(hurtbox.GetInstanceId()))
		{
			return;
		}

		EmitSignal(SignalName.HurtboxEntered, hurtbox);
	}
}
