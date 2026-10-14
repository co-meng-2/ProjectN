using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class RunStateTest : TestCase
{
	public void TestNewRunStartsClean()
	{
		var state = new RunState(DefFactory.Stage(), new PlayerProfile(), 7);
		Assert.Eq(state.Wallet.Gold, 0);
		Assert.Eq(state.Wallet.Stone, 0);
		Assert.Eq(state.Kills, 0);
		Assert.Eq(state.Rng.SeedValue, 7L);
	}

	public void TestToResultReportsTheRun()
	{
		var state = new RunState(DefFactory.Stage(), new PlayerProfile());
		state.Wallet.Add(CurrencyKind.Gold, 12);
		state.Wallet.Add(CurrencyKind.Stone, 3);
		state.Kills = 9;
		RunResult result = state.ToResult(RunOutcome.Cleared, 42f);
		Assert.Eq(result.Outcome, RunOutcome.Cleared);
		Assert.Eq(result.Gold, 12);
		Assert.Eq(result.Stone, 3);
		Assert.Eq(result.Kills, 9);
		Assert.AlmostEq(result.Elapsed, 42.0);
	}

	public void TestWalletIsSeededFromTheStageDef()
	{
		StageDef def = DefFactory.Stage();
		def.StartGold = 200;
		def.StartStone = 3;
		var state = new RunState(def, new PlayerProfile(), 7);
		Assert.Eq(state.Wallet.Gold, 200);
		Assert.Eq(state.Wallet.Stone, 3);
	}
}
