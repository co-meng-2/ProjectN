using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Actors;

public class EnemyTest : TestCase
{
	private const string EnemyScene = "res://src/Actors/Enemy/enemy.tscn";
	private const string BuildingScene = "res://src/Actors/Building/building.tscn";

	private static EnemyDef MakeDef(float maxHealth = 30f, bool withDrop = false)
	{
		EnemyDef def = DefFactory.Enemy(GD.Load<PackedScene>(EnemyScene));
		def.Stats = new StatBlock { MaxHealth = maxHealth, MoveSpeed = 120f };
		def.ContactDamage = 10f;
		if (withDrop)
		{
			def.Drops = new DropTable();
			def.Drops.Entries.Add(new DropEntry { Chance = 1f, Icon = DefFactory.Texture(8, 8) });
		}

		return def;
	}

	private Enemy MakeEnemy(EnemyDef def, Node2D target, Vector2? at = null)
	{
		var enemy = Adopt(GD.Load<PackedScene>(EnemyScene).Instantiate<Enemy>());
		enemy.GlobalPosition = at ?? Vector2.Zero;
		enemy.Setup(def, target, new RunRng(5));
		return enemy;
	}

	private Building MakeBuilding(float maxHealth)
	{
		var building = Adopt(GD.Load<PackedScene>(BuildingScene).Instantiate<Building>());
		building.Setup(DefFactory.Texture(64, 64), new StatBlock { MaxHealth = maxHealth });
		return building;
	}

	private void Kill(Enemy enemy)
	{
		ActorEntity killer = ActorFixture.BareActor(this, new StatBlock());
		DamageEffects.Hit(killer, enemy.Entity, 9999f);
	}

	public void TestSetupAppliesStatsAndJoinsTheEnemyGroup()
	{
		var target = Adopt(new Node2D());
		Enemy enemy = MakeEnemy(MakeDef(45f), target);
		Assert.AlmostEq(enemy.Health.Current(), 45.0);
		Assert.True(enemy.IsInGroup(Groups.Enemies));
	}

	public async Task TestEnemyWalksTowardItsTarget()
	{
		var target = Adopt(new Node2D { GlobalPosition = new Vector2(300, 0) });
		Enemy enemy = MakeEnemy(MakeDef(), target);
		await WaitSeconds(0.3);
		Assert.True(enemy.GlobalPosition.X > 5f, "moved toward +x");
		Assert.AlmostEq(enemy.GlobalPosition.Y, 0.0, 0.5);
	}

	public async Task TestEnemyWithoutAValidTargetStandsStill()
	{
		var target = Adopt(new Node2D());
		Enemy enemy = MakeEnemy(MakeDef(), target);
		target.Free();
		await WaitSeconds(0.2);
		Assert.AlmostEq(enemy.GlobalPosition.Length(), 0.0, 0.5);
	}

	public async Task TestEnemyDamagesAnOverlappingAllyHurtbox()
	{
		Building building = MakeBuilding(200f);
		MakeEnemy(MakeDef(), building);
		await WaitSeconds(0.4);
		Assert.True(building.Health.Current() < 200f);
	}

	public async Task TestDeathEmitsSignalsLeavesTheGroupAndFreesItself()
	{
		var target = Adopt(new Node2D());
		Enemy enemy = MakeEnemy(MakeDef(30f, withDrop: true), target, new Vector2(10, 20));
		var deaths = new List<Enemy>();
		var drops = new List<(DropRoll, Vector2)>();
		enemy.Died += found => deaths.Add(found);
		enemy.LootDropped += (roll, position) => drops.Add((roll, position));
		Kill(enemy);
		Assert.SequenceEq(deaths, new[] { enemy });
		Assert.Eq(drops.Count, 1);
		Assert.False(enemy.IsInGroup(Groups.Enemies));
		await WaitSeconds(0.9);
		Assert.False(GodotObject.IsInstanceValid(enemy), "freed after the death delay");
	}

	public void TestDeadEnemyIsNotTargetable()
	{
		var provider = Adopt(new TargetProvider());
		var target = Adopt(new Node2D());
		Enemy enemy = MakeEnemy(MakeDef(), target, new Vector2(40, 0));
		Assert.True(provider.NearestEnemy(Vector2.Zero, 200f) == enemy);
		Kill(enemy);
		Assert.Null(provider.NearestEnemy(Vector2.Zero, 200f));
	}

	public async Task TestDeadEnemyStopsDealingDamage()
	{
		Building building = MakeBuilding(200f);
		EnemyDef def = MakeDef();
		def.ContactInterval = 0.2f;
		Enemy enemy = MakeEnemy(def, building);
		await WaitSeconds(0.3);
		Assert.True(building.Health.Current() < 200f, "enemy hit the building before dying");
		Kill(enemy);
		float healthAtDeath = building.Health.Current();
		await WaitSeconds(0.5);
		Assert.AlmostEq(building.Health.Current(), healthAtDeath);
	}

	public async Task TestEnemyWithoutATargetStandsStill()
	{
		Enemy enemy = Adopt(GD.Load<PackedScene>(EnemyScene).Instantiate<Enemy>());
		enemy.GlobalPosition = new Vector2(40f, 0f);
		enemy.Setup(MakeDef(), null, new RunRng(5));
		await WaitSeconds(0.2);
		Assert.AlmostEq(enemy.GlobalPosition.X, 40.0, 0.01, "no target: no movement and no exception");
	}
}
