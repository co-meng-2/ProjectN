using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>One attack in a boss's repertoire and its weight in Random order.</summary>
public partial class BossPatternEntry : Resource
{
	[Export]
	public ActiveAbilityDef? Pattern { get; set; }

	[Export]
	public float Weight { get; set; } = 1f;

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (Weight <= 0f)
		{
			errors.Add("BossPatternEntry: weight must be > 0");
		}

		if (Pattern is null)
		{
			errors.Add("BossPatternEntry: pattern is null");
		}
		else
		{
			errors.AddRange(Pattern.Validate());
		}

		return errors;
	}
}
