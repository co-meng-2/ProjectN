using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;

namespace VampireSurvivor.Abilities;

/// <summary>One cast of a <see cref="BladeSweepDef"/>: a strike around the owner if an enemy is within reach.</summary>
public sealed class BladeSweepBehavior(BladeSweepDef def) : IAbilityBehavior
{
	public void OnStarted(AbilityBehaviorContext context)
	{
		try
		{
			Sweep(context);
		}
		finally
		{
			context.InstanceHandle.End();
		}
	}

	public void OnEnded(AbilityBehaviorContext context)
	{
	}

	private void Sweep(AbilityBehaviorContext context)
	{
		if (context.Owner is not ActorEntity entity || entity.Context is not { } ctx
			|| def.StrikeScene is null || def.Radius is null || def.Damage is null)
		{
			return;
		}

		Vector2 origin = ctx.Actor.GlobalPosition;
		int level = context.Level;
		float reach = def.Radius.Sample(level);
		Node2D? target = ctx.Targets.NearestEnemy(origin, reach);
		if (target is null)
		{
			return; // a miss costs nothing: no cooldown
		}

		context.AbilityHandle.CommitCooldown();
		var strike = def.StrikeScene.Instantiate<AreaStrike>();
		ctx.ProjectileLayer.AddChild(strike);
		strike.GlobalPosition = origin;
		strike.Strike(entity, def.Damage.Sample(level), reach, def.StrikeSeconds);
		ctx.RequestAnimation(AnimNames.Attack, (target.GlobalPosition - origin).Normalized());
	}
}
