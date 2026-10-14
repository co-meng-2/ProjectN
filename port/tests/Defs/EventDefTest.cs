using Godot;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class EventDefTest : TestCase
{
	private static PackedScene Scene() => DefFactory.Packed(new Node2D());

	public void TestRewardMustPaySomethingAndNothingNegative()
	{
		Assert.Eq(new RewardDef().Validate().Count, 1, "all zero pays nothing");
		Assert.NoErrors(DefFactory.Reward(0, 0, 1).Validate(), "a single ticket is a reward");
		Assert.Eq(DefFactory.Reward(150, -1, 1).Validate().Count, 1, "negative stone");
	}

	public void TestValidEventDefReportsNoErrors()
	{
		Assert.NoErrors(DefFactory.Event(Scene(), 25f).Validate(), "event");
	}

	public void TestEventDefNeedsIdNameSceneLimitAndReward()
	{
		Assert.Eq(new EventDef().Validate().Count, 5, string.Join(", ", new EventDef().Validate()));
	}

	public void TestRewardErrorsAreReportedWithTheEvent()
	{
		EventDef def = DefFactory.Event(Scene());
		def.Reward = DefFactory.Reward(-5, 0, 0);
		var errors = def.Validate();
		Assert.Eq(errors.Count, 1);
		Assert.True(errors[0].Contains("probe"), "names the event");
	}

	public void TestControllerRootMustBeANode2D()
	{
		Assert.Eq(DefFactory.Event(DefFactory.Packed(new Node())).Validate().Count, 1, "a plain Node cannot be placed in the world");
	}

	public void TestControllerRootMayRejectItself()
	{
		var root = new RejectingNode2D { RejectReason = "probe says no" };
		var errors = DefFactory.Event(DefFactory.Packed(root)).Validate();
		Assert.Eq(errors.Count, 1);
		Assert.True(errors[0].Contains("probe says no"));
	}
}
