using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

public enum TargetPolicy
{
	Building,
	Player,
}

/// <summary>Everything that distinguishes one enemy type; the actor scene itself is generic.</summary>
[GlobalClass]
public partial class EnemyDef : Resource
{
	[Export]
	public StringName Id { get; set; } = string.Empty;

	[Export]
	public PackedScene? Scene { get; set; }

	[Export]
	public VisualDef? Visual { get; set; }

	[Export]
	public float VisualScale { get; set; } = 1f;

	[Export]
	public Color Tint { get; set; } = Colors.White;

	[Export]
	public StatBlock? Stats { get; set; }

	[Export]
	public float ContactDamage { get; set; } = 8f;

	[Export]
	public float ContactInterval { get; set; } = 0.9f;

	[Export]
	public TargetPolicy TargetPolicy { get; set; } = TargetPolicy.Building;

	[Export]
	public float BodyRadius { get; set; } = 10f;

	[Export]
	public DropTable? Drops { get; set; }

	public List<string> Validate()
	{
		var errors = new List<string>();
		string id = Id.ToString();
		if (id.Length == 0)
		{
			errors.Add("EnemyDef: id is empty");
		}

		if (Scene is null)
		{
			errors.Add($"EnemyDef '{id}': scene is null");
		}

		if (BodyRadius <= 0f || ContactDamage < 0f || ContactInterval <= 0f || VisualScale <= 0f)
		{
			errors.Add($"EnemyDef '{id}': body_radius, contact_interval and visual_scale must be > 0, contact_damage >= 0");
		}

		if (Visual is null)
		{
			errors.Add($"EnemyDef '{id}': visual is null");
		}
		else
		{
			errors.AddRange(Visual.Validate());
		}

		if (Stats is null)
		{
			errors.Add($"EnemyDef '{id}': stats is null");
		}
		else
		{
			errors.AddRange(Stats.Validate());
		}

		if (Drops is not null)
		{
			errors.AddRange(Drops.Validate());
		}

		return errors;
	}
}
