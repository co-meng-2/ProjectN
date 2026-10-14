using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Tests.Components;

public class TargetProviderTest : TestCase
{
	private TargetProvider MakeProvider() => Adopt(new TargetProvider());

	private Node2D MakeEnemy(Vector2 at)
	{
		var node = Adopt(new Node2D());
		node.AddToGroup(Groups.Enemies);
		node.GlobalPosition = at;
		return node;
	}

	public void TestNearestEnemyPicksTheClosestWithinRange()
	{
		TargetProvider provider = MakeProvider();
		Node2D far = MakeEnemy(new Vector2(200, 0));
		Node2D near = MakeEnemy(new Vector2(50, 0));
		Assert.True(provider.NearestEnemy(Vector2.Zero, 300f) == near);
		Assert.True(provider.NearestEnemy(Vector2.Zero, 100f) == near);
		far.QueueFree();
	}

	public void TestNearestEnemyIsNullWhenNothingIsInRange()
	{
		TargetProvider provider = MakeProvider();
		MakeEnemy(new Vector2(500, 0));
		Assert.Null(provider.NearestEnemy(Vector2.Zero, 100f));
	}

	public void TestNearestEnemySkipsQueuedForDeletion()
	{
		TargetProvider provider = MakeProvider();
		Node2D doomed = MakeEnemy(new Vector2(10, 0));
		Node2D other = MakeEnemy(new Vector2(80, 0));
		doomed.QueueFree();
		Assert.True(provider.NearestEnemy(Vector2.Zero, 300f) == other);
	}

	public void TestNearestEnemySkipsNodesThatLeftTheGroup()
	{
		TargetProvider provider = MakeProvider();
		Node2D dying = MakeEnemy(new Vector2(10, 0));
		dying.RemoveFromGroup(Groups.Enemies);
		Assert.Null(provider.NearestEnemy(Vector2.Zero, 300f));
	}

	public void TestTargetForPrefersThePolicyTargetThenFallsBack()
	{
		TargetProvider provider = MakeProvider();
		var building = Adopt(new Node2D());
		var player = Adopt(new Node2D());
		provider.Building = building;
		provider.Player = player;
		Assert.True(provider.TargetFor(TargetPolicy.Building) == building);
		Assert.True(provider.TargetFor(TargetPolicy.Player) == player);
		player.Free();
		Assert.True(provider.TargetFor(TargetPolicy.Player) == building);
		building.Free();
		Assert.Null(provider.TargetFor(TargetPolicy.Building));
	}

	public void TestNearestEnemiesReturnsUpToCountSortedByDistance()
	{
		TargetProvider provider = MakeProvider();
		Node2D fifty = MakeEnemy(new Vector2(50, 0));
		MakeEnemy(new Vector2(150, 0));
		MakeEnemy(new Vector2(400, 0));
		Node2D ninety = MakeEnemy(new Vector2(0, 90));
		var two = provider.NearestEnemies(Vector2.Zero, 300f, 2);
		Assert.Eq(two.Count, 2);
		Assert.True(two[0] == fifty);
		Assert.True(two[1] == ninety);
		Assert.Eq(provider.NearestEnemies(Vector2.Zero, 300f, 10).Count, 3, "the one at 400 is out of range");
	}

	public void TestNonPositiveCountOrRangeYieldsNothing()
	{
		TargetProvider provider = MakeProvider();
		MakeEnemy(new Vector2(10, 0));
		Assert.Eq(provider.NearestEnemies(Vector2.Zero, 300f, 0).Count, 0);
		Assert.Eq(provider.NearestEnemies(Vector2.Zero, 300f, -3).Count, 0);
		Assert.Eq(provider.NearestEnemies(Vector2.Zero, 0f, 5).Count, 0, "a zero range reaches no one standing off-origin");
		Assert.Null(provider.NearestEnemy(Vector2.Zero, -5f));
	}

	public void TestNearestEnemiesSkipsDoomedNodes()
	{
		TargetProvider provider = MakeProvider();
		Node2D doomed = MakeEnemy(new Vector2(10, 0));
		Node2D other = MakeEnemy(new Vector2(80, 0));
		doomed.QueueFree();
		Assert.SequenceEq(provider.NearestEnemies(Vector2.Zero, 300f, 5), new[] { other });
	}
}
