using System;
using Godot;

namespace VampireSurvivor.Components;

public enum TelegraphShape
{
	Circle,
	Line,
}

/// <summary>
/// A warning drawn on the ground - a circle or a line that fills up over its seconds - then <see cref="Elapsed"/> and free.
/// Purely visual: whoever shows it keeps its own timer and decides what happens when the warning ends.
/// </summary>
public partial class Telegraph : Node2D
{
	private const int Segments = 40;
	private const float EdgeWidth = 2f;

	/// <summary>Ground decal: drawn below every actor, whatever the y-sort says.</summary>
	private const int GroundZIndex = -1;

	private float _duration;
	private bool _armed;

	public Telegraph()
	{
		ZIndex = GroundZIndex;
	}

	[Signal]
	public delegate void ElapsedEventHandler();

	[Export]
	public Color FillColor { get; set; } = new(1f, 0.2f, 0.15f, 0.25f);

	[Export]
	public Color EdgeColor { get; set; } = new(1f, 0.3f, 0.2f, 0.9f);

	public TelegraphShape Shape { get; private set; } = TelegraphShape.Circle;

	public float Radius { get; private set; }

	public Vector2 LineOffset { get; private set; }

	public float LineWidth { get; private set; }

	public float Remaining { get; private set; }

	/// <summary>Adds a circular warning centred on the global <paramref name="position"/> under <paramref name="parent"/> (in the tree) and starts it.</summary>
	public static Telegraph SpawnCircle(Node parent, Vector2 position, float radius, float seconds)
	{
		var telegraph = new Telegraph();
		parent.AddChild(telegraph);
		telegraph.GlobalPosition = position;
		telegraph.ShowCircle(radius, seconds);
		return telegraph;
	}

	/// <summary>Adds a line warning from global <paramref name="from"/> to global <paramref name="to"/> and starts it.</summary>
	public static Telegraph SpawnLine(Node parent, Vector2 from, Vector2 to, float width, float seconds)
	{
		var telegraph = new Telegraph();
		parent.AddChild(telegraph);
		telegraph.GlobalPosition = from;
		telegraph.ShowLine(to - from, width, seconds);
		return telegraph;
	}

	public void ShowCircle(float radius, float seconds)
	{
		Shape = TelegraphShape.Circle;
		Radius = radius;
		Arm(seconds);
	}

	public void ShowLine(Vector2 offset, float width, float seconds)
	{
		Shape = TelegraphShape.Line;
		LineOffset = offset;
		LineWidth = width;
		Arm(seconds);
	}

	/// <summary>0 when shown, 1 when the warning is over.</summary>
	public float Progress() => _duration <= 0f ? 1f : Math.Clamp(1f - (Remaining / _duration), 0f, 1f);

	public void Step(float delta)
	{
		if (!_armed)
		{
			return;
		}

		Remaining = Math.Max(Remaining - delta, 0f);
		QueueRedraw();
		if (Remaining <= 0f)
		{
			_armed = false;
			EmitSignal(SignalName.Elapsed);
			QueueFree();
		}
	}

	public override void _PhysicsProcess(double delta) => Step((float)delta);

	public override void _Draw()
	{
		if (Shape == TelegraphShape.Circle)
		{
			DrawCircle(Vector2.Zero, Radius, FillColor);
			DrawCircle(Vector2.Zero, Radius * Progress(), FillColor);
			DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, Segments, EdgeColor, EdgeWidth);
		}
		else
		{
			DrawLine(Vector2.Zero, LineOffset, FillColor, LineWidth);
			DrawLine(Vector2.Zero, LineOffset * Progress(), EdgeColor, LineWidth * 0.4f);
		}
	}

	private void Arm(float seconds)
	{
		_duration = Math.Max(seconds, 0f);
		Remaining = _duration;
		_armed = true;
		QueueRedraw();
	}
}
