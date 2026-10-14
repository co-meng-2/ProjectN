using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>Base stats of an actor, authored in display units (floats) and converted to Forge integers once.</summary>
[GlobalClass]
public partial class StatBlock : Resource
{
	[Export]
	public float MaxHealth { get; set; } = 100f;

	[Export]
	public float MoveSpeed { get; set; } = 100f;

	/// <summary>Damage multiplier applied to hits this actor lands (1.0 = base damage).</summary>
	[Export]
	public float Attack { get; set; } = 1f;

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (MaxHealth <= 0f)
		{
			errors.Add("StatBlock: MaxHealth must be > 0");
		}

		if (MoveSpeed < 0f)
		{
			errors.Add("StatBlock: MoveSpeed must be >= 0");
		}

		return errors;
	}
}
