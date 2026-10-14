using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Actors;

public class ProjectileTest : TestCase
{
	private const string ScenePath = "res://port/src/Actors/Projectile/projectile.tscn";

	private static ProjectileDef MakeDef(int pierce = 0, float lifetime = 1f) => new()
	{
		Texture = DefFactory.Texture(32, 32),
		Speed = 50f,
		Lifetime = lifetime,
		Pierce = pierce,
		HitRadius = 8f,
	};

	private ActorEntity MakeShooter() => ActorFixture.BareActor(this, new StatBlock());

	private Projectile Fire(ActorEntity shooter, ProjectileDef def)
	{
		var projectile = Adopt(GD.Load<PackedScene>(ScenePath).Instantiate<Projectile>());
		projectile.Launch(def, shooter, 10f, Vector2.Right);
		return projectile;
	}

	private static double HealthOf(ActorEntity entity) => entity.Attributes[Stats.Health].DisplayValue;

	private static bool Consumed(Projectile projectile) => !GodotObject.IsInstanceValid(projectile) || projectile.IsQueuedForDeletion();

	public async Task TestProjectileDamagesAVictimAndIsConsumed()
	{
		ActorEntity shooter = MakeShooter();
		var (victim, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		Projectile projectile = Fire(shooter, MakeDef());
		await Settle();
		await NextPhysicsFrame();
		Assert.AlmostEq(HealthOf(victim), 90.0);
		Assert.True(Consumed(projectile));
	}

	public async Task TestWithoutPierceHitsOnlyOneOfTwoOverlappingVictims()
	{
		ActorEntity shooter = MakeShooter();
		var (first, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		var (second, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		Fire(shooter, MakeDef(0));
		await Settle();
		await NextPhysicsFrame();
		int damaged = (HealthOf(first) < 100.0 ? 1 : 0) + (HealthOf(second) < 100.0 ? 1 : 0);
		Assert.Eq(damaged, 1);
	}

	public async Task TestWithPierceHitsBothAndNeverHitsAThirdTime()
	{
		ActorEntity shooter = MakeShooter();
		var (first, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		var (second, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		var (third, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		Fire(shooter, MakeDef(1));
		await Settle();
		await NextPhysicsFrame();
		Assert.Eq((HealthOf(first) < 100.0 ? 1 : 0) + (HealthOf(second) < 100.0 ? 1 : 0) + (HealthOf(third) < 100.0 ? 1 : 0), 2,
			"pierce 1 = two hits in total");
	}

	public async Task TestAFreedShooterMeansNoDamageButTheProjectileIsStillConsumed()
	{
		ActorEntity shooter = MakeShooter();
		var (victim, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		Projectile projectile = Fire(shooter, MakeDef());
		shooter.Free();
		await Settle();
		await NextPhysicsFrame();
		Assert.AlmostEq(HealthOf(victim), 100.0, 0.001, "no damage without a living source");
		Assert.True(Consumed(projectile));
	}

	public async Task TestProjectileExpiresAfterItsLifetime()
	{
		Projectile projectile = Fire(MakeShooter(), MakeDef(0, 0.1f));
		await WaitSeconds(0.4);
		Assert.True(Consumed(projectile));
	}

	public async Task TestProjectileFliesAlongItsDirection()
	{
		Projectile projectile = Fire(MakeShooter(), MakeDef());
		Vector2 start = projectile.GlobalPosition;
		await WaitSeconds(0.2);
		Assert.True(projectile.GlobalPosition.X > start.X + 3f);
		Assert.AlmostEq(projectile.GlobalPosition.Y, start.Y, 0.01);
	}

	public async Task TestAnAllyHittingProjectileDamagesAlliesAndPassesEnemies()
	{
		ActorEntity shooter = MakeShooter();
		ProjectileDef def = MakeDef(1);
		def.Hits = ProjectileTeam.Allies;
		var (ally, _) = ActorFixture.HurtTarget(this, new Vector2(4, 0), PhysicsLayers.AllyHurt);
		var (enemy, _) = ActorFixture.HurtTarget(this, new Vector2(-4, 0), PhysicsLayers.EnemyHurt);
		Projectile projectile = Fire(shooter, def);
		Assert.Eq((int)projectile.Hitbox.CollisionMask, PhysicsLayers.AllyHurt);
		await Settle();
		await NextPhysicsFrame();
		Assert.AlmostEq(HealthOf(ally), 90.0);
		Assert.AlmostEq(HealthOf(enemy), 100.0, 0.001, "pierce 1 would have hit it too if the mask matched");
	}

	public void TestLaunchResetsTheMaskFromTheDefSoProjectilesHitEnemiesByDefault()
	{
		var projectile = Adopt(GD.Load<PackedScene>(ScenePath).Instantiate<Projectile>());
		projectile.Hitbox.CollisionMask = (uint)PhysicsLayers.AllyHurt;
		projectile.Launch(MakeDef(), MakeShooter(), 10f, Vector2.Right);
		Assert.Eq((int)projectile.Hitbox.CollisionMask, PhysicsLayers.EnemyHurt);
	}
}
