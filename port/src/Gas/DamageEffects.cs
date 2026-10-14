using Gamesmiths.Forge.Core;
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Duration;
using VampireSurvivor.Gas.Executions;

namespace VampireSurvivor.Gas;

/// <summary>The single place that assembles a hit, so every hit carries the same context.</summary>
public static class DamageEffects
{
	private static EffectData? _data;

	/// <summary>
	/// Damages <paramref name="target"/> by <paramref name="amount"/> display units times the source's Attack.
	/// Zero or negative amounts, targets without vitals and invulnerable targets are ignored: a hit can never heal.
	/// Returns whether the hit was applied (a dodged hit must not count as "struck").
	/// </summary>
	public static bool Hit(IForgeEntity source, IForgeEntity target, float amount)
	{
		if (amount <= 0f || !Stats.TryGet(target, Stats.Health, out _) || IsInvulnerable(target))
		{
			return false;
		}

		_data ??= new EffectData(
			"Damage",
			new DurationData(DurationType.Instant),
			customExecutions: [new DamageExecution()]);
		var effect = new Effect(_data.Value, new EffectOwnership(source, source));
		effect.SetSetByCallerMagnitude(GameplayTags.Request(GameplayTags.DamageAmount), amount);
		target.EffectsManager.ApplyEffect(effect);
		return true;
	}

	private static bool IsInvulnerable(IForgeEntity target)
	{
		return target.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.StateInvulnerable));
	}
}
