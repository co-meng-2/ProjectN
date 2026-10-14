using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Gamesmiths.Forge.Abilities;
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Godot.Core;
using Gamesmiths.Forge.Tags;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Gas;

public static class AbilityFactory
{
	private static readonly ConditionalWeakTable<ActiveAbilityDef, StrongBox<AbilityData>> Cache = [];

	/// <summary>The def's AbilityData, built once per def instance (Forge compares ability data by value).</summary>
	public static AbilityData Build(ActiveAbilityDef def)
	{
		if (Cache.TryGetValue(def, out StrongBox<AbilityData>? cached))
		{
			return cached.Value;
		}

		EffectData[]? cooldowns = def.Cooldown is null
			? null
			: [CooldownEffects.Build(GameplayTags.CooldownFor(def.Tag), def.Cooldown)];
		TagContainer? owned = def.OwnedTags.Length == 0
			? null
			: new TagContainer(ForgeManagers.Instance.TagsManager, def.OwnedTags.Select(GameplayTags.Request).ToHashSet());
		var data = new AbilityData(
			def.Tag,
			cooldownEffects: cooldowns,
			abilityTags: GameplayTags.ContainerOf(def.Tag),
			activationOwnedTags: owned,
			behaviorFactory: def.CreateBehavior);
		Cache.Add(def, new StrongBox<AbilityData>(data));
		return data;
	}

	/// <summary>Def validation plus the checks that need the Forge tag registry.</summary>
	public static List<string> Validate(ActiveAbilityDef def)
	{
		List<string> errors = def.Validate();
		foreach (string tag in new[] { def.Tag }.Concat(def.OwnedTags).Where(t => t.Length > 0))
		{
			try
			{
				GameplayTags.Request(tag);
			}
			catch (System.ArgumentException e)
			{
				errors.Add($"{def.GetType().Name}: {e.Message}");
			}
		}

		return errors;
	}
}
