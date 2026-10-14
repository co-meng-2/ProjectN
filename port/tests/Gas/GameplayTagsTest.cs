using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Gamesmiths.Forge.Godot.Core;
using Gamesmiths.Forge.Tags;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Tests.Gas;

public class GameplayTagsTest : TestCase
{
	public void TestEveryConstantAndSlotCooldownIsListedInAll()
	{
		string[] constants = typeof(GameplayTags)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(f => f.IsLiteral && f.FieldType == typeof(string))
			.Select(f => (string)f.GetRawConstantValue()!)
			.Concat(GameplayTags.SlotCooldowns)
			.OrderBy(s => s, StringComparer.Ordinal)
			.ToArray();
		Assert.SequenceEq(GameplayTags.All.OrderBy(s => s, StringComparer.Ordinal), constants);
	}

	public void TestAllTagsAreLowercaseDotted()
	{
		foreach (string tag in GameplayTags.All)
		{
			Assert.True(Regex.IsMatch(tag, "^[a-z]+(\\.[a-z]+)*$"), $"bad tag '{tag}'");
		}
	}

	public void TestWithParentsAddsEveryPrefixOnce()
	{
		string[] expanded = GameplayTags.WithParents(["ability.boss.charge", "ability.boss"]).ToArray();
		Assert.SequenceEq(expanded.OrderBy(s => s, StringComparer.Ordinal), new[] { "ability", "ability.boss", "ability.boss.charge" });
	}

	public void TestCooldownForSwapsThePrefix()
	{
		Assert.Eq(GameplayTags.CooldownFor(GameplayTags.AbilityVolley), GameplayTags.CooldownVolley);
		Assert.Eq(GameplayTags.CooldownFor(GameplayTags.AbilityDash), GameplayTags.CooldownDash);
		Assert.Eq(GameplayTags.CooldownFor(GameplayTags.AbilityProbe), GameplayTags.CooldownProbe);
	}

	public void TestForgeRegistryContainsEveryTagAndParent()
	{
		TagsManager manager = ForgeManagers.Instance.TagsManager;
		foreach (string tag in GameplayTags.WithParents(GameplayTags.All))
		{
			Assert.True(Tag.RequestTag(manager, tag, errorIfNotFound: false).IsValid, $"'{tag}' is not registered; run the sync tool");
		}
	}

	public void TestUnregisteredTagFailsLoudly()
	{
		Assert.Throws<ArgumentException>(() => GameplayTags.Request("ability.nonexistent"));
	}

	public void TestRequestedTagsMatchHierarchically()
	{
		Tag boss = GameplayTags.Request("ability.boss");
		Tag charge = GameplayTags.Request(GameplayTags.AbilityBossCharge);
		Assert.True(charge.MatchesTag(boss), "child tag matches its parent");
	}

	public void TestThereIsOneSlotCooldownTagPerPossibleSlot()
	{
		Assert.Eq(GameplayTags.SlotCooldowns.Length, PlayerProfile.MaxSlots);
		Assert.Eq(GameplayTags.SlotCooldowns.Distinct().Count(), GameplayTags.SlotCooldowns.Length);
		foreach (string tag in GameplayTags.SlotCooldowns)
		{
			Assert.True(GameplayTags.All.Contains(tag), tag);
		}
	}
}
