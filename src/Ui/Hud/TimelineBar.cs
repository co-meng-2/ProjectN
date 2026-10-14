using System;
using Godot;

namespace VampireSurvivor.Ui;

/// <summary>
/// Stage progress: a fill for elapsed time, a diamond for every wave and a circle for every event (passed marks turn
/// grey), and a "BOSS" label once the boss fight starts.
/// </summary>
public partial class TimelineBar : Control
{
	public const float MarkHalfWidth = 5f;

	private float _duration = 1f;
	private float[] _marks = [];
	private float _elapsed;
	private Label? _bossLabel;

	[Export]
	public Color TrackColor { get; set; } = new(0.12f, 0.12f, 0.16f, 0.85f);

	[Export]
	public Color FillColor { get; set; } = new(0.35f, 0.7f, 1f);

	[Export]
	public Color MarkColor { get; set; } = new(1f, 0.75f, 0.2f);

	[Export]
	public Color EventMarkColor { get; set; } = new(0.55f, 1f, 0.45f);

	[Export]
	public Color PassedMarkColor { get; set; } = new(0.55f, 0.55f, 0.55f);

	[Export]
	public Color BossTextColor { get; set; } = new(1f, 0.35f, 0.3f);

	public float[] EventMarks { get; private set; } = [];

	public bool IsShowingBoss => _bossLabel is { Visible: true };

	public override void _Ready()
	{
		_bossLabel = new Label
		{
			Name = "BossLabel",
			Text = "BOSS",
			Visible = false,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_bossLabel.AddThemeColorOverride("font_color", BossTextColor);
		AddChild(_bossLabel);
		_bossLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public void Configure(float duration, float[] marks, float[]? eventMarks = null)
	{
		_duration = duration;
		_marks = marks;
		EventMarks = eventMarks ?? [];
		_elapsed = 0f;
		QueueRedraw();
	}

	public void ShowBoss()
	{
		_bossLabel!.Visible = true;
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

		foreach (float mark in EventMarks)
		{
			Color color = mark <= _elapsed ? PassedMarkColor : EventMarkColor;
			DrawCircle(new Vector2(MarkX(mark), Size.Y * 0.5f), MarkHalfWidth, color);
		}
	}
}
