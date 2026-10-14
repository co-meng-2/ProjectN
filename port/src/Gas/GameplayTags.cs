using System;
using System.Collections.Generic;
using System.Linq;
using Gamesmiths.Forge.Godot.Core;
using Gamesmiths.Forge.Tags;

namespace VampireSurvivor.Gas;

/// <summary>
/// The only place tag names are spelled. <see cref="All"/> is the source from which tools/sync_tags writes
/// addons/forge/core/forge_data.tres. Tags are lowercase and dotted.
/// </summary>
public static class GameplayTags
{
	public const string AbilityArrow = "ability.arrow";
	public const string AbilityBossBurst = "ability.boss.burst";
	public const string AbilityBossCharge = "ability.boss.charge";
	public const string AbilityBossSlam = "ability.boss.slam";
	public const string AbilityBossSummon = "ability.boss.summon";
	public const string AbilityBounty = "ability.bounty";
	public const string AbilityDash = "ability.dash";
	public const string AbilitySweep = "ability.sweep";
	public const string AbilityVolley = "ability.volley";

	/// <summary>Used by tests only (probe abilities).</summary>
	public const string AbilityProbe = "ability.probe";

	public const string CooldownArrow = "cooldown.arrow";
	public const string CooldownDash = "cooldown.dash";
	public const string CooldownVolley = "cooldown.volley";
	public const string CooldownSweep = "cooldown.sweep";

	/// <summary>Used by tests only (probe abilities).</summary>
	public const string CooldownProbe = "cooldown.probe";

	public const string DamageAmount = "damage.amount";
	public const string EventKill = "event.kill";
	public const string StateCasting = "state.casting";
	public const string StateInvulnerable = "state.invulnerable";

	public static readonly string[] All =
	[
		AbilityArrow, AbilityBossBurst, AbilityBossCharge, AbilityBossSlam, AbilityBossSummon, AbilityBounty,
		AbilityDash, AbilitySweep, AbilityVolley, AbilityProbe,
		CooldownArrow, CooldownDash, CooldownVolley, CooldownSweep, CooldownProbe,
		DamageAmount, EventKill, StateCasting, StateInvulnerable,
	];

	/// <summary>Every tag plus all of its dotted prefixes, each once ("a.b.c" -> a, a.b, a.b.c).</summary>
	public static IEnumerable<string> WithParents(IEnumerable<string> tags)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (string tag in tags)
		{
			string[] parts = tag.Split('.');
			for (int length = 1; length <= parts.Length; length++)
			{
				string prefix = string.Join('.', parts.Take(length));
				if (seen.Add(prefix))
				{
					yield return prefix;
				}
			}
		}
	}

	/// <summary>"ability.volley" -> "cooldown.volley".</summary>
	public static string CooldownFor(string abilityTag)
	{
		const string abilityPrefix = "ability.";
		if (!abilityTag.StartsWith(abilityPrefix, StringComparison.Ordinal))
		{
			throw new ArgumentException($"'{abilityTag}' is not an ability tag", nameof(abilityTag));
		}

		return "cooldown." + abilityTag[abilityPrefix.Length..];
	}

	/// <summary>The registered tag, or an <see cref="ArgumentException"/> naming the missing tag.</summary>
	public static Tag Request(string tag)
	{
		Tag result = Tag.RequestTag(ForgeManagers.Instance.TagsManager, tag, errorIfNotFound: false);
		if (!result.IsValid)
		{
			throw new ArgumentException(
				$"Tag '{tag}' is not registered. Add it to GameplayTags.All and run tools/sync_tags.",
				nameof(tag));
		}

		return result;
	}

	public static TagContainer ContainerOf(string tag) => Request(tag).GetSingleTagContainer()!;
}
