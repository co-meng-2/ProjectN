using System;
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Core;

public class GradeTest : TestCase
{
	public void TestNextClimbsTheLadderAndStopsAtRevealed()
	{
		Assert.Eq(Grade.None.Next(), Grade.Common);
		Assert.Eq(Grade.Unique.Next(), Grade.Legendary);
		Assert.Eq(Grade.Legendary.Next(), Grade.Revealed);
		Assert.Eq(Grade.Revealed.Next(), Grade.Revealed);
	}

	public void TestLevelMapsCommonToOneAndRevealedToSix()
	{
		Assert.AlmostEq(Grade.None.Level(), 0.0);
		Assert.AlmostEq(Grade.Common.Level(), 1.0);
		Assert.AlmostEq(Grade.Epic.Level(), 3.0);
		Assert.AlmostEq(Grade.Revealed.Level(), 6.0);
	}

	public void TestIsRollableExcludesNoneAndRevealed()
	{
		Assert.False(Grade.None.IsRollable());
		Assert.True(Grade.Common.IsRollable());
		Assert.True(Grade.Legendary.IsRollable());
		Assert.False(Grade.Revealed.IsRollable());
	}

	public void TestEveryGradeHasANameAndADistinctColor()
	{
		var colors = new List<Color>();
		foreach (Grade grade in Enum.GetValues<Grade>())
		{
			Assert.False(grade.DisplayName().Length == 0, $"name for {grade}");
			Assert.False(colors.Contains(grade.Tint()), $"colour reused by {grade.DisplayName()}");
			colors.Add(grade.Tint());
		}

		Assert.Eq(colors.Count, 7);
	}
}
