using System.Collections.Generic;
using VampireSurvivor.Abilities;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Abilities;

public class AbilityPoolTest : TestCase
{
	private sealed record Rig(AbilityPool Pool, AbilityDef Common, AbilityDef Rare, AbilityDef Epic);

	private static AbilityDef Probe(string id, Grade minGrade = Grade.Common) =>
		DefFactory.Ability(id, DefFactory.Vitality(), minGrade: minGrade);

	private static Rig MakeRig()
	{
		AbilityDef common = Probe("common_one");
		AbilityDef rare = Probe("rare_one", Grade.Rare);
		AbilityDef epic = Probe("epic_one", Grade.Epic);
		return new Rig(new AbilityPool([common, rare, epic]), common, rare, epic);
	}

	public void TestDefsForIncludesEverythingAtOrBelowTheGrade()
	{
		Rig rig = MakeRig();
		List<AbilityDef> atCommon = rig.Pool.DefsFor(Grade.Common);
		Assert.Eq(atCommon.Count, 1);
		Assert.True(atCommon.Contains(rig.Common));
		List<AbilityDef> atRare = rig.Pool.DefsFor(Grade.Rare);
		Assert.Eq(atRare.Count, 2);
		Assert.True(atRare.Contains(rig.Rare));
		Assert.False(atRare.Contains(rig.Epic));
		Assert.Eq(rig.Pool.DefsFor(Grade.Legendary).Count, 3);
	}

	public void TestNoneAndRevealedHaveNoPool()
	{
		AbilityPool pool = MakeRig().Pool;
		Assert.Eq(pool.DefsFor(Grade.None).Count, 0);
		Assert.Eq(pool.DefsFor(Grade.Revealed).Count, 0);
		Assert.Null(pool.Roll(Grade.None, new RunRng(1)));
		Assert.Null(pool.Roll(Grade.Revealed, new RunRng(1)));
	}

	public void TestRollIsSeededAndOnlyReturnsPoolMembers()
	{
		Rig rig = MakeRig();
		var first = new List<AbilityDef?>();
		var second = new List<AbilityDef?>();
		var rngA = new RunRng(5);
		var rngB = new RunRng(5);
		for (int i = 0; i < 20; i++)
		{
			first.Add(rig.Pool.Roll(Grade.Rare, rngA));
			second.Add(rig.Pool.Roll(Grade.Rare, rngB));
		}

		Assert.SequenceEq(first, second, "same seed, same sequence");
		foreach (AbilityDef? def in first)
		{
			Assert.True(def == rig.Common || def == rig.Rare, "only Rare-pool members");
		}

		Assert.True(first.Contains(rig.Common) && first.Contains(rig.Rare), "both members appear over 20 rolls");
	}

	public void TestRollPrefersADifferentDefThanTheExcludedOne()
	{
		Rig rig = MakeRig();
		var rng = new RunRng(8);
		for (int i = 0; i < 20; i++)
		{
			Assert.True(rig.Pool.Roll(Grade.Rare, rng, rig.Common) == rig.Rare);
		}
	}

	public void TestRollWithOnlyTheExcludedDefReturnsIt()
	{
		Rig rig = MakeRig();
		Assert.True(rig.Pool.Roll(Grade.Common, new RunRng(8), rig.Common) == rig.Common);
	}

	public void TestEmptyPoolRollsNullAndNullsAreSkipped()
	{
		Assert.Null(new AbilityPool([]).Roll(Grade.Common, new RunRng(1)));
		var pool = new AbilityPool([null, Probe("a")]);
		Assert.Eq(pool.DefsFor(Grade.Common).Count, 1);
		Assert.NotNull(pool.Roll(Grade.Common, new RunRng(1)));
	}
}
