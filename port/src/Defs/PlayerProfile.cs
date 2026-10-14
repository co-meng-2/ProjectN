using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>
/// The player's starting stats, look and abilities for a run. The shop catalog (slice 3) and the skill-tree bonuses (slice 5)
/// are added by later slices.
/// </summary>
public partial class PlayerProfile : Resource
{
	[Export]
	public StatBlock? Stats { get; set; }

	[Export]
	public VisualDef? Visual { get; set; }

	/// <summary>Ability slots shown in the HUD.</summary>
	[Export]
	public int SlotCount { get; set; } = 6;

	/// <summary>Abilities the player starts with, granted at level 1 (the arrow, the dash).</summary>
	[Export]
	public Godot.Collections.Array<ActiveAbilityDef> InnateAbilities { get; set; } = [];

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (Stats is null)
		{
			errors.Add("PlayerProfile: stats is null");
		}
		else
		{
			errors.AddRange(Stats.Validate());
		}

		if (Visual is null)
		{
			errors.Add("PlayerProfile: visual is null");
		}
		else
		{
			errors.AddRange(Visual.Validate());
		}

		if (SlotCount < 1)
		{
			errors.Add("PlayerProfile: slot_count must be >= 1");
		}

		foreach (ActiveAbilityDef? ability in InnateAbilities)
		{
			if (ability is null)
			{
				errors.Add("PlayerProfile: null innate ability");
			}
			else
			{
				errors.AddRange(ability.Validate());
			}
		}

		return errors;
	}
}
