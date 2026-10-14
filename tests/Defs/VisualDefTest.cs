using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class VisualDefTest : TestCase
{
	public void TestValidVisualHasNoErrors()
	{
		Assert.NoErrors(DefFactory.Visual().Validate(), "factory visual");
	}

	public void TestMissingIdleStripIsAnError()
	{
		VisualDef def = DefFactory.Visual();
		def.Strips[0].Animation = AnimNames.Walk;
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestStripWidthMustDivideByFrameCount()
	{
		VisualDef def = DefFactory.Visual();
		def.Strips[0].FrameCount = 7;
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestZeroFrameCountIsAnErrorWithoutThrowing()
	{
		VisualDef def = DefFactory.Visual();
		def.Strips[0].FrameCount = 0;
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestNonPositiveFpsIsAnError()
	{
		VisualDef def = DefFactory.Visual();
		def.Strips[0].Fps = 0f;
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestDuplicateAnimationNamesAreReportedOncePerName()
	{
		VisualDef def = DefFactory.Visual();
		def.Strips.Add((AnimationStrip)def.Strips[0].Duplicate());
		def.Strips.Add((AnimationStrip)def.Strips[0].Duplicate());
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestNullTextureIsAnError()
	{
		VisualDef def = DefFactory.Visual();
		def.Strips[0].Texture = null;
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestFindStripReturnsNullWhenAbsent()
	{
		Assert.Null(DefFactory.Visual().FindStrip(AnimNames.Death));
	}

	public void TestSpriteFramesAreBuiltFromStripsAndShared()
	{
		VisualDef def = DefFactory.Visual();
		Godot.SpriteFrames frames = def.SpriteFrames();
		Assert.Eq(frames.GetFrameCount(AnimNames.Idle), 6);
		Assert.False(frames.HasAnimation("default"));
		Assert.True(ReferenceEquals(def.SpriteFrames(), frames), "memoised: one SpriteFrames per def");
	}
}
