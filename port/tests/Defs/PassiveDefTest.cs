using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class PassiveDefTest : TestCase
{
	public void TestWellFormedPassivesAreValid()
	{
		Assert.NoErrors(DefFactory.Might().Validate());
		Assert.NoErrors(DefFactory.Vitality().Validate());
	}

	public void TestMissingCurveIsReported()
	{
		var def = new PassiveDef { Stat = StatId.Attack, Operation = PassiveOperation.Percent, ByLevel = null };
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestWrongPointCountIsReported()
	{
		var def = new PassiveDef { Stat = StatId.Attack, Operation = PassiveOperation.Percent, ByLevel = DefFactory.CurveOf(0.1f, 0.2f, 0.3f) };
		Assert.True(def.Validate().Count >= 1);
	}

	public void TestPointsNotAtLevelsOneToSixAreReported()
	{
		// The domain must cover x = 2..7, otherwise Godot clamps the points back into [0, 1] and the test proves nothing.
		var curve = new Godot.Curve { MinDomain = 0f, MaxDomain = 10f };
		for (int i = 0; i < 6; i++)
		{
			curve.AddPoint(new Godot.Vector2(i + 2, 1f));
		}

		var def = new PassiveDef { Stat = StatId.Attack, Operation = PassiveOperation.Percent, ByLevel = curve };
		Assert.True(def.Validate().Count >= 1);
	}

	public void TestAllZeroCurveIsReported()
	{
		var def = new PassiveDef { Stat = StatId.Attack, Operation = PassiveOperation.Percent, ByLevel = DefFactory.CurveOf(0f, 0f, 0f, 0f, 0f, 0f) };
		Assert.True(def.Validate().Count >= 1);
	}
}
