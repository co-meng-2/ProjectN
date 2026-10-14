using Gamesmiths.Forge.Abilities;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Tests.Support;

/// <summary>An ability that counts its starts and ends; optionally it stays active until cancelled.</summary>
public sealed partial class ProbeAbilityDef : ActiveAbilityDef
{
	public int Starts { get; set; }

	public int Ends { get; set; }

	public bool StayActive { get; set; }

	/// <summary>The ability level of the latest start.</summary>
	public int LastLevel { get; set; }

	public override IAbilityBehavior CreateBehavior() => new ProbeBehavior(this);

	private sealed class ProbeBehavior(ProbeAbilityDef owner) : IAbilityBehavior
	{
		public void OnStarted(AbilityBehaviorContext context)
		{
			owner.Starts++;
			owner.LastLevel = context.Level;
			context.AbilityHandle.CommitCooldown();
			if (!owner.StayActive)
			{
				context.InstanceHandle.End();
			}
		}

		public void OnEnded(AbilityBehaviorContext context)
		{
			owner.Ends++;
		}
	}
}
