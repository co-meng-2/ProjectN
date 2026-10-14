using VampireSurvivor.Defs;

namespace VampireSurvivor.Tests.Defs;

public class StatBlockTest : TestCase
{
	public void TestDefaultsAreValid()
	{
		Assert.NoErrors(new StatBlock().Validate());
	}

	public void TestNonPositiveMaxHealthIsRejected()
	{
		Assert.Eq(new StatBlock { MaxHealth = 0f }.Validate().Count, 1);
		Assert.Eq(new StatBlock { MaxHealth = -5f }.Validate().Count, 1);
	}

	public void TestNegativeMoveSpeedIsRejected()
	{
		Assert.Eq(new StatBlock { MoveSpeed = -1f }.Validate().Count, 1);
	}
}
