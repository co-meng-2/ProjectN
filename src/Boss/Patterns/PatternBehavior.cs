using Gamesmiths.Forge.Abilities;
using VampireSurvivor.Components;

namespace VampireSurvivor.Boss;

/// <summary>
/// Base of one run of a boss pattern. <see cref="Begin"/> starts it (false = cannot run now: it ends at once),
/// <see cref="Advance"/> moves it on every entity tick while it runs, <see cref="Complete"/> ends it. When the instance
/// ends any other way (boss death, cleanup, a cancel) <see cref="Abort"/> removes what the attack placed.
/// </summary>
public abstract class PatternBehavior : IAbilityBehavior
{
	private AbilityBehaviorContext? _context;

	/// <summary>The boss's entity: the source of the attack's damage.</summary>
	protected ActorEntity Entity { get; private set; } = null!;

	/// <summary>What the boss offers its patterns: the actor, its mover, the targets, the projectile layer, the summon.</summary>
	protected AbilityContext Ctx { get; private set; } = null!;

	protected bool IsRunning { get; private set; }

	public void OnStarted(AbilityBehaviorContext context)
	{
		_context = context;
		if (context.Owner is not ActorEntity { Context: { } ctx } entity)
		{
			context.InstanceHandle.End();
			return;
		}

		Entity = entity;
		Ctx = ctx;
		IsRunning = true;
		if (!Begin())
		{
			IsRunning = false;
			context.InstanceHandle.End();
		}
	}

	public void OnUpdate(double deltaTime)
	{
		if (IsRunning)
		{
			Advance((float)deltaTime);
		}
	}

	public void OnEnded(AbilityBehaviorContext context)
	{
		if (!IsRunning)
		{
			return; // finished through Complete (or refused): nothing to undo
		}

		IsRunning = false;
		Abort();
	}

	/// <summary>Starts the attack; false when it cannot run now (no target, no summon).</summary>
	protected abstract bool Begin();

	protected virtual void Advance(float delta)
	{
	}

	/// <summary>Removes whatever the attack placed (telegraphs, strikes) when it is interrupted.</summary>
	protected virtual void Abort()
	{
	}

	protected void Complete()
	{
		if (!IsRunning)
		{
			return;
		}

		IsRunning = false;
		_context!.InstanceHandle.End();
	}
}
