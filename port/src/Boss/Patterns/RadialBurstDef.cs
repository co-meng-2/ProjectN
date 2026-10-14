using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Boss;

/// <summary>
/// Radial burst: <see cref="Count"/> projectiles fired evenly around the boss. The projectile def must hit allies (the
/// player and the building); speed and lifetime live in that def.
/// </summary>
public partial class RadialBurstDef : PatternDef
{
	[Export]
	public PackedScene? ProjectileScene { get; set; }

	[Export]
	public ProjectileDef? Projectile { get; set; }

	[Export]
	public int Count { get; set; }

	[Export]
	public float Damage { get; set; }

	public override IAbilityBehavior CreateBehavior() => new RadialBurstBehavior(this);

	public override List<string> Validate()
	{
		List<string> errors = base.Validate();
		if (ProjectileScene is null || Projectile is null)
		{
			errors.Add("Radial burst: projectile_scene and projectile are required");
		}
		else
		{
			if (!PatternScenes.RootIs<Projectile>(ProjectileScene))
			{
				errors.Add("Radial burst: projectile_scene root is not a Projectile");
			}

			if (Projectile.Hits != ProjectileTeam.Allies)
			{
				errors.Add("Radial burst: projectile must hit allies");
			}
		}

		if (Count < 1)
		{
			errors.Add("Radial burst: count must be >= 1");
		}

		if (Damage < 0f)
		{
			errors.Add("Radial burst: damage must be >= 0");
		}

		return errors;
	}
}
