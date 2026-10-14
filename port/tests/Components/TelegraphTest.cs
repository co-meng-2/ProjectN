using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class TelegraphTest : TestCase
{
	private Telegraph MakeTelegraph()
	{
		Telegraph telegraph = Adopt(new Telegraph());
		telegraph.SetPhysicsProcess(false);
		return telegraph;
	}

	public void TestCircleCountsDownThenAnnouncesAndFreesItself()
	{
		Telegraph telegraph = MakeTelegraph();
		int elapsed = 0;
		telegraph.Elapsed += () => elapsed++;
		telegraph.ShowCircle(60f, 1f);
		Assert.Eq(telegraph.Shape, TelegraphShape.Circle);
		Assert.AlmostEq(telegraph.Radius, 60.0);
		telegraph.Step(0.4f);
		Assert.AlmostEq(telegraph.Remaining, 0.6);
		Assert.AlmostEq(telegraph.Progress(), 0.4);
		Assert.Eq(elapsed, 0);
		telegraph.Step(0.7f);
		Assert.Eq(elapsed, 1);
		Assert.True(telegraph.IsQueuedForDeletion());
		telegraph.Step(0.5f);
		Assert.Eq(elapsed, 1, "announces once");
	}

	public void TestTelegraphIsAGroundDecalBelowTheActors()
	{
		Node2D actor = Adopt(new Node2D());
		Assert.True(MakeTelegraph().ZIndex < actor.ZIndex, "drawn under actors regardless of y-sort");
	}

	public void TestLineKeepsItsOffsetAndWidth()
	{
		Telegraph telegraph = MakeTelegraph();
		telegraph.ShowLine(new Vector2(360f, -40f), 24f, 0.7f);
		Assert.Eq(telegraph.Shape, TelegraphShape.Line);
		Assert.Eq(telegraph.LineOffset, new Vector2(360f, -40f));
		Assert.AlmostEq(telegraph.LineWidth, 24.0);
		Assert.AlmostEq(telegraph.Remaining, 0.7);
	}

	public void TestAnUnarmedTelegraphDoesNothing()
	{
		Telegraph telegraph = MakeTelegraph();
		int elapsed = 0;
		telegraph.Elapsed += () => elapsed++;
		telegraph.Step(5f);
		Assert.Eq(elapsed, 0);
		Assert.False(telegraph.IsQueuedForDeletion());
	}

	public void TestSpawnCirclePlacesARunningTelegraphUnderTheParent()
	{
		Node2D parent = Adopt(new Node2D());
		Telegraph telegraph = Telegraph.SpawnCircle(parent, new Vector2(130f, -70f), 55f, 0.9f);
		Assert.True(telegraph.GetParent() == parent);
		Assert.Eq(telegraph.GlobalPosition, new Vector2(130f, -70f));
		Assert.Eq(telegraph.Shape, TelegraphShape.Circle);
		Assert.AlmostEq(telegraph.Radius, 55.0);
		Assert.AlmostEq(telegraph.Remaining, 0.9);
		Assert.Eq(ActorFixture.LiveChildren<Telegraph>(parent).Count, 1);
		telegraph.Step(1f);
		Assert.Eq(ActorFixture.LiveChildren<Telegraph>(parent).Count, 0, "a finished telegraph is not live");
	}

	public void TestSpawnLineRunsFromTheStartToTheEndPoint()
	{
		Node2D parent = Adopt(new Node2D());
		Telegraph telegraph = Telegraph.SpawnLine(parent, new Vector2(-50f, 20f), new Vector2(310f, -20f), 24f, 0.7f);
		Assert.Eq(telegraph.GlobalPosition, new Vector2(-50f, 20f));
		Assert.Eq(telegraph.Shape, TelegraphShape.Line);
		Assert.Eq(telegraph.LineOffset, new Vector2(360f, -40f));
		Assert.AlmostEq(telegraph.LineWidth, 24.0);
		Assert.AlmostEq(telegraph.Remaining, 0.7);
	}
}
