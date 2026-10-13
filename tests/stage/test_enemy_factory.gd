extends TestCase

const ENEMY_SCENE: PackedScene = preload("res://src/actors/enemy/enemy.tscn")


func _rig() -> Array:
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var building := adopt(Node2D.new()) as Node2D
	building.global_position = Vector2(0, 500)
	var player := adopt(Node2D.new()) as Node2D
	player.global_position = Vector2(500, 0)
	targets.building = building
	targets.player = player
	var factory := adopt(EnemyFactory.new()) as EnemyFactory
	factory.bind(layer, targets, RunRng.new(1))
	return [factory, layer]


func test_spawn_places_a_configured_enemy_in_the_layer() -> void:
	var rig := _rig()
	var factory: EnemyFactory = rig[0]
	var layer: Node2D = rig[1]
	var spawned: Array = []
	factory.spawned.connect(func(enemy: Enemy) -> void: spawned.append(enemy))
	var enemy := factory.spawn(DefFactory.enemy(ENEMY_SCENE), Vector2(30, 40))
	assert_eq(enemy.get_parent(), layer)
	assert_eq(enemy.global_position, Vector2(30, 40))
	assert_eq(spawned, [enemy])
	assert_eq(factory.alive_count(), 1)


func test_dead_enemies_no_longer_count_as_alive() -> void:
	var rig := _rig()
	var factory: EnemyFactory = rig[0]
	var enemy := factory.spawn(DefFactory.enemy(ENEMY_SCENE), Vector2.ZERO)
	var killer := ActorFixture.bare_actor(self, StatBlock.new())
	var spec := EffectBuilder.damage(killer, killer, load("res://data/effects/damage_basic.tres"), 9999.0)
	ActorFixture.asc_of(killer).apply_effect_spec_to_target(spec, enemy.get_node("AbilitySystemComponent"))
	assert_eq(factory.alive_count(), 0)


func test_enemies_chase_the_target_of_their_policy() -> void:
	var rig := _rig()
	var factory: EnemyFactory = rig[0]
	var def := DefFactory.enemy(ENEMY_SCENE)
	def.target_policy = EnemyDef.TargetPolicy.PLAYER
	var enemy := factory.spawn(def, Vector2.ZERO)
	await wait_seconds(0.3)
	assert_true(enemy.global_position.x > 3.0, "moved toward the player on +x")
	assert_almost_eq(enemy.global_position.y, 0.0, 0.5)
