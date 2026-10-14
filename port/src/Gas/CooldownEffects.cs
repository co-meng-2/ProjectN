using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Components;
using Gamesmiths.Forge.Effects.Duration;
using Gamesmiths.Forge.Effects.Magnitudes;
using Gamesmiths.Forge.Godot.Core;
using Godot;

namespace VampireSurvivor.Gas;

public static class CooldownEffects
{
	/// <summary>
	/// A timed effect that grants <paramref name="cooldownTag"/> for the curve's value at the ability level (seconds).
	/// Forge blocks activation while the owner carries the tag.
	/// </summary>
	public static EffectData Build(string cooldownTag, Curve seconds)
	{
		var duration = new ModifierMagnitude(
			MagnitudeCalculationType.ScalableFloat,
			new ScalableFloat(1f, new ForgeCurve(seconds)));
		return new EffectData(
			$"Cooldown:{cooldownTag}",
			new DurationData(DurationType.HasDuration, duration),
			effectComponents: [new ModifierTagsEffectComponent(GameplayTags.ContainerOf(cooldownTag))]);
	}
}
