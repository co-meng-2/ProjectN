using Godot;
using VampireSurvivor.Tests.Support;
using VampireSurvivor.Ui;

namespace VampireSurvivor.Tests.Ui;

public class ObjectiveArrowsTest : TestCase
{
	private static readonly Vector2 View = new(800f, 600f);

	private ObjectiveArrows MakeArrows()
	{
		ObjectiveArrows arrows = Adopt(new ObjectiveArrows { Margin = 20f });
		arrows.SetProcess(false);
		return arrows;
	}

	private ProbeEvent ProbeWith(params Vector2[] points)
	{
		ProbeEvent probe = Adopt(new ProbeEvent());
		probe.SetPhysicsProcess(false);
		foreach (Vector2 point in points)
		{
			probe.ObjectiveNodes.Add(Adopt(new Node2D { Position = point }));
		}

		return probe;
	}

	public void TestEdgePointClampsToTheInnerRectAlongTheRayFromTheCentre()
	{
		var view = new Rect2(Vector2.Zero, View);
		Assert.Eq(ObjectiveArrows.EdgePoint(new Vector2(1600f, 300f), view, 20f), new Vector2(780f, 300f));
		Assert.Eq(ObjectiveArrows.EdgePoint(new Vector2(400f, -900f), view, 20f), new Vector2(400f, 20f));
		Vector2 corner = ObjectiveArrows.EdgePoint(new Vector2(1200f, 1200f), view, 20f);
		Assert.AlmostEq(corner.X, 400.0 + (800.0 * 280.0 / 900.0), 0.01);
		Assert.AlmostEq(corner.Y, 580.0, 0.01);
	}

	public void TestOnlyOffScreenObjectivesGetArrows()
	{
		ObjectiveArrows arrows = MakeArrows();
		arrows.Track(ProbeWith(new Vector2(100f, 100f), new Vector2(2000f, 300f)));
		arrows.Refresh(Transform2D.Identity, View);
		Assert.SequenceEq(arrows.ArrowPositions, new[] { new Vector2(780f, 300f) });
	}

	public void TestTheCameraTransformDecidesWhatIsOnScreen()
	{
		ObjectiveArrows arrows = MakeArrows();
		arrows.Track(ProbeWith(new Vector2(1400f, 300f)));
		arrows.Refresh(Transform2D.Identity, View);
		Assert.Eq(arrows.ArrowPositions.Count, 1, "off-screen without a camera offset");
		arrows.Refresh(new Transform2D(0f, new Vector2(-1000f, 0f)), View);
		Assert.Eq(arrows.ArrowPositions.Count, 0, "the camera moved right: now on screen");
	}

	public void TestClearedOrFreedObjectivesHaveNoArrows()
	{
		ObjectiveArrows arrows = MakeArrows();
		ProbeEvent probe = ProbeWith(new Vector2(-900f, 300f));
		arrows.Track(probe);
		arrows.Refresh(Transform2D.Identity, View);
		Assert.Eq(arrows.ArrowPositions.Count, 1);
		probe.ObjectiveNodes[0].Free();
		arrows.Refresh(Transform2D.Identity, View);
		Assert.Eq(arrows.ArrowPositions.Count, 0);
		arrows.Clear();
		arrows.Refresh(Transform2D.Identity, View);
		Assert.Eq(arrows.ArrowPositions.Count, 0);
	}
}
