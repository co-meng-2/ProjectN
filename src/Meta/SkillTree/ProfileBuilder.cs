using System;
using System.Collections.Generic;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Meta.SkillTree;

/// <summary>
/// Derives the PlayerProfile for a run from the base profile and the purchased skill levels. Pure: the base profile is
/// never modified, and the stage only ever sees the result.
/// </summary>
public static class ProfileBuilder
{
	/// <summary>
	/// Levels above a node's max level are clamped; ids that are not in the tree are ignored (they stay in the save).
	/// Slot bonuses stop at <see cref="PlayerProfile.MaxSlots"/>. Fields are copied explicitly: when PlayerProfile gains
	/// a field, copy it here too.
	/// </summary>
	public static PlayerProfile Build(PlayerProfile baseProfile, SkillTreeDef tree, IReadOnlyDictionary<string, int> levels)
	{
		var result = new PlayerProfile
		{
			Stats = baseProfile.Stats,
			Visual = baseProfile.Visual,
			SlotCount = baseProfile.SlotCount,
			InnateAbilities = [.. baseProfile.InnateAbilities],
			Abilities = [.. baseProfile.Abilities],
			SkillBonuses = [.. baseProfile.SkillBonuses],
		};
		foreach (SkillNodeDef? node in tree.Nodes)
		{
			if (node is null)
			{
				continue;
			}

			int level = Math.Min(levels.GetValueOrDefault(node.Id.ToString()), node.MaxLevel);
			if (level < 1)
			{
				continue;
			}

			if (node.Passive is not null)
			{
				result.SkillBonuses.Add(new SkillBonus(node.Passive, level));
			}

			result.SlotCount += node.SlotBonus * level;
		}

		result.SlotCount = Math.Min(result.SlotCount, Math.Max(PlayerProfile.MaxSlots, baseProfile.SlotCount));
		return result;
	}
}
