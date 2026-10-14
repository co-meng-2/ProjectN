using System.Collections.Generic;
using System.Linq;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Abilities;

/// <summary>Which abilities a grade can roll: every def whose <see cref="AbilityDef.MinGrade"/> is at or below that grade.</summary>
public sealed class AbilityPool(IEnumerable<AbilityDef?> defs)
{
	private readonly List<AbilityDef> _defs = [.. defs.OfType<AbilityDef>()];

	public List<AbilityDef> DefsFor(Grade grade) => grade.IsRollable() ? [.. _defs.Where(d => d.MinGrade <= grade)] : [];

	/// <summary>
	/// A random def of the grade's pool, or null when it is empty. With <paramref name="exclude"/> a different def is
	/// preferred, but the excluded one is returned when it is the only candidate.
	/// </summary>
	public AbilityDef? Roll(Grade grade, RunRng rng, AbilityDef? exclude = null)
	{
		List<AbilityDef> candidates = DefsFor(grade);
		if (exclude is not null && candidates.Count > 1)
		{
			candidates.Remove(exclude);
		}

		return rng.Pick(candidates);
	}
}
