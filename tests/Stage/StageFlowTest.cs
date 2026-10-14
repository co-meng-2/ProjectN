using System.Collections.Generic;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Tests.Stage;

public class StageFlowTest : TestCase
{
	public void TestNormalProgressionAndEvent()
	{
		var flow = new StageFlow();
		var seen = new List<StageState>();
		flow.StateChanged += seen.Add;
		Assert.True(flow.TransitionTo(StageState.Running));
		Assert.True(flow.TransitionTo(StageState.Cleared));
		Assert.SequenceEq(seen, new[] { StageState.Running, StageState.Cleared });
		Assert.True(flow.IsTerminal);
	}

	public void TestIllegalTransitionsAreRefused()
	{
		var flow = new StageFlow();
		Assert.False(flow.TransitionTo(StageState.Cleared), "cannot clear before running");
		flow.TransitionTo(StageState.Running);
		flow.TransitionTo(StageState.Failed);
		Assert.False(flow.TransitionTo(StageState.Running), "terminal states are final");
		Assert.False(flow.TransitionTo(StageState.Cleared));
		Assert.Eq(flow.State, StageState.Failed);
	}

	public void TestBossStateSitsBetweenRunningAndTheEnd()
	{
		var flow = new StageFlow();
		flow.TransitionTo(StageState.Running);
		Assert.True(flow.TransitionTo(StageState.Boss));
		Assert.True(flow.TransitionTo(StageState.Cleared));
	}
}
