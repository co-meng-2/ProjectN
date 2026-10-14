using System.Collections.Generic;
using Godot;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class ArenaTest : TestCase
{
	private Arena MakeArena() => Adopt(GD.Load<PackedScene>("res://port/src/Stage/arena.tscn").Instantiate<Arena>());

	public void TestBuildCentersTheBoundsAndSurroundsThemWithWalls()
	{
		Arena arena = MakeArena();
		arena.Build(new Vector2(400f, 300f), DefFactory.Texture(32, 32));
		Assert.Eq(arena.Bounds, new Rect2(-200f, -150f, 400f, 300f));
		Assert.Eq(arena.Walls.GetChildCount(), 4);
		var centers = new List<Vector2>();
		foreach (Node child in arena.Walls.GetChildren())
		{
			centers.Add(((CollisionShape2D)child).Position);
		}

		Assert.True(centers.Contains(new Vector2(0f, -250f)), "top wall");
		Assert.True(centers.Contains(new Vector2(0f, 250f)), "bottom wall");
		Assert.True(centers.Contains(new Vector2(-300f, 0f)), "left wall");
		Assert.True(centers.Contains(new Vector2(300f, 0f)), "right wall");
	}

	public void TestRebuildingReplacesTheWalls()
	{
		Arena arena = MakeArena();
		arena.Build(new Vector2(400f, 300f), DefFactory.Texture(32, 32));
		arena.Build(new Vector2(800f, 600f), DefFactory.Texture(32, 32));
		Assert.Eq(arena.Walls.GetChildCount(), 4);
		Assert.Eq(arena.Bounds.Size, new Vector2(800f, 600f));
	}
}
