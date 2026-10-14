using Godot;
using VampireSurvivor.Ui;

namespace VampireSurvivor.Tests.Ui;

public class TimelineBarTest : TestCase
{
	private TimelineBar MakeBar(float duration, params float[] marks)
	{
		TimelineBar bar = Adopt(new TimelineBar());
		bar.Size = new Vector2(400f, 20f);
		bar.Configure(duration, marks);
		return bar;
	}

	public void TestMarkXScalesWithWidthAndDuration()
	{
		TimelineBar bar = MakeBar(100f, 25f, 50f);
		Assert.AlmostEq(bar.MarkX(25f), 100.0);
		Assert.AlmostEq(bar.MarkX(50f), 200.0);
		Assert.AlmostEq(bar.MarkX(500f), 400.0, 0.001, "clamped to the bar");
	}

	public void TestZeroDurationDoesNotDivideByZero()
	{
		TimelineBar bar = MakeBar(0f, 10f);
		bar.SetElapsed(5f);
		Assert.AlmostEq(bar.MarkX(10f), 0.0);
	}
}
