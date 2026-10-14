using System.Collections.Generic;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class MoverTest : TestCase
{
	private (CharacterBody2D Body, Mover Mover, ActorEntity Entity) Rig(float speed)
	{
		ActorEntity entity = ActorFixture.BareActor(this, new StatBlock { MoveSpeed = speed });
		var body = Adopt(new CharacterBody2D());
		var mover = Adopt(new Mover());
		mover.SetPhysicsProcess(false);
		mover.Bind(body, entity);
		return (body, mover, entity);
	}

	public void TestVelocityFollowsDirectionAndEntitySpeed()
	{
		var (body, mover, _) = Rig(100f);
		mover.SetDirection(Vector2.Right);
		mover.Step(0.1);
		Assert.Eq(body.Velocity, new Vector2(100, 0));
		mover.SetDirection(Vector2.Zero);
		mover.Step(0.1);
		Assert.Eq(body.Velocity, Vector2.Zero);
	}

	public void TestDiagonalInputIsNotFaster()
	{
		var (body, mover, _) = Rig(100f);
		mover.SetDirection(new Vector2(1, 1));
		mover.Step(0.1);
		Assert.AlmostEq(body.Velocity.Length(), 100.0, 0.01);
	}

	public void TestDashOverridesVelocityThenFinishes()
	{
		var (body, mover, _) = Rig(100f);
		var finished = new List<bool>();
		mover.DashFinished += () => finished.Add(true);
		mover.SetDirection(Vector2.Up);
		mover.Dash(Vector2.Right, 400f, 0.2f);
		Assert.True(mover.IsDashing);
		mover.Step(0.1);
		Assert.Eq(body.Velocity, new Vector2(400, 0));
		mover.Step(0.15);
		Assert.False(mover.IsDashing);
		Assert.Eq(finished.Count, 1);
		mover.Step(0.1);
		Assert.Eq(body.Velocity, new Vector2(0, -100), "back to walking");
	}

	public void TestDashWithoutDirectionUsesTheLastDirection()
	{
		var (body, mover, _) = Rig(100f);
		mover.SetDirection(Vector2.Left);
		mover.SetDirection(Vector2.Zero);
		mover.Dash(Vector2.Zero, 300f, 0.2f);
		mover.Step(0.05);
		Assert.Eq(body.Velocity, new Vector2(-300, 0));
	}

	public void TestStepBeforeBindOrAfterTheEntityWasFreedIsANoOp()
	{
		var unbound = Adopt(new Mover());
		unbound.SetPhysicsProcess(false);
		unbound.Step(0.1);

		var (body, mover, entity) = Rig(100f);
		mover.SetDirection(Vector2.Right);
		entity.Free();
		mover.Step(0.1);
		Assert.Eq(body.Velocity, Vector2.Zero);
	}
}
