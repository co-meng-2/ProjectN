using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>
/// One skill-tree node. Buying a level costs gem (<c>Costs[level]</c> for the next level). <see cref="Passive"/> is
/// authored like an ability passive but its curve has one point per purchasable level (x = 1..MaxLevel).
/// <see cref="SlotBonus"/> (per level) raises the player's ability slot count, which is not a Forge attribute.
/// </summary>
public partial class SkillNodeDef : Resource
{
	[Export]
	public StringName Id { get; set; } = string.Empty;

	[Export]
	public string DisplayName { get; set; } = string.Empty;

	[Export(PropertyHint.MultilineText)]
	public string Description { get; set; } = string.Empty;

	/// <summary>Ids of nodes that must have level >= 1 first. Only nodes listed earlier in the tree.</summary>
	[Export]
	public Godot.Collections.Array<StringName> Requires { get; set; } = [];

	[Export]
	public int MaxLevel { get; set; } = 1;

	/// <summary>Gem cost of each level: Costs[0] buys level 1.</summary>
	[Export]
	public int[] Costs { get; set; } = [];

	[Export]
	public PassiveDef? Passive { get; set; }

	[Export]
	public int SlotBonus { get; set; }

	public List<string> Validate()
	{
		var errors = new List<string>();
		string id = Id.ToString();
		if (id.Length == 0)
		{
			errors.Add("SkillNodeDef: id is empty");
		}

		if (DisplayName.Length == 0)
		{
			errors.Add($"SkillNodeDef '{id}': display_name is empty");
		}

		if (MaxLevel < 1)
		{
			errors.Add($"SkillNodeDef '{id}': max_level must be >= 1");
		}
		else if (Costs.Length != MaxLevel)
		{
			errors.Add($"SkillNodeDef '{id}': costs needs one entry per level ({MaxLevel})");
		}

		foreach (int cost in Costs)
		{
			if (cost <= 0)
			{
				errors.Add($"SkillNodeDef '{id}': every cost must be > 0");
				break;
			}
		}

		if (SlotBonus < 0)
		{
			errors.Add($"SkillNodeDef '{id}': slot_bonus must be >= 0");
		}

		if (Passive is null && SlotBonus <= 0)
		{
			errors.Add($"SkillNodeDef '{id}': needs a passive and/or a slot_bonus");
		}

		if (Requires.Contains(Id))
		{
			errors.Add($"SkillNodeDef '{id}': cannot require itself");
		}

		if (Passive is not null && MaxLevel >= 1)
		{
			foreach (string error in Passive.Validate(MaxLevel))
			{
				errors.Add($"SkillNodeDef '{id}': {error}");
			}
		}

		return errors;
	}
}
