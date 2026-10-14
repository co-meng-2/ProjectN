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
		var curve = new Godot.Curve();
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
