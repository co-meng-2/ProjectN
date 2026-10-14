using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class ActorVisualTest : TestCase
{
	private static AnimationStrip MakeStrip(StringName animation, int frames, bool loop) => new()
	{
		Animation = animation,
		Texture = DefFactory.Texture(frames * 10, 10),
		FrameCount = frames,
		Loop = loop,
	};

	private static VisualDef MakeDef()
	{
		var def = new VisualDef();
		def.Strips.Add(MakeStrip(AnimNames.Idle, 6, true));
		def.Strips.Add(MakeStrip(AnimNames.Walk, 8, true));
		def.Strips.Add(MakeStrip(AnimNames.Hurt, 4, false));
		def.Strips.Add(MakeStrip(AnimNames.Death, 5, false));
		return def;
	}

	private ActorVisual MakeVisual()
	{
		var visual = Adopt(new ActorVisual());
		visual.Apply(MakeDef(), 2f, Colors.Red);
		return visual;
	}

	public void TestApplyBuildsFramesFromStrips()
	{
		ActorVisual visual = MakeVisual();
		Assert.Eq(visual.SpriteFrames.GetFrameCount(AnimNames.Idle), 6);
		Assert.Eq(visual.SpriteFrames.GetFrameCount(AnimNames.Walk), 8);
		Assert.False(visual.SpriteFrames.GetAnimationLoop(AnimNames.Hurt));
		Assert.Eq(visual.Animation, AnimNames.Idle);
		Assert.Eq(visual.Scale, new Vector2(2, 2));
		Assert.Eq(visual.Modulate, Colors.Red);
	}

	public void TestSetMovingSwitchesBetweenWalkAndIdle()
	{
		ActorVisual visual = MakeVisual();
		visual.SetMoving(true);
		Assert.Eq(visual.Animation, AnimNames.Walk);
		visual.SetMoving(false);
		Assert.Eq(visual.Animation, AnimNames.Idle);
	}

	public void TestOneShotIsNotInterruptedByLocomotion()
	{
		ActorVisual visual = MakeVisual();
		visual.PlayOnce(AnimNames.Hurt);
		visual.SetMoving(true);
		Assert.Eq(visual.Animation, AnimNames.Hurt);
	}

	public void TestMissingOneShotAnimationIsIgnored()
	{
		ActorVisual visual = MakeVisual();
		visual.PlayOnce(AnimNames.Attack);
		Assert.Eq(visual.Animation, AnimNames.Idle);
	}

	public void TestFaceFlipsHorizontallyOnlyWithRealInput()
	{
		ActorVisual visual = MakeVisual();
		visual.Face(-1f);
		Assert.True(visual.FlipH);
		visual.Face(0f);
		Assert.True(visual.FlipH, "zero input keeps the facing");
		visual.Face(1f);
		Assert.False(visual.FlipH);
	}

	public void TestApplyAgainResetsOneShotAndMovingState()
	{
		ActorVisual visual = MakeVisual();
		visual.PlayOnce(AnimNames.Death);
		visual.Apply(MakeDef(), 1f);
		visual.SetMoving(true);
		Assert.Eq(visual.Animation, AnimNames.Walk);
	}

	public void TestDeathIsTerminal()
	{
		ActorVisual visual = MakeVisual();
		visual.PlayOnce(AnimNames.Death);
		Color tint = visual.Modulate;
		visual.PlayOnce(AnimNames.Hurt);
		visual.FlashHit();
		Assert.Eq(visual.Animation, AnimNames.Death);
		Assert.Eq(visual.Modulate, tint);
	}
}
