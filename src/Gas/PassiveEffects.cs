using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Duration;
using Gamesmiths.Forge.Effects.Magnitudes;
using Gamesmiths.Forge.Effects.Modifiers;
using Gamesmiths.Forge.Godot.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Gas;

public static class PassiveEffects
{
	/// <summary>
	/// An INFINITE effect whose single modifier is scaled by the curve at the effect level. Flat curves are in display
	/// units, so the base value is the stat's scale; Percent curves are fractions, so the base value is 1.
	/// </summary>
	public static EffectData Build(PassiveDef def)
	{
		bool flat = def.Operation == PassiveOperation.Flat;
		float baseValue = flat ? Stats.Scale(Stats.DecimalsOf(def.Stat)) : 1f;
		var magnitude = new ModifierMagnitude(
			MagnitudeCalculationType.ScalableFloat,
			new ScalableFloat(baseValue, new ForgeCurve(def.ByLevel)));
		var modifier = new Modifier(
			Stats.KeyOf(def.Stat),
			flat ? ModifierOperation.FlatBonus : ModifierOperation.PercentBonus,
			magnitude);
		return new EffectData($"Passive:{def.Stat}", new DurationData(DurationType.Infinite), [modifier]);
	}
}
