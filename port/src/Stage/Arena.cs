using Godot;

namespace VampireSurvivor.Stage;

/// <summary>The playfield: a tiled ground and four walls around it.</summary>
public partial class Arena : Node2D
{
	public const float WallThickness = 200f;

	[Export]
	public Sprite2D Ground { get; set; } = null!;

	[Export]
	public StaticBody2D Walls { get; set; } = null!;

	public Rect2 Bounds { get; private set; }

	/// <summary>Call after the arena has entered the tree.</summary>
	public void Build(Vector2 size, Texture2D groundTexture)
	{
		Bounds = new Rect2(-size / 2f, size);
		Ground.Texture = groundTexture;
		Ground.TextureRepeat = TextureRepeatEnum.Enabled;
		Ground.RegionEnabled = true;
		Ground.RegionRect = new Rect2(Vector2.Zero, size);
		foreach (Node child in Walls.GetChildren())
		{
			Walls.RemoveChild(child);
			child.Free();
		}

		foreach (Rect2 rect in WallRects())
		{
			var shape = new CollisionShape2D
			{
				Shape = new RectangleShape2D { Size = rect.Size },
				Position = rect.GetCenter(),
			};
			Walls.AddChild(shape);
		}
	}

	private Rect2[] WallRects()
	{
		float t = WallThickness;
		float width = Bounds.Size.X + (2f * t);
		return
		[
			new Rect2(Bounds.Position.X - t, Bounds.Position.Y - t, width, t),
			new Rect2(Bounds.Position.X - t, Bounds.End.Y, width, t),
			new Rect2(Bounds.Position.X - t, Bounds.Position.Y, t, Bounds.Size.Y),
			new Rect2(Bounds.End.X, Bounds.Position.Y, t, Bounds.Size.Y),
		];
	}
}
