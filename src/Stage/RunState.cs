using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>Mutable state of one stage run. Dies with the stage; nothing here is global.</summary>
public sealed class RunState
{
	public RunState(StageDef stageDef, PlayerProfile profile, long runSeed = 0)
	{
		StageDef = stageDef;
		Profile = profile;
		Rng = new RunRng(runSeed);
		Wallet.Add(CurrencyKind.Gold, stageDef.StartGold);
		Wallet.Add(CurrencyKind.Stone, stageDef.StartStone);
	}

	public StageDef StageDef { get; }

	public PlayerProfile Profile { get; }

	public RunRng Rng { get; }

	public Wallet Wallet { get; } = new();

	public int Kills { get; set; }

	public RunResult ToResult(RunOutcome outcome, float elapsed) => new()
	{
		Outcome = outcome,
		Elapsed = elapsed,
		Kills = Kills,
		Gold = Wallet.Gold,
		Stone = Wallet.Stone,
	};
}
