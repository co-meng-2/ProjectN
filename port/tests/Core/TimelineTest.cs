using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Core;

public class TimelineTest : TestCase
{
	public void TestMarksAreSorted()
	{
		var timeline = new Timeline(100f, [60f, 20f, 40f]);
		Assert.SequenceEq(timeline.Marks, new[] { 20f, 40f, 60f });
	}

	public void TestProgressIsClamped()
	{
		var timeline = new Timeline(100f);
		Assert.AlmostEq(timeline.Progress(25f), 0.25);
		Assert.AlmostEq(timeline.Progress(-5f), 0.0);
		Assert.AlmostEq(timeline.Progress(500f), 1.0);
	}

	public void TestProgressWithZeroDurationIsComplete()
	{
		Assert.AlmostEq(new Timeline(0f).Progress(3f), 1.0);
	}

	public void TestNextAfterSkipsCurrentAndPastMarks()
	{
		var timeline = new Timeline(100f, [20f, 40f]);
		Assert.AlmostEq(timeline.NextAfter(0f), 20.0);
		Assert.AlmostEq(timeline.NextAfter(20f), 40.0);
		Assert.AlmostEq(timeline.NextAfter(40f), -1.0);
	}

	public void TestCrossedExcludesStartAndIncludesEnd()
	{
		var timeline = new Timeline(100f, [20f, 40f, 60f]);
		Assert.SequenceEq(timeline.Crossed(20f, 40f), new[] { 1 });
		Assert.SequenceEq(timeline.Crossed(0f, 100f), new[] { 0, 1, 2 });
		Assert.SequenceEq(timeline.Crossed(41f, 59f), System.Array.Empty<int>());
	}
}
