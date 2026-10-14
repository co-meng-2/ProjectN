using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Boss;

/// <summary>One burst: fire them all and complete at once.</summary>
public sealed class RadialBurstBehavior(RadialBurstDef def) : PatternBehavior
{
	protected override bool Begin()
	{
		Vector2 origin = Ctx.Actor.GlobalPosition;
		for (int i = 0; i < def.Count; i++)
		{
			Vector2 direction = Vector2.FromAngle(Mathf.Tau * i / def.Count);
			var projectile = def.ProjectileScene!.Instantiate<Projectile>();
			Ctx.ProjectileLayer.AddChild(projectile);
			projectile.GlobalPosition = origin;
			projectile.Launch(def.Projectile!, Entity, def.Damage, direction);
		}

		Complete();
		return true;
	}
}
