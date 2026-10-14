using System;
using Gamesmiths.Forge.Attributes;
using Gamesmiths.Forge.Core;
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Calculator;
using Gamesmiths.Forge.Effects.Magnitudes;
using Gamesmiths.Forge.Effects.Modifiers;
using Gamesmiths.Forge.Tags;

namespace VampireSurvivor.Gas.Executions;

/// <summary>
/// Damage = the amount injected by the caller (set-by-caller "damage.amount", display units) x the source's Attack
/// multiplier, as a negative FlatBonus on the target's Health. All arithmetic is in raw integers and truncates once.
/// </summary>
public sealed class DamageExecution : CustomExecution
{
	public DamageExecution()
	{
		AttributesToCapture.Add(new AttributeCaptureDefinition(Stats.Attack, AttributeCaptureSource.Source));
	}

	public override ModifierEvaluatedData[] EvaluateExecution(
		Effect effect,
		IForgeEntity target,
		EffectEvaluatedData? effectEvaluatedData)
	{
		if (!Stats.TryGet(target, Stats.Health, out EntityAttribute? health))
		{
			return [];
		}

		Tag amountTag = GameplayTags.Request(GameplayTags.DamageAmount);
		float amount = effect.DataTag.TryGetValue(amountTag, out float value) ? value : 0f;
		if (amount <= 0f)
		{
			return [];
		}

		int attackRaw = CaptureAttributeMagnitude(AttributesToCapture[0], effect, target, effectEvaluatedData);
		long amountRaw = (long)MathF.Round(amount * Stats.Scale(Stats.HealthDecimals));
		long damageRaw = amountRaw * attackRaw / Stats.Scale(Stats.AttackDecimals);

		return [new ModifierEvaluatedData(health, ModifierOperation.FlatBonus, -damageRaw)];
	}
}
