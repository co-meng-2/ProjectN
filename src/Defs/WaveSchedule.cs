using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>Ordered surges of a stage. Entries may be authored in any order.</summary>
[GlobalClass]
public partial class WaveSchedule : Resource
{
	[Export]
	public Godot.Collections.Array<WaveEntry> Entries { get; set; } = [];

	public List<WaveEntry> SortedEntries()
	{
		return [.. Entries.Where(e => e is not null).OrderBy(e => e.Time)];
	}

	public float[] Times() => [.. SortedEntries().Select(e => e.Time)];

	public List<string> ValidateAgainst(float duration)
	{
		var errors = new List<string>();
		foreach (WaveEntry? entry in Entries)
		{
			if (entry is null)
			{
				errors.Add("WaveSchedule: null entry");
				continue;
			}

			if (entry.Enemy is null)
			{
				errors.Add($"WaveEntry '{entry.Label}': enemy is null");
			}
			else
			{
				errors.AddRange(entry.Enemy.Validate());
			}

			if (entry.Count <= 0)
			{
				errors.Add($"WaveEntry '{entry.Label}': count must be > 0");
			}

			if (entry.Time < 0f || entry.Time >= duration)
			{
				errors.Add($"WaveEntry '{entry.Label}': time {entry.Time} is outside [0, stage duration {duration})");
			}
		}

		return errors;
	}
}
