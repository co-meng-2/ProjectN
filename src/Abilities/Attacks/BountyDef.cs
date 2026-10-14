using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Abilities;

/// <summary>Pays gold into the run's wallet every time the owner is credited with a kill (<c>event.kill</c>).</summary>
[GlobalClass]
public partial class BountyDef : ActiveAbilityDef
{
	public BountyDef()
	{
		TriggerEventTag = GameplayTags.EventKill;
	}

	/// <summary>Gold per kill over the level.</summary>
	[Export]
	public Curve? GoldPerKill { get; set; }

	public override IAbilityBehavior CreateBehavior() => new BountyBehavior(this);

	public override List<string> Validate()
	{
		List<string> errors = base.Validate();
		if (GoldPerKill is null)
		{
			errors.Add("Bounty: gold per kill curve is null");
		}

		return errors;
	}
}
