using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>
/// The player's starting stats, look and abilities for a run, the shop catalog, and the skill-tree bonuses bought
/// outside the run (filled in by ProfileBuilder, never saved in the .tres).
/// </summary>
[GlobalClass]
public partial class PlayerProfile : Resource
{
	/// <summary>Upper bound of <see cref="SlotCount"/>: each slot needs its own cooldown tag (GameplayTags.SlotCooldowns).</summary>
	public const int MaxSlots = 10;

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

	/// <summary>What the shop can roll into the slots.</summary>
	[Export]
	public Godot.Collections.Array<AbilityDef> Abilities { get; set; } = [];

	/// <summary>Permanent bonuses from the skill tree, applied by Player.Setup.</summary>
	public List<SkillBonus> SkillBonuses { get; set; } = [];

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

		if (SlotCount < 1 || SlotCount > MaxSlots)
		{
			errors.Add($"PlayerProfile: slot_count must be 1..{MaxSlots}");
		}

		foreach (AbilityDef? ability in Abilities)
		{
			if (ability is null)
			{
				errors.Add("PlayerProfile: null catalog ability");
			}
			else
			{
				errors.AddRange(ability.Validate());
			}
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

		foreach (SkillBonus? bonus in SkillBonuses)
		{
			if (bonus?.Passive is null)
			{
				errors.Add("PlayerProfile: skill bonus without a passive");
				continue;
			}

			int levels = bonus.Passive.ByLevel?.PointCount ?? 0;
			if (bonus.Level < 1 || bonus.Level > levels)
			{
				errors.Add($"PlayerProfile: skill bonus level {bonus.Level} is outside 1..{levels}");
			}

			errors.AddRange(bonus.Passive.Validate(levels));
		}

		return errors;
	}
}
