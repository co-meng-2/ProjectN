using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>
/// One shop entry the player can hold in a slot: a passive (a stat modifier over the grade level), an active ability
/// (an attack), or both. The grade maps to the level both are applied at (Common 1 ... Revealed 6).
/// </summary>
public partial class AbilityDef : Resource
{
	[Export]
	public StringName Id { get; set; } = string.Empty;

	[Export]
	public string DisplayName { get; set; } = string.Empty;

	[Export(PropertyHint.MultilineText)]
	public string Description { get; set; } = string.Empty;

	[Export]
	public Texture2D? Icon { get; set; }

	/// <summary>Optional icon of the revealed form; without it the view tints <see cref="Icon"/>.</summary>
	[Export]
	public Texture2D? RevealedIcon { get; set; }

	/// <summary>Lowest grade whose pool offers this ability.</summary>
	[Export]
	public Grade MinGrade { get; set; } = Grade.Common;

	[Export]
	public PassiveDef? Passive { get; set; }

	[Export]
	public ActiveAbilityDef? Active { get; set; }

	public Texture2D? IconFor(Grade grade) => grade == Grade.Revealed && RevealedIcon is not null ? RevealedIcon : Icon;

	public List<string> Validate()
	{
		var errors = new List<string>();
		string id = Id.ToString();
		if (id.Length == 0)
		{
			errors.Add("AbilityDef: id is empty");
		}

		if (DisplayName.Length == 0)
		{
			errors.Add($"AbilityDef '{id}': display_name is empty");
		}

		if (Icon is null)
		{
			errors.Add($"AbilityDef '{id}': icon is null");
		}

		if (!MinGrade.IsRollable())
		{
			errors.Add($"AbilityDef '{id}': min_grade must be Common..Legendary");
		}

		if (Passive is null && Active is null)
		{
			errors.Add($"AbilityDef '{id}': needs a passive and/or an active ability");
		}

		if (Passive is not null)
		{
			errors.AddRange(Passive.Validate());
		}

		if (Active is not null)
		{
			errors.AddRange(Active.Validate());
		}

		return errors;
	}
}
