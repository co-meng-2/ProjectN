using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>How the boss brain picks the next pattern.</summary>
public enum BossOrder
{
	/// <summary>List order, wrapping.</summary>
	Sequence,

	/// <summary>Weighted, never the same twice in a row when there are alternatives.</summary>
	Random,
}

/// <summary>
/// The end-of-stage boss: a generic boss actor scene configured like an enemy, plus its attack patterns and how the brain
/// picks them. Each pattern must be a boss pattern (Boss layer); BossBrain.ValidateDef checks that.
/// </summary>
[GlobalClass]
public partial class BossDef : Resource
{
	[Export]
	public StringName Id { get; set; } = string.Empty;

	[Export]
	public string DisplayName { get; set; } = string.Empty;

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
	public float ContactDamage { get; set; }

	[Export]
	public float ContactInterval { get; set; } = 1f;

	[Export]
	public float BodyRadius { get; set; } = 10f;

	[Export]
	public Godot.Collections.Array<BossPatternEntry> Patterns { get; set; } = [];

	[Export]
	public BossOrder Order { get; set; } = BossOrder.Random;

	/// <summary>Seconds of chasing between the end of one pattern and the start of the next.</summary>
	[Export]
	public float PatternGap { get; set; }

	public List<string> Validate()
	{
		var errors = new List<string>();
		string id = Id.ToString();
		if (id.Length == 0)
		{
			errors.Add("BossDef: id is empty");
		}

		if (DisplayName.Length == 0)
		{
			errors.Add($"BossDef '{id}': display_name is empty");
		}

		if (Scene is null)
		{
			errors.Add($"BossDef '{id}': scene is null");
		}

		if (BodyRadius <= 0f || ContactDamage < 0f || ContactInterval <= 0f || VisualScale <= 0f)
		{
			errors.Add($"BossDef '{id}': body_radius, contact_interval and visual_scale must be > 0, contact_damage >= 0");
		}

		if (Visual is null)
		{
			errors.Add($"BossDef '{id}': visual is null");
		}
		else
		{
			errors.AddRange(Visual.Validate());
		}

		if (Stats is null)
		{
			errors.Add($"BossDef '{id}': stats is null");
		}
		else
		{
			errors.AddRange(Stats.Validate());
		}

		if (PatternGap <= 0f)
		{
			errors.Add($"BossDef '{id}': pattern_gap must be > 0");
		}

		if (Patterns.Count == 0)
		{
			errors.Add($"BossDef '{id}': needs at least one pattern");
		}

		foreach (BossPatternEntry? entry in Patterns)
		{
			if (entry is null)
			{
				errors.Add($"BossDef '{id}': null pattern");
				continue;
			}

			foreach (string error in entry.Validate())
			{
				errors.Add($"BossDef '{id}': {error}");
			}
		}

		return errors;
	}
}
