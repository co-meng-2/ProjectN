using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Abilities;

/// <summary>A short burst of speed. Invulnerability comes from the owned tag <c>state.invulnerable</c> while it runs.</summary>
public partial class DashDef : ActiveAbilityDef
{
	public DashDef()
	{
		OwnedTags = [GameplayTags.StateInvulnerable];
	}

	[Export]
	public float Speed { get; set; } = 480f;

	[Export]
	public float Duration { get; set; } = 0.18f;

	public override IAbilityBehavior CreateBehavior() => new DashBehavior(this);

	public override List<string> Validate()
	{
		List<string> errors = base.Validate();
		if (Speed <= 0f || Duration <= 0f)
		{
			errors.Add("Dash: speed and duration must be > 0");
		}

		return errors;
	}
}
