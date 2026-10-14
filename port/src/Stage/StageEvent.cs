using System;
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>
/// Base of an event controller (the root of an <see cref="EventDef.ControllerScene"/>). The base owns the time limit and
/// guarantees the event ends exactly once: <see cref="Succeed"/> / <see cref="Fail"/> emit their signal after
/// <see cref="Cleanup"/>, <see cref="Abort"/> only cleans up. Running out of time calls <see cref="OnTimeUp"/>, which fails
/// unless a subclass overrides it. Physics drives <see cref="Step"/> in play; tests call it by hand.
/// </summary>
public abstract partial class StageEvent : Node2D, IValidatable
{
	[Signal]
	public delegate void SucceededEventHandler();

	[Signal]
	public delegate void FailedEventHandler();

	[Signal]
	public delegate void ProgressChangedEventHandler(string text);

	public EventContext? Context { get; private set; }

	public float RemainingTime { get; private set; }

	public bool IsRunning { get; private set; }

	public bool HasEnded { get; private set; }

	public override void _Ready() => YSortEnabled = true; // statues, meteors and telegraphs depth-sort with the actors

	public void Begin(EventContext context)
	{
		if (IsRunning || HasEnded)
		{
			return;
		}

		Context = context;
		RemainingTime = context.Def.TimeLimit;
		IsRunning = true;
		OnBegin();
	}

	/// <summary>Ends the event without a result (boss appearance, run end): cleanup only, no signal.</summary>
	public void Abort()
	{
		if (!IsRunning)
		{
			return;
		}

		IsRunning = false;
		HasEnded = true;
		Cleanup();
	}

	public void Step(float delta)
	{
		if (!IsRunning)
		{
			return;
		}

		RemainingTime = Math.Max(RemainingTime - delta, 0f);
		OnStep(delta);
		if (IsRunning && RemainingTime <= 0f)
		{
			OnTimeUp();
		}
	}

	/// <summary>Things the HUD points at when they are off-screen.</summary>
	public virtual List<Node2D> Objectives() => [];

	/// <summary>Problems with the exported numbers (checked by the def before the run starts).</summary>
	public virtual List<string> Validate() => [];

	public void ReportProgress(string text) => EmitSignal(SignalName.ProgressChanged, text);

	public override void _PhysicsProcess(double delta) => Step((float)delta);

	protected void Succeed() => Finish(true);

	protected void Fail() => Finish(false);

	protected virtual void OnBegin()
	{
	}

	protected virtual void OnStep(float delta)
	{
	}

	protected virtual void OnTimeUp() => Fail();

	protected virtual void Cleanup()
	{
	}

	private void Finish(bool success)
	{
		if (!IsRunning)
		{
			return;
		}

		IsRunning = false;
		HasEnded = true;
		Cleanup();
		EmitSignal(success ? SignalName.Succeeded : SignalName.Failed);
	}
}
