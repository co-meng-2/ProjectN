using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Duration;
using Gamesmiths.Forge.Effects.Magnitudes;
using Gamesmiths.Forge.Effects.Modifiers;

namespace VampireSurvivor.Gas;

/// <summary>Instant effects that set an attribute's base value (Forge changes attributes only through effects).</summary>
public static class StatEffects
{
	/// <summary>Raw values up to 2^24 are exact as floats; the attribute limits keep every stat below that.</summary>
	public static EffectData SetBase(string key, int raw)
	{
		var magnitude = new ModifierMagnitude(MagnitudeCalculationType.ScalableFloat, new ScalableFloat(raw));
		return new EffectData(
			$"SetBase:{key}",
			new DurationData(DurationType.Instant),
			[new Modifier(key, ModifierOperation.Override, magnitude)]);
	}
}
