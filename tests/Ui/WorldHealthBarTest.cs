using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;
using VampireSurvivor.Ui;

namespace VampireSurvivor.Tests.Ui;

public class WorldHealthBarTest : TestCase
{
	private sealed record Rig(WorldHealthBar Bar, Node2D Anchor, ActorEntity Entity, Health Health);

	private Rig MakeRig(float maxHealth = 50f)
	{
		ActorEntity entity = ActorFixture.BareActor(this, new StatBlock { MaxHealth = maxHealth });
		var health = new Health();
		entity.AddChild(health);
		health.Bind(entity);
		Node2D anchor = Adopt(new Node2D());
		WorldHealthBar bar = Adopt(new WorldHealthBar());
		bar.Bind(health, anchor, new Vector2(0f, -60f));
		return new Rig(bar, anchor, entity, health);
	}

	public void TestFillRatioFollowsHealthChanged()
	{
		Rig rig = MakeRig();
		Assert.AlmostEq(rig.Bar.FillRatio(), 1.0);
		DamageEffects.Hit(ActorFixture.BareActor(this, new StatBlock()), rig.Entity, 25f);
		Assert.AlmostEq(rig.Bar.FillRatio(), 0.5);
	}

	public void TestFillRatioIsZeroWhenMaximumIsZero()
	{
		WorldHealthBar bar = Adopt(new WorldHealthBar());
		Assert.AlmostEq(bar.FillRatio(), 0.0);
		Health health = Adopt(new Health());
		bar.Bind(health, null, Vector2.Zero);
		health.EmitSignal(Health.SignalName.Changed, 10f, 0f);
		Assert.AlmostEq(bar.FillRatio(), 0.0);
	}

	public void TestFillRatioIsClamped()
	{
		Rig rig = MakeRig();
		rig.Health.EmitSignal(Health.SignalName.Changed, 80f, 50f);
		Assert.AlmostEq(rig.Bar.FillRatio(), 1.0);
		rig.Health.EmitSignal(Health.SignalName.Changed, -5f, 50f);
		Assert.AlmostEq(rig.Bar.FillRatio(), 0.0);
	}

	public void TestBindPositionsAtAnchorPlusOffsetAndFollowsTheAnchor()
	{
		Rig rig = MakeRig();
		rig.Anchor.GlobalPosition = new Vector2(100f, 40f);
		rig.Bar.Step();
		Assert.Eq(rig.Bar.GlobalPosition, new Vector2(100f, -20f));
		rig.Anchor.GlobalPosition = new Vector2(-30f, 10f);
		rig.Bar.Step();
		Assert.Eq(rig.Bar.GlobalPosition, new Vector2(-30f, -50f));
	}

	public void TestFreedAnchorHidesTheBarWithoutError()
	{
		Rig rig = MakeRig();
		Assert.True(rig.Bar.Visible);
		rig.Anchor.Free();
		rig.Bar.Step();
		Assert.False(rig.Bar.Visible);
	}

	public void TestFillColourShiftsGreenOrangeRed()
	{
		WorldHealthBar bar = Adopt(new WorldHealthBar());
		Assert.Eq(bar.ColourFor(1f), bar.FillColourHigh);
		Assert.Eq(bar.ColourFor(0.5f), bar.FillColourMid);
		Assert.Eq(bar.ColourFor(0.1f), bar.FillColourLow);
	}

	public void TestHidesAtFullHealthWhenNotAlwaysVisible()
	{
		Rig rig = MakeRig();
		rig.Bar.AlwaysVisible = false;
		rig.Bar.Step();
		Assert.False(rig.Bar.Visible);
		rig.Health.EmitSignal(Health.SignalName.Changed, 10f, 50f);
		rig.Bar.Step();
		Assert.True(rig.Bar.Visible);
	}
}
