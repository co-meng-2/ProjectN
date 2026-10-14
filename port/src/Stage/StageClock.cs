using Godot;

namespace VampireSurvivor.Stage;

/// <summary>
/// Elapsed stage time. Everything time-driven (spawning, waves, HUD) subscribes to <see cref="Ticked"/>. <see cref="Ended"/>
/// fires once when <see cref="Elapsed"/> reaches <see cref="Duration"/>; the clock then keeps ticking (overtime: the boss
/// fight) until stopped, so the trickle continues while waves, scheduled inside the duration, never fire again.
/// </summary>
public partial class StageClock : Node
{
	[Signal]
	public delegate void TickedEventHandler(float elapsed);

	[Signal]
	public delegate void EndedEventHandler();

	public float Elapsed { get; private set; }

	public float Duration { get; private set; }

	public bool IsRunning { get; private set; }

	public bool HasEnded { get; private set; }

	public void Start(float duration)
	{
		Duration = duration;
		Elapsed = 0f;
		IsRunning = true;
		HasEnded = false;
	}

	public void Stop() => IsRunning = false;

	public void Step(float delta)
	{
		if (!IsRunning)
		{
			return;
		}

		Elapsed += delta;
		EmitSignal(SignalName.Ticked, Elapsed);
		if (!HasEnded && Elapsed >= Duration)
		{
			HasEnded = true;
			EmitSignal(SignalName.Ended);
		}
	}

	public override void _PhysicsProcess(double delta) => Step((float)delta);
}
