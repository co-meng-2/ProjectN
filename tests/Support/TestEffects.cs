using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Components;
using Gamesmiths.Forge.Effects.Duration;
using Gamesmiths.Forge.Effects.Magnitudes;
using Gamesmiths.Forge.Effects.Modifiers;
using VampireSurvivor.Components;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Tests.Support;

public static class TestEffects
{
	public static void Apply(ActorEntity entity, EffectData data) => entity.Apply(data);

	/// <summary>An instant FlatBonus of <paramref name="raw"/> (raw attribute units) on one attribute.</summary>
	public static void Flat(ActorEntity entity, string key, int raw)
	{
		var magnitude = new ModifierMagnitude(MagnitudeCalculationType.ScalableFloat, new ScalableFloat(raw));
		var data = new EffectData(
			$"TestFlat:{key}",
			new DurationData(DurationType.Instant),
			[new Modifier(key, ModifierOperation.FlatBonus, magnitude)]);
		entity.Apply(data);
	}

	/// <summary>An INFINITE effect that adds <paramref name="tag"/> to the entity; remove the returned handle to take it off.</summary>
	public static ActiveEffectHandle? GrantTag(ActorEntity entity, string tag)
	{
		var data = new EffectData(
			$"TestTag:{tag}",
			new DurationData(DurationType.Infinite),
			effectComponents: [new ModifierTagsEffectComponent(GameplayTags.ContainerOf(tag))]);
		return entity.EffectsManager.ApplyEffect(new Effect(data, new EffectOwnership(entity, entity)));
	}
}
