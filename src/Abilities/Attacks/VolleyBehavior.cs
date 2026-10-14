using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;

namespace VampireSurvivor.Abilities;

/// <summary>One cast of a <see cref="VolleyDef"/>. It ends in the same call: shooting is instantaneous.</summary>
public sealed class VolleyBehavior(VolleyDef def) : IAbilityBehavior
{
	public void OnStarted(AbilityBehaviorContext context)
	{
		try
		{
			Fire(context);
		}
		finally
		{
			context.InstanceHandle.End();
		}
	}

	public void OnEnded(AbilityBehaviorContext context)
	{
	}

	private void Fire(AbilityBehaviorContext context)
	{
		if (context.Owner is not ActorEntity entity || entity.Context is not { } ctx
			|| def.ArrowCount is null || def.Damage is null || def.ProjectileScene is null || def.Projectile is null)
		{
			return;
		}

		Vector2 origin = ctx.Actor.GlobalPosition;
		int level = context.Level;
		int count = Mathf.RoundToInt(def.ArrowCount.Sample(level));
		List<Node2D> targets = ctx.Targets.NearestEnemies(origin, def.AttackRange, count);
		if (targets.Count == 0)
		{
			return; // a miss costs nothing: no cooldown
		}

		context.AbilityHandle.CommitCooldown();
		float amount = def.Damage.Sample(level);
		foreach (Node2D target in targets)
		{
			Vector2 direction = (target.GlobalPosition - origin).Normalized();
			var projectile = def.ProjectileScene.Instantiate<Projectile>();
			ctx.ProjectileLayer.AddChild(projectile);
			projectile.GlobalPosition = origin;
			projectile.Launch(def.Projectile, entity, amount, direction);
		}

		ctx.RequestAnimation(AnimNames.Attack, (targets[0].GlobalPosition - origin).Normalized());
	}
}
