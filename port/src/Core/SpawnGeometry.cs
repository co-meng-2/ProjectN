using Godot;

namespace VampireSurvivor.Core;

/// <summary>Pure helpers for choosing spawn positions.</summary>
public static class SpawnGeometry
{
	public static Vector2 RingPoint(Vector2 center, float radius, float angle)
	{
		return center + (Vector2.Right.Rotated(angle) * radius);
	}

	public static Vector2 ClampToRect(Vector2 point, Rect2 bounds, float margin = 0f)
	{
		Rect2 inner = bounds.Grow(-margin);
		return new Vector2(
			Mathf.Clamp(point.X, inner.Position.X, inner.End.X),
			Mathf.Clamp(point.Y, inner.Position.Y, inner.End.Y));
	}
}
