using System.Collections.Generic;
using System.Linq;
using Gamesmiths.Forge.Abilities;
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Godot.Core;
using Gamesmiths.Forge.Tags;
using Godot;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Gas;

public static class AbilityFactory
{
	/// <summary>
	/// A new AbilityData on every call, on purpose: Forge identifies an ability by its data, so granting equal data twice
	/// would merge into one ability at the first grant's level. One data per grant gives each grant its own handle and level.
	/// </summary>
	public static AbilityData Build(ActiveAbilityDef def)
	{
		EffectData[]? cooldowns = def.Cooldown is null
			? null
			: [CooldownEffects.Build(GameplayTags.CooldownFor(def.Tag), def.Cooldown)];
		TagContainer? owned = def.OwnedTags.Length == 0
			? null
			: new TagContainer(ForgeManagers.Instance.TagsManager, def.OwnedTags.Select(GameplayTags.Request).ToHashSet());
		AbilityTriggerData? trigger = def.TriggerEventTag.Length == 0
			? null
			: AbilityTriggerData.ForEvent<Node>(GameplayTags.Request(def.TriggerEventTag), 0);
		var data = new AbilityData(
			def.Tag,
			cooldownEffects: cooldowns,
			abilityTags: GameplayTags.ContainerOf(def.Tag),
			abilityTriggerData: trigger,
			activationOwnedTags: owned,
			behaviorFactory: def.CreateBehavior);
		return data;
	}

	/// <summary>Def validation plus the checks that need the Forge tag registry.</summary>
	public static List<string> Validate(ActiveAbilityDef def)
	{
		List<string> errors = def.Validate();
		foreach (string tag in new[] { def.Tag, def.TriggerEventTag }.Concat(def.OwnedTags).Where(t => t.Length > 0))
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
