using System;
using System.Collections.Generic;

namespace VampireSurvivor.Stage;

public enum StageState
{
	Prep,
	Running,
	Boss,
	Cleared,
	Failed,
}

/// <summary>The stage's lifecycle as an explicit state machine; owns transitions only.</summary>
public sealed class StageFlow
{
	private static readonly Dictionary<StageState, StageState[]> Allowed = new()
	{
		[StageState.Prep] = [StageState.Running],
		[StageState.Running] = [StageState.Boss, StageState.Cleared, StageState.Failed],
		[StageState.Boss] = [StageState.Cleared, StageState.Failed],
	};

	public event Action<StageState>? StateChanged;

	public StageState State { get; private set; } = StageState.Prep;

	public bool IsTerminal => State is StageState.Cleared or StageState.Failed;

	public bool TransitionTo(StageState to)
	{
		if (!Allowed.TryGetValue(State, out StageState[]? next) || Array.IndexOf(next, to) < 0)
		{
			return false;
		}

		State = to;
		StateChanged?.Invoke(State);
		return true;
	}
}
