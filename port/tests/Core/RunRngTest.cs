using System;
using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Core;

public class RunRngTest : TestCase
{
	public void TestSameSeedGivesSameSequence()
	{
		var a = new RunRng(42);
		var b = new RunRng(42);
		for (int i = 0; i < 5; i++)
		{
			Assert.Eq(a.Float(), b.Float(), $"draw {i}");
		}
	}

	public void TestPickReturnsNullForEmptyArray()
	{
		Assert.Null(new RunRng(1).Pick(Array.Empty<string>()));
	}

	public void TestPickReturnsAnElement()
	{
		string[] items = ["a", "b", "c"];
		Assert.True(Array.IndexOf(items, new RunRng(1).Pick(items)) >= 0);
	}

	public void TestWeightedIndexNeverPicksZeroWeight()
	{
		var rng = new RunRng(7);
		float[] weights = [0f, 0f, 5f, 0f];
		for (int i = 0; i < 30; i++)
		{
			Assert.Eq(rng.WeightedIndex(weights), 2);
		}
	}

	public void TestWeightedIndexIsMinusOneWithoutPositiveWeight()
	{
		Assert.Eq(new RunRng(1).WeightedIndex([0f, -1f]), -1);
		Assert.Eq(new RunRng(1).WeightedIndex(Array.Empty<float>()), -1);
	}

	public void TestWeightedReturnsMatchingItem()
	{
		Assert.Eq(new RunRng(3).Weighted(["x", "y"], [0f, 1f]), "y");
	}

	public void TestChanceExtremes()
	{
		var rng = new RunRng(9);
		for (int i = 0; i < 20; i++)
		{
			Assert.False(rng.Chance(0f));
			Assert.True(rng.Chance(1f));
		}
	}

	public void TestOffsetInCircleStaysWithinRadius()
	{
		var rng = new RunRng(5);
		for (int i = 0; i < 50; i++)
		{
			Assert.True(rng.OffsetInCircle(10f).Length() <= 10.0001f);
		}
	}
}
