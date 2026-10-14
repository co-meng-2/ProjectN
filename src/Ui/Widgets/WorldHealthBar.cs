using System;
using Godot;
using VampireSurvivor.Components;

namespace VampireSurvivor.Ui;

/// <summary>Health bar drawn in world space above an actor. Read-only: it observes Health and follows its anchor.</summary>
public partial class WorldHealthBar : Node2D
{
	private float _current;
	private float _maximum;
	private Node2D? _anchor;
	private Vector2 _offset;

	/// <summary>Bar size in pixels (the GDScript export was <c>size</c>; Node2D has no size of its own).</summary>
	[Export]
	public Vector2 BarSize { get; set; } = new(80f, 8f);

	[Export]
	public Color BackgroundColour { get; set; } = new(0.1f, 0.1f, 0.1f, 0.8f);

	[Export]
	public Color BorderColour { get; set; } = new(0f, 0f, 0f);

	[Export]
	public Color FillColourHigh { get; set; } = new(0.3f, 0.8f, 0.3f);

	[Export]
	public Color FillColourMid { get; set; } = new(0.95f, 0.6f, 0.15f);

	[Export]
	public Color FillColourLow { get; set; } = new(0.85f, 0.15f, 0.15f);

	[Export]
	public bool AlwaysVisible { get; set; } = true;

	public void Bind(Health health, Node2D? anchor, Vector2 offset)
	{
		_anchor = anchor;
		_offset = offset;
		health.Changed += OnHealthChanged;
		OnHealthChanged(health.Current(), health.Maximum());
		Step();
	}

	public override void _Process(double delta) => Step();

	/// <summary>Follows the anchor and refreshes visibility; _Process calls it every frame.</summary>
	public void Step()
	{
		if (!IsInstanceValid(_anchor))
		{
			Visible = false;
			return;
		}

		GlobalPosition = _anchor!.GlobalPosition + _offset;
		Visible = AlwaysVisible || FillRatio() < 1f;
	}

	public float FillRatio()
	{
		if (_maximum <= 0f)
		{
			return 0f;
		}

		return Math.Clamp(_current / _maximum, 0f, 1f);
	}

	public Color ColourFor(float ratio)
	{
		if (ratio > 0.6f)
		{
			return FillColourHigh;
		}

		return ratio > 0.3f ? FillColourMid : FillColourLow;
	}

	public override void _Draw()
	{
		var rect = new Rect2(-BarSize * 0.5f, BarSize);
		DrawRect(rect, BackgroundColour);
		float ratio = FillRatio();
		DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X * ratio, rect.Size.Y)), ColourFor(ratio));
		DrawRect(rect, BorderColour, false, 1f);
	}

	private void OnHealthChanged(float current, float maximum)
	{
		_current = current;
		_maximum = maximum;
		QueueRedraw();
	}
}
