using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Actors;

public class StatueTest : TestCase
{
	public const string StatueScene = "res://port/src/Actors/Statue/statue.tscn";

	private Statue MakeStatue(Vector2 at, float maxHealth = 60f)
	{
		Statue statue = Adopt(GD.Load<PackedScene>(StatueScene).Instantiate<Statue>());
		statue.GlobalPosition = at;
		statue.Setup(new StatBlock { MaxHealth = maxHealth, MoveSpeed = 0f });
		return statue;
	}

	public void TestSetupAppliesStatsAndMakesItATarget()
	{
		Statue statue = MakeStatue(new Vector2(240f, -130f), 60f);
		Assert.AlmostEq(statue.Health.Current(), 60.0);
		Assert.True(statue.IsInGroup(Groups.Enemies));
		TargetProvider targets = Adopt(new TargetProvider());
		Assert.True(targets.NearestEnemy(new Vector2(200f, -100f), 100f) == statue, "auto-aim sees it");
	}

	public async Task TestDestroyingItAnnouncesOnceAndFreesIt()
	{
		Statue statue = MakeStatue(new Vector2(-300f, 80f));
		var destroyed = new List<Statue>();
		statue.Destroyed += destroyed.Add;
		ActorFixture.Kill(this, statue.Entity);
		Assert.Eq(destroyed.Count, 1);
		Assert.True(destroyed[0] == statue);
		Assert.False(statue.IsInGroup(Groups.Enemies));
		Assert.True(statue.IsRemoved);
		ActorFixture.Kill(this, statue.Entity);
		Assert.Eq(destroyed.Count, 1, "dead once");
		await NextFrame();
		await NextFrame();
		Assert.False(GodotObject.IsInstanceValid(statue));
	}

	public async Task TestRemoveTakesItOutWithoutCountingAsDestroyed()
	{
		Statue statue = MakeStatue(new Vector2(90f, 410f));
		int destroyed = 0;
		statue.Destroyed += _ => destroyed++;
		statue.Remove();
		statue.Remove();
		Assert.Eq(destroyed, 0);
		Assert.False(statue.IsInGroup(Groups.Enemies));
		await NextFrame();
		await NextFrame();
		Assert.False(GodotObject.IsInstanceValid(statue));
	}

	public async Task TestAProjectileOnTheEnemyLayerCanDamageIt()
	{
		Statue statue = MakeStatue(new Vector2(500f, 300f));
		ActorEntity shooter = ActorFixture.BareActor(this, new StatBlock());
		Projectile projectile = Adopt(GD.Load<PackedScene>("res://port/src/Actors/Projectile/projectile.tscn").Instantiate<Projectile>());
		projectile.GlobalPosition = new Vector2(490f, 284f);
		projectile.Launch(GD.Load<ProjectileDef>("res://port/data/projectiles/arrow.tres"), shooter, 12f, Vector2.Right);
		await Settle();
		await NextPhysicsFrame();
		Assert.AlmostEq(statue.Health.Current(), 48.0);
	}
}
