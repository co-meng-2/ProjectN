namespace VampireSurvivor.Defs;

public enum RunOutcome
{
	Cleared,
	Failed,
}

/// <summary>What a finished stage run reports to its caller.</summary>
public sealed class RunResult
{
	public RunOutcome Outcome { get; init; } = RunOutcome.Failed;

	public float Elapsed { get; init; }

	public int Kills { get; init; }

	public int Gold { get; init; }

	public int Stone { get; init; }
}
