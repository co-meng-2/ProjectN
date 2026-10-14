using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Boss;

/// <summary>One summon: call the stage's summon for every ring point and complete.</summary>
public sealed class SummonBehavior(SummonDef def) : PatternBehavior
{
	protected override bool Begin()
	{
		if (Ctx.Summon is null)
		{
			return false;
		}

		Vector2 origin = Ctx.Actor.GlobalPosition;
		for (int i = 0; i < def.Count; i++)
		{
			Ctx.Summon(def.Enemy!, SpawnGeometry.RingPoint(origin, def.Radius, Mathf.Tau * i / def.Count));
		}

		Complete();
		return true;
	}
}
