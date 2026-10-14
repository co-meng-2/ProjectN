using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Abilities;

/// <summary>A radial melee hit around the owner whenever an enemy is within reach. Radius and damage are curves over the level.</summary>
public partial class BladeSweepDef : ActiveAbilityDef
{
	/// <summary>The strike spawned on the projectile layer (an AreaStrike scene).</summary>
	[Export]
	public PackedScene? StrikeScene { get; set; }

	[Export]
	public Curve? Radius { get; set; }

	/// <summary>Damage over the level, in display units.</summary>
	[Export]
	public Curve? Damage { get; set; }

	/// <summary>How long the strike area stays active; it must outlive one physics step so overlaps are reported.</summary>
	[Export]
	public float StrikeSeconds { get; set; }

	public override IAbilityBehavior CreateBehavior() => new BladeSweepBehavior(this);

	public override List<string> Validate()
	{
		List<string> errors = base.Validate();
		if (StrikeScene is null)
		{
			errors.Add("Blade Sweep: strike scene is required");
		}

		if (Radius is null || Damage is null)
		{
			errors.Add("Blade Sweep: radius and damage curves are required");
		}

		if (StrikeSeconds <= 0f)
		{
			errors.Add("Blade Sweep: strike seconds must be > 0");
		}

		return errors;
	}
}
