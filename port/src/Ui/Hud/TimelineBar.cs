using System;
using Godot;

namespace VampireSurvivor.Ui;

/// <summary>Stage progress: a fill for elapsed time and a diamond for every wave; passed marks turn grey.</summary>
public partial class TimelineBar : Control
{
	public const float MarkHalfWidth = 5f;

	private float _duration = 1f;
	private float[] _marks = [];
	private float _elapsed;

	[Export]
	public Color TrackColor { get; set; } = new(0.12f, 0.12f, 0.16f, 0.85f);

	[Export]
	public Color FillColor { get; set; } = new(0.35f, 0.7f, 1f);

	[Export]
	public Color MarkColor { get; set; } = new(1f, 0.75f, 0.2f);

	[Export]
	public Color PassedMarkColor { get; set; } = new(0.55f, 0.55f, 0.55f);

	public void Configure(float duration, float[] marks)
	{
		_duration = duration;
		_marks = marks;
		_elapsed = 0f;
		QueueRedraw();
	}

	public void SetElapsed(float elapsed)
	{
		_elapsed = elapsed;
		QueueRedraw();
	}

	public float MarkX(float mark)
	{
		if (_duration <= 0f)
		{
			return 0f;
		}

		return Size.X * Math.Clamp(mark / _duration, 0f, 1f);
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), TrackColor);
		float ratio = _duration <= 0f ? 1f : Math.Clamp(_elapsed / _duration, 0f, 1f);
		DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X * ratio, Size.Y)), FillColor);
		foreach (float mark in _marks)
		{
			float x = MarkX(mark);
			Color color = mark <= _elapsed ? PassedMarkColor : MarkColor;
			DrawColoredPolygon(
				[
					new Vector2(x, 0f),
					new Vector2(x + MarkHalfWidth, Size.Y * 0.5f),
					new Vector2(x, Size.Y),
					new Vector2(x - MarkHalfWidth, Size.Y * 0.5f),
				],
				color);
		}
	}
}
