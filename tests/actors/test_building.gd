extends TestCase

const BUILDING_SCENE: PackedScene = preload("res://src/actors/building/building.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _building(max_health: float) -> Building:
	var stats := StatBlock.new()
	stats.max_health = max_health
	var building := adopt(BUILDING_SCENE.instantiate()) as Building
	building.setup(DefFactory.texture(64, 64), stats)
	return building


func _hit(building: Building, amount: float) -> void:
	var stats := StatBlock.new()
	var attacker := ActorFixture.bare_actor(self, stats)
	var spec := EffectBuilder.damage(attacker, attacker, DAMAGE, amount)
	ActorFixture.asc_of(attacker).apply_effect_spec_to_target(spec, building.asc)


func test_setup_applies_stats_and_joins_the_building_group() -> void:
	var building := _building(300.0)
	assert_almost_eq(building.health.current(), 300.0)
	assert_true(building.is_in_group(Groups.BUILDING))


func test_hurtbox_sits_on_the_ally_hurt_layer() -> void:
	var building := _building(300.0)
	var hurtbox := building.get_node("Hurtbox") as Hurtbox
	assert_eq(hurtbox.collision_layer, PhysicsLayers.ALLY_HURT)
	assert_eq(hurtbox.asc, building.asc)


func test_lethal_damage_emits_destroyed_once() -> void:
	var building := _building(100.0)
	var destroyed: Array = []
	building.destroyed.connect(func() -> void: destroyed.append(true))
	_hit(building, 60.0)
	assert_eq(destroyed.size(), 0)
	_hit(building, 60.0)
	_hit(building, 60.0)
	assert_eq(destroyed.size(), 1)
