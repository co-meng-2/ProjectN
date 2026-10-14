using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Tests.Support;

/// <summary>An ability started by a gameplay event (<see cref="ActiveAbilityDef.TriggerEventTag"/>); it records each payload and level.</summary>
public sealed partial class EventProbeAbilityDef : ActiveAbilityDef
{
	public EventProbeAbilityDef()
	{
		Tag = GameplayTags.AbilityProbe;
		TriggerEventTag = GameplayTags.EventKill;
	}

	public List<Node?> Payloads { get; } = [];

	public List<int> Levels { get; } = [];

	public override IAbilityBehavior CreateBehavior() => new Behavior(this);

	private sealed class Behavior(EventProbeAbilityDef owner) : IAbilityBehavior<Node>
	{
		public void OnStarted(AbilityBehaviorContext context, Node data)
		{
			owner.Payloads.Add(data);
			owner.Levels.Add(context.Level);
			context.InstanceHandle.End();
		}

		public void OnStarted(AbilityBehaviorContext context)
		{
			context.InstanceHandle.End();
		}

		public void OnEnded(AbilityBehaviorContext context)
		{
		}
	}
}
