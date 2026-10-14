using System;
using System.Threading.Tasks;
using Godot;

namespace VampireSurvivor.Tests.Support;

public class RunnerSelfTest : TestCase
{
	public void TestPassingSyncTest()
	{
		Assert.Eq(1 + 1, 2);
	}

	public async Task TestAsyncTestsAreAwaited()
	{
		await WaitSeconds(0.05);
		Assert.True(true);
	}

	public void TestAdoptedNodesAreInTheTreeAndFreedAfterwards()
	{
		var node = Adopt(new Node());
		Assert.True(node.IsInsideTree());
	}

	public void TestFailedAssertionsThrowAssertionException()
	{
		Assert.Throws<AssertionException>(() => Assert.Eq(1, 2));
		Assert.Throws<AssertionException>(() => Assert.True(false));
		Assert.Throws<AssertionException>(() => Assert.AlmostEq(1.0, 2.0));
	}

	public void TestSequenceEqComparesElements()
	{
		Assert.SequenceEq(new[] { 1, 2, 3 }, new[] { 1, 2, 3 });
		Assert.Throws<AssertionException>(() => Assert.SequenceEq(new[] { 1 }, new[] { 2 }));
	}

	public void TestNoErrorsAcceptsEmptyAndRejectsMessages()
	{
		Assert.NoErrors(Array.Empty<string>());
		Assert.Throws<AssertionException>(() => Assert.NoErrors(new[] { "bad" }));
	}
}
