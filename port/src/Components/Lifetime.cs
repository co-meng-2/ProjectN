using Godot;

namespace VampireSurvivor.Components;

/// <summary>Counts down and announces expiry; the owner decides what to do about it.</summary>
public partial class Lifetime : Node
{
	private double _remaining;
	private bool _hasExpired;

	[Signal]
	public delegate void ExpiredEventHandler();

	[Export]
	public float Seconds { get; set; } = 1f;

	public override void _Ready() => _remaining = Seconds;

	public void Restart(float newSeconds)
	{
		Seconds = newSeconds;
		_remaining = newSeconds;
		_hasExpired = false;
	}

	public void Step(double delta)
	{
		if (_hasExpired)
		{
			return;
		}

		_remaining -= delta;
		if (_remaining <= 0.0)
		{
			_hasExpired = true;
			EmitSignal(SignalName.Expired);
		}
	}

	public override void _PhysicsProcess(double delta) => Step(delta);
}
