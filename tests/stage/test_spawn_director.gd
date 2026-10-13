extends TestCase

const ENEMY_SCENE: PackedScene = preload("res://src/actors/enemy/enemy.tscn")
const BIG_ARENA: Rect2 = Rect2(-5000, -5000, 10000, 10000)


func _rig(stage_def: StageDef) -> Array:
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var anchor := adopt(Node2D.new()) as Node2D
	targets.building = anchor
	targets.player = anchor
	var rng := RunRng.new(2)
	var factory := adopt(EnemyFactory.new()) as EnemyFactory
	factory.bind(layer, targets, rng)
	var clock := adopt(StageClock.new()) as StageClock
	var director := adopt(SpawnDirector.new()) as SpawnDirector
	director.bind(stage_def, clock, factory, anchor, rng, BIG_ARENA)
	clock.start(stage_def.duration)
	return [clock, factory, director]


func _stage(cap: int) -> StageDef:
	var def := DefFactory.stage(100.0)
	def.trickle_enemies.append(DefFactory.enemy(ENEMY_SCENE))
	def.trickle_interval_start = 1.0
	def.trickle_interval_end = 1.0
	def.trickle_cap = cap
	def.spawn_radius = 300.0
	return def


func test_interval_interpolates_across_the_stage() -> void:
	var def := _stage(10)
	def.trickle_interval_start = 2.0
	def.trickle_interval_end = 0.5
	var director: SpawnDirector = _rig(def)[2]
	assert_almost_eq(director.current_interval(0.0), 2.0)
	assert_almost_eq(director.current_interval(50.0), 1.25)
	assert_almost_eq(director.current_interval(100.0), 0.5)


func test_spawns_on_the_ring_at_the_configured_interval_and_respects_the_cap() -> void:
	var rig := _rig(_stage(2))
	var clock: StageClock = rig[0]
	var factory: EnemyFactory = rig[1]
	clock.step(0.1)
	assert_eq(factory.alive_count(), 1)
	clock.step(0.5)
	assert_eq(factory.alive_count(), 1, "interval not elapsed")
	clock.step(1.0)
	assert_eq(factory.alive_count(), 2)
	clock.step(5.0)
	assert_eq(factory.alive_count(), 2, "cap reached")
	for node in get_tree_enemies():
		assert_almost_eq((node as Node2D).global_position.length(), 300.0, 0.5)


func test_empty_trickle_list_spawns_nothing_and_does_not_fail() -> void:
	var def := _stage(5)
	def.trickle_enemies.clear()
	var rig := _rig(def)
	(rig[0] as StageClock).step(3.0)
	assert_eq((rig[1] as EnemyFactory).alive_count(), 0)


func get_tree_enemies() -> Array[Node]:
	return host.get_tree().get_nodes_in_group(Groups.ENEMIES)


func test_trickle_continues_in_overtime_at_the_end_interval() -> void:
	var def := _stage(10)
	def.duration = 2.0
	def.trickle_interval_start = 3.0
	def.trickle_interval_end = 1.0
	var rig := _rig(def)
	var clock: StageClock = rig[0]
	var factory: EnemyFactory = rig[1]
	var director: SpawnDirector = rig[2]
	clock.set_physics_process(false)
	assert_almost_eq(director.current_interval(25.0), 1.0, 0.001, "clamped to the end interval")
	clock.step(2.5)
	assert_eq(factory.alive_count(), 1)
	clock.step(1.0)
	assert_eq(factory.alive_count(), 2, "spawns keep coming after the stage time")
	clock.step(1.0)
	assert_eq(factory.alive_count(), 3)
	assert_true(clock.elapsed > def.duration)
