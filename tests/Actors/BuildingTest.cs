using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Actors;

public class BuildingTest : TestCase
{
	private const string ScenePath = "res://src/Actors/Building/building.tscn";

	private Building MakeBuilding(float maxHealth)
	{
		var building = Adopt(GD.Load<PackedScene>(ScenePath).Instantiate<Building>());
		building.Setup(DefFactory.Texture(64, 64), new StatBlock { MaxHealth = maxHealth });
		return building;
	}

	private void Hit(Building building, float amount)
	{
		ActorEntity attacker = ActorFixture.BareActor(this, new StatBlock());
		DamageEffects.Hit(attacker, building.Entity, amount);
	}

	public void TestSetupAppliesStatsAndJoinsTheBuildingGroup()
	{
		Building building = MakeBuilding(300f);
		Assert.AlmostEq(building.Health.Current(), 300.0);
		Assert.True(building.IsInGroup(Groups.Building));
	}

	public void TestHurtboxSitsOnTheAllyHurtLayerAndProtectsTheBuildingsEntity()
	{
		Building building = MakeBuilding(300f);
		Assert.Eq((int)building.Hurtbox.CollisionLayer, PhysicsLayers.AllyHurt);
		Assert.True(building.Hurtbox.Entity == building.Entity);
		Assert.True(building.Hurtbox.Actor == building);
	}

	public void TestLethalDamageEmitsDestroyedOnce()
	{
		Building building = MakeBuilding(100f);
		int destroyed = 0;
		building.Destroyed += () => destroyed++;
		Hit(building, 60f);
		Assert.Eq(destroyed, 0);
		Hit(building, 60f);
		Hit(building, 60f);
		Assert.Eq(destroyed, 1);
	}
}
