using VampireSurvivor.Components;

namespace VampireSurvivor.Tests.Components;

public class LifetimeTest : TestCase
{
	public void TestExpiresOnceWhenTimeRunsOut()
	{
		var lifetime = Adopt(new Lifetime());
		lifetime.SetPhysicsProcess(false);
		int expired = 0;
		lifetime.Expired += () => expired++;
		lifetime.Restart(1f);
		lifetime.Step(0.5);
		Assert.Eq(expired, 0);
		lifetime.Step(0.6);
		lifetime.Step(5.0);
		Assert.Eq(expired, 1);
	}

	public void TestRestartRearmsTheCountdown()
	{
		var lifetime = Adopt(new Lifetime());
		lifetime.SetPhysicsProcess(false);
		int expired = 0;
		lifetime.Expired += () => expired++;
		lifetime.Restart(0.1f);
		lifetime.Step(0.2);
		lifetime.Restart(0.1f);
		lifetime.Step(0.2);
		Assert.Eq(expired, 2);
	}
}
