using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>What an enemy may drop on death.</summary>
public partial class DropTable : Resource
{
	[Export]
	public Godot.Collections.Array<DropEntry> Entries { get; set; } = [];

	public List<DropRoll> Roll(RunRng rng)
	{
		var rolls = new List<DropRoll>();
		foreach (DropEntry? entry in Entries)
		{
			if (entry is null || !rng.Chance(entry.Chance))
			{
				continue;
			}

			rolls.Add(new DropRoll
			{
				Kind = entry.Kind,
				Icon = entry.Icon,
				Amount = rng.Int(entry.MinAmount, entry.MaxAmount),
			});
		}

		return rolls;
	}

	public List<string> Validate()
	{
		var errors = new List<string>();
		foreach (DropEntry? entry in Entries)
		{
			if (entry is null)
			{
				errors.Add("DropTable: null entry");
			}
			else
			{
				errors.AddRange(entry.Validate());
			}
		}

		return errors;
	}
}
