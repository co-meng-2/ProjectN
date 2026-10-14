using System;
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>
/// Rolls the stage's event times once (seeded), starts each event on the clock tick that crosses its time and relays its
/// signals. At most one event runs: an event whose time comes while another runs waits and starts when that one ends.
/// Events are <see cref="EventDef.ControllerScene"/> instances; this file never names a concrete event.
/// </summary>
public partial class EventDirector : Node
{
	private readonly Queue<int> _waiting = new();
	private Timeline _timeline = new(0f);
	private List<EventDef> _defs = [];
	private Func<EventDef, EventContext> _contextFactory = null!;
	private float _previousTime = -1f;
	private EventDef? _runningDef;

	[Signal]
	public delegate void EventStartedEventHandler(EventDef def, StageEvent stageEvent);

	[Signal]
	public delegate void EventProgressedEventHandler(EventDef def, string text);

	/// <summary>A real result only (success or failure). An event cut short by the boss or the run end emits EventAborted.</summary>
	[Signal]
	public delegate void EventEndedEventHandler(EventDef def, bool success);

	[Signal]
	public delegate void EventAbortedEventHandler(EventDef def);

	public StageEvent? Running { get; private set; }

	/// <summary>Stage-layer check run by the stage: every controller root must be a StageEvent.</summary>
	public static List<string> ValidateDefs(EventSchedule? schedule)
	{
		var errors = new List<string>();
		if (schedule is null)
		{
			return errors;
		}

		foreach (EventDef? def in schedule.Pool)
		{
			if (def?.ControllerScene is null)
			{
				continue; // EventDef.Validate reports these
			}

			Node root = def.ControllerScene.Instantiate();
			if (root is not StageEvent)
			{
				errors.Add($"event '{def.Id}': controller_scene root is not a StageEvent");
			}

			root.Free();
		}

		return errors;
	}

	public void Bind(StageDef stageDef, StageClock clock, Func<EventDef, EventContext> contextFactory, RunRng rng, float[] waveTimes)
	{
		_contextFactory = contextFactory;
		float[] times = [];
		if (stageDef.Events is not null)
		{
			times = stageDef.Events.RollTimes(stageDef.Duration, waveTimes, rng);
			_defs = stageDef.Events.RollDefs(times.Length, rng);
		}

		_timeline = new Timeline(stageDef.Duration, times);
		clock.Ticked += OnClockTicked;
	}

	/// <summary>Rolled start times, ascending; the HUD draws them on the timeline.</summary>
	public float[] EventTimes() => _timeline.Marks;

	public List<EventDef> PlannedDefs() => [.. _defs];

	/// <summary>Ends the running event without reward (boss appearance, run end) and drops any waiting one.</summary>
	public void AbortRunning()
	{
		_waiting.Clear();
		if (Running is null)
		{
			return;
		}

		StageEvent stopped = Running;
		EventDef def = _runningDef!;
		Running = null;
		_runningDef = null;
		stopped.Abort();
		stopped.QueueFree();
		EmitSignal(SignalName.EventAborted, def);
	}

	private void OnClockTicked(float elapsed)
	{
		foreach (int index in _timeline.Crossed(_previousTime, elapsed))
		{
			_waiting.Enqueue(index);
		}

		_previousTime = elapsed;
		StartNext();
	}

	private void StartNext()
	{
		// The deferred call may land after the stage was discarded (restart).
		if (!IsInstanceValid(this) || !IsInsideTree() || Running is not null || _waiting.Count == 0)
		{
			return;
		}

		EventDef def = _defs[_waiting.Dequeue()];
		var started = def.ControllerScene!.Instantiate<StageEvent>();
		EventContext context = _contextFactory(def);
		context.Layer.AddChild(started);
		Running = started;
		_runningDef = def;
		started.ProgressChanged += text => EmitSignal(SignalName.EventProgressed, def, text);
		started.Succeeded += () => OnEventFinished(started, def, true);
		started.Failed += () => OnEventFinished(started, def, false);
		EmitSignal(SignalName.EventStarted, def, started);

		// A listener may have aborted the event already; it must not begin after that.
		if (Running != started)
		{
			return;
		}

		started.Begin(context);
	}

	private void OnEventFinished(StageEvent finished, EventDef def, bool success)
	{
		if (finished != Running)
		{
			return;
		}

		Running = null;
		_runningDef = null;
		finished.QueueFree();
		EmitSignal(SignalName.EventEnded, def, success);

		// Deferred: events usually end inside a physics callback (a hit), where new areas must not be added.
		Callable.From(StartNext).CallDeferred();
	}
}
