using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Gamesmiths.Forge.Godot.Resources.Abilities;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>
/// An ability an actor performs (attack, dash, boss pattern). Authored as a resource with its numbers as exports;
/// the behavior is built from it on demand. Forge's AbilityData is assembled from this in Gas/AbilityFactory.
/// </summary>
[Tool]
public abstract partial class ActiveAbilityDef : ForgeAbilityBehavior
{
	/// <summary>The ability tag, for example "ability.volley". Must be registered in GameplayTags.</summary>
	[Export]
	public string Tag { get; set; } = string.Empty;

	/// <summary>Seconds over the ability level (x = 1..6). Null means the ability has no cooldown.</summary>
	[Export]
	public Curve? Cooldown { get; set; }

	/// <summary>Tags the owner carries while an instance runs (for example "state.invulnerable").</summary>
	[Export]
	public string[] OwnedTags { get; set; } = [];

	/// <summary>When true the owner's <c>AutoCaster</c> keeps trying to activate it (attacks); false for on-demand abilities (dash).</summary>
	[Export]
	public bool AutoFire { get; set; }

	public abstract IAbilityBehavior CreateBehavior();

	public override IAbilityBehavior GetBehavior() => CreateBehavior();

	public virtual List<string> Validate()
	{
		var errors = new List<string>();
		if (string.IsNullOrEmpty(Tag))
		{
			errors.Add($"{GetType().Name}: Tag is empty");
		}

		return errors;
	}
}
