using System;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;

namespace VampireSurvivor.Abilities;

/// <summary>One payment: started by the kill event, credits the wallet and ends.</summary>
public sealed class BountyBehavior(BountyDef def) : IAbilityBehavior<Node>
{
	public void OnStarted(AbilityBehaviorContext context, Node data) => Pay(context);

	public void OnStarted(AbilityBehaviorContext context) => Pay(context);

	public void OnEnded(AbilityBehaviorContext context)
	{
	}

	private void Pay(AbilityBehaviorContext context)
	{
		try
		{
			if (context.Owner is ActorEntity { Context.Wallet: { } wallet } && def.GoldPerKill is not null)
			{
				int gold = (int)Math.Round(def.GoldPerKill.Sample(context.Level), MidpointRounding.AwayFromZero);
				wallet.Add(CurrencyKind.Gold, gold);
			}
		}
		finally
		{
			context.InstanceHandle.End();
		}
	}
}
