using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>Which side a projectile damages: the player's arrows hit enemies; boss bolts hit allies (player and building).</summary>
public enum ProjectileTeam
{
	Enemies,
	Allies,
}

/// <summary>Data for a straight-flying projectile.</summary>
public partial class ProjectileDef : Resource
{
	[Export]
	public Texture2D? Texture { get; set; }

	[Export]
	public float Speed { get; set; } = 400f;

	[Export]
	public float Lifetime { get; set; } = 0.7f;

	/// <summary>Extra enemies the projectile passes through (0 = stops at the first hit).</summary>
	[Export]
	public int Pierce { get; set; }

	[Export]
	public float HitRadius { get; set; } = 6f;

	[Export]
	public ProjectileTeam Hits { get; set; } = ProjectileTeam.Enemies;

	public int HitMask() => Hits == ProjectileTeam.Allies ? PhysicsLayers.AllyHurt : PhysicsLayers.EnemyHurt;

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (Texture is null)
		{
			errors.Add("ProjectileDef: texture is null");
		}

		if (Speed <= 0f || Lifetime <= 0f || HitRadius <= 0f)
		{
			errors.Add("ProjectileDef: speed, lifetime and hit_radius must be > 0");
		}

		return errors;
	}
}
