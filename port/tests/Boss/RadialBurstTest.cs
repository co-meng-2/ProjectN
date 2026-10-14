using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Boss;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Actors;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Boss;

public class RadialBurstTest : TestCase
{
	public const string BurstPath = "res://port/data/bosses/patterns/radial_burst.tres";
	private static readonly Vector2 BossAt = new(40f, -30f);

	private sealed record Rig(Player Player, BossActor Boss, Node Layer);

	private Rig MakeRig(Vector2 playerAt)
	{
		Player player = ActorFixture.Player(this);
		player.GlobalPosition = playerAt;
		var burst = DefFactory.Fresh<RadialBurstDef>(BurstPath);
		BossActor boss = ActorFixture.Boss(this, player, DefFactory.Boss(GD.Load<PackedScene>(BossActorTest.BossScene), burst), BossAt);
		return new Rig(player, boss, player.Context!.ProjectileLayer);
	}

	public void TestFiresCountAllyHittingBoltsEvenlyAroundTheBoss()
	{
		Rig rig = MakeRig(new Vector2(600f, 600f));
		Assert.True(ActorFixture.Fire(rig.Boss.Patterns[0]));
		Assert.False(rig.Boss.IsCasting, "the burst completes at once");
		var bolts = ActorFixture.LiveChildren<Projectile>(rig.Layer);
		Assert.Eq(bolts.Count, 12);
		var angles = new List<float>();
		foreach (Projectile bolt in bolts)
		{
			Assert.Eq(bolt.GlobalPosition, BossAt);
			Assert.Eq(bolt.Hitbox.CollisionMask, (uint)PhysicsLayers.AllyHurt);
			angles.Add(Mathf.PosMod(bolt.Rotation, Mathf.Tau));
		}

		angles.Sort();
		for (int i = 1; i < angles.Count; i++)
		{
			Assert.AlmostEq(angles[i] - angles[i - 1], Mathf.Tau / 12f, 0.001, "even spacing");
		}
	}

	public async Task TestABoltDamagesThePlayerButNotTheBoss()
	{
		Rig rig = MakeRig(BossAt + new Vector2(45f, 0f));
		ActorFixture.Fire(rig.Boss.Patterns[0]);
		await WaitSeconds(0.4);
		Assert.AlmostEq(rig.Player.Health.Current(), 90.0, 0.001, "one bolt flies along +x into the player");
		Assert.AlmostEq(rig.Boss.Health.Current(), 500.0, 0.001, "bolts ignore the enemy hurt layer");
	}

	public void TestValidateRequiresAnAllyHittingProjectile()
	{
		var burst = DefFactory.Fresh<RadialBurstDef>(BurstPath);
		Assert.NoErrors(burst.Validate(), "as shipped");
		burst.Projectile = GD.Load<ProjectileDef>("res://port/data/projectiles/arrow.tres");
		Assert.Eq(burst.Validate().Count, 1, "the player's arrow hits enemies");
		burst.Count = 0;
		Assert.Eq(burst.Validate().Count, 2);
	}

	public void TestValidateRejectsASceneWhoseRootIsNotAProjectile()
	{
		var burst = DefFactory.Fresh<RadialBurstDef>(BurstPath);
		burst.ProjectileScene = DefFactory.Packed(new Node2D());
		Assert.Eq(burst.Validate().Count, 1);
	}
}
