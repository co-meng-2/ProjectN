using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Core;

public class SpawnGeometryTest : TestCase
{
	public void TestRingPointIsRadiusAway()
	{
		Vector2 point = SpawnGeometry.RingPoint(new Vector2(10, 10), 50f, 0f);
		Assert.AlmostEq(point.X, 60.0);
		Assert.AlmostEq(point.Y, 10.0);
	}

	public void TestClampToRectRespectsMargin()
	{
		var bounds = new Rect2(-100, -100, 200, 200);
		Assert.Eq(SpawnGeometry.ClampToRect(new Vector2(500, 0), bounds, 10f), new Vector2(90, 0));
		Assert.Eq(SpawnGeometry.ClampToRect(new Vector2(0, -500), bounds), new Vector2(0, -100));
		Assert.Eq(SpawnGeometry.ClampToRect(new Vector2(5, 5), bounds), new Vector2(5, 5));
	}
}
