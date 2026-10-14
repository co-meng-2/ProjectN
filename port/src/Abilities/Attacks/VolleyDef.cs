using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Abilities;

/// <summary>
/// Fires one arrow at each of the N nearest enemies in range. N and the damage are curves over the ability level.
/// The player's innate arrow is a VolleyDef whose arrow count is 1 at every level.
/// </summary>
public partial class VolleyDef : ActiveAbilityDef
{
	[Export]
	public PackedScene? ProjectileScene { get; set; }

	[Export]
	public ProjectileDef? Projectile { get; set; }

	/// <summary>Arrows per cast over the ability level (x = 1..6).</summary>
	[Export]
	public Curve? ArrowCount { get; set; }

	/// <summary>Damage of each arrow over the ability level, in display units.</summary>
	[Export]
	public Curve? Damage { get; set; }

	[Export]
	public float AttackRange { get; set; }

	public override IAbilityBehavior CreateBehavior() => new VolleyBehavior(this);

	public override List<string> Validate()
	{
		List<string> errors = base.Validate();
		if (ProjectileScene is null || Projectile is null)
		{
			errors.Add("Volley: projectile scene and projectile def are required");
		}
		else
		{
			errors.AddRange(Projectile.Validate());
		}

		if (ArrowCount is null || Damage is null)
		{
			errors.Add("Volley: arrow count and damage curves are required");
		}

		if (AttackRange <= 0f)
		{
			errors.Add("Volley: attack range must be > 0");
		}

		return errors;
	}
}
