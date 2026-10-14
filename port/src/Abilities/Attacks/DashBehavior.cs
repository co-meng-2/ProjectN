using Gamesmiths.Forge.Abilities;
using VampireSurvivor.Components;

namespace VampireSurvivor.Abilities;

/// <summary>One dash: starts the mover's dash, then ends the instance when the duration has passed.</summary>
public sealed class DashBehavior(DashDef def) : IAbilityBehavior
{
	private AbilityBehaviorContext? _context;
	private Mover? _mover;
	private double _remaining;

	public void OnStarted(AbilityBehaviorContext context)
	{
		if (context.Owner is not ActorEntity entity || entity.Context is not { } ctx)
		{
			context.InstanceHandle.End();
			return;
		}

		_context = context;
		_mover = ctx.Mover;
		_remaining = def.Duration;
		context.AbilityHandle.CommitCooldown();
		_mover.Dash(_mover.Direction, def.Speed, def.Duration);
	}

	public void OnUpdate(double deltaTime)
	{
		if (_context is null)
		{
			return;
		}

		_remaining -= deltaTime;
		if (_remaining <= 0.0)
		{
			AbilityBehaviorContext context = _context;
			_context = null;
			context.InstanceHandle.End();
		}
	}

	public void OnEnded(AbilityBehaviorContext context)
	{
		_context = null;
		_mover?.CancelDash();
	}
}
