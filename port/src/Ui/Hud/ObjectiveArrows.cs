using System;
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Ui;

/// <summary>Arrows at the screen edge pointing at the running event's objectives that are off-screen.</summary>
public partial class ObjectiveArrows : Control
{
	private readonly List<Vector2> _positions = [];
	private readonly List<Vector2> _directions = [];
	private StageEvent? _event;

	[Export]
	public float Margin { get; set; } = 28f;

	[Export]
	public float ArrowSize { get; set; } = 12f;

	[Export]
	public Color ArrowColor { get; set; } = new(1f, 0.85f, 0.3f, 0.9f);

	public IReadOnlyList<Vector2> ArrowPositions => _positions;

	/// <summary>Where the ray from the view centre to <paramref name="target"/> leaves the view shrunk by <paramref name="edgeMargin"/>.</summary>
	public static Vector2 EdgePoint(Vector2 target, Rect2 view, float edgeMargin)
	{
		Rect2 inner = view.Grow(-edgeMargin);
		Vector2 center = inner.GetCenter();
		Vector2 offset = target - center;
		float limit = 1f;
		if (Math.Abs(offset.X) > 0f)
		{
			limit = Math.Min(limit, inner.Size.X * 0.5f / Math.Abs(offset.X));
		}

		if (Math.Abs(offset.Y) > 0f)
		{
			limit = Math.Min(limit, inner.Size.Y * 0.5f / Math.Abs(offset.Y));
		}

		return center + (offset * limit);
	}

	public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

	public void Track(StageEvent stageEvent) => _event = stageEvent;

	public void Clear()
	{
		_event = null;
		_positions.Clear();
		_directions.Clear();
		QueueRedraw();
	}

	/// <summary><paramref name="canvasTransform"/> maps world positions to the screen (the camera); <paramref name="viewSize"/> is the screen size.</summary>
	public void Refresh(Transform2D canvasTransform, Vector2 viewSize)
	{
		_positions.Clear();
		_directions.Clear();
		if (IsInstanceValid(_event))
		{
			var view = new Rect2(Vector2.Zero, viewSize);
			foreach (Node2D objective in _event!.Objectives())
			{
				Vector2 screen = canvasTransform * objective.GlobalPosition;
				if (view.HasPoint(screen))
				{
					continue;
				}

				_positions.Add(EdgePoint(screen, view, Margin));
				_directions.Add((screen - view.GetCenter()).Normalized());
			}
		}

		QueueRedraw();
	}

	public override void _Process(double delta) => Refresh(GetViewport().GetCanvasTransform(), GetViewportRect().Size);

	public override void _Draw()
	{
		for (int i = 0; i < _positions.Count; i++)
		{
			Vector2 tip = _positions[i];
			Vector2 direction = _directions[i];
			Vector2 side = direction.Orthogonal() * ArrowSize * 0.6f;
			Vector2 back = tip - (direction * ArrowSize);
			DrawColoredPolygon([tip, back + side, back - side], ArrowColor);
		}
	}
}
