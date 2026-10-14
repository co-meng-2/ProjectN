using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class EnemyFactoryTest : TestCase
{
	private const string EnemyScene = "res://src/Actors/Enemy/enemy.tscn";

	private (EnemyFactory Factory, Node2D Layer) MakeRig()
	{
		Node2D layer = Adopt(new Node2D());
		TargetProvider targets = Adopt(new TargetProvider());
		targets.Building = Adopt(new Node2D { Position = new Vector2(0f, 500f) });
		targets.Player = Adopt(new Node2D { Position = new Vector2(500f, 0f) });
		EnemyFactory factory = Adopt(new EnemyFactory());
		factory.Bind(layer, targets, new RunRng(1));
		return (factory, layer);
	}

	private static EnemyDef MakeDef() => DefFactory.Enemy(GD.Load<PackedScene>(EnemyScene));

	public void TestSpawnPlacesAConfiguredEnemyInTheLayer()
	{
		(EnemyFactory factory, Node2D layer) = MakeRig();
		var spawned = new List<Enemy>();
		factory.Spawned += spawned.Add;
		Enemy enemy = factory.Spawn(MakeDef(), new Vector2(30f, 40f));
		Assert.True(enemy.GetParent() == layer, "added to the enemy layer");
		Assert.Eq(enemy.GlobalPosition, new Vector2(30f, 40f));
		Assert.Eq(spawned.Count, 1);
		Assert.True(spawned[0] == enemy);
		Assert.Eq(factory.AliveCount, 1);
	}

	public void TestDeadEnemiesNoLongerCountAsAlive()
	{
		(EnemyFactory factory, _) = MakeRig();
		Enemy enemy = factory.Spawn(MakeDef(), Vector2.Zero);
		ActorFixture.Kill(this, enemy.Entity);
		Assert.Eq(factory.AliveCount, 0);
	}

	public async Task TestEnemiesChaseTheTargetOfTheirPolicy()
	{
		(EnemyFactory factory, _) = MakeRig();
		EnemyDef def = MakeDef();
		def.TargetPolicy = TargetPolicy.Player;
		Enemy enemy = factory.Spawn(def, Vector2.Zero);
		await WaitSeconds(0.3);
		Assert.True(enemy.GlobalPosition.X > 3f, "moved toward the player on +x");
		Assert.AlmostEq(enemy.GlobalPosition.Y, 0.0, 0.5);
	}
}
