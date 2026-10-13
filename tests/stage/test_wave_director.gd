extends TestCase

const ENEMY_SCENE: PackedScene = preload("res://src/actors/enemy/enemy.tscn")
const BIG_ARENA: Rect2 = Rect2(-5000, -5000, 10000, 10000)


func _entry(time: float, count: int, label: String) -> WaveEntry:
	var entry := WaveEntry.new()
	entry.time = time
	entry.count = count
	entry.label = label
	entry.enemy = DefFactory.enemy(ENEMY_SCENE)
	return entry


func _rig(entries: Array[WaveEntry]) -> Array:
	var def := DefFactory.stage(10.0)
	def.spawn_radius = 300.0
	def.waves.entries.append_array(entries)
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var anchor := adopt(Node2D.new()) as Node2D
	targets.building = anchor
	targets.player = anchor
	var rng := RunRng.new(4)
	var factory := adopt(EnemyFactory.new()) as EnemyFactory
	factory.bind(layer, targets, rng)
	var clock := adopt(StageClock.new()) as StageClock
	var director := adopt(WaveDirector.new()) as WaveDirector
	director.bind(def, clock, factory, anchor, rng, BIG_ARENA)
	clock.start(def.duration)
	return [clock, factory, director]


func test_waves_fire_in_time_order_with_their_enemy_counts() -> void:
	var entries: Array[WaveEntry] = [_entry(3.0, 4, "late"), _entry(1.0, 3, "early")]
	var rig := _rig(entries)
	var clock: StageClock = rig[0]
	var factory: EnemyFactory = rig[1]
	var director: WaveDirector = rig[2]
	var labels: Array = []
	director.wave_started.connect(func(entry: WaveEntry) -> void: labels.append(entry.label))
	clock.step(1.5)
	assert_eq(factory.alive_count(), 3)
	clock.step(1.0)
	assert_eq(labels, ["early"])
	clock.step(1.0)
	assert_eq(labels, ["early", "late"])
	assert_eq(factory.alive_count(), 7)


func test_a_huge_time_step_fires_every_wave_exactly_once() -> void:
	var entries: Array[WaveEntry] = [_entry(1.0, 2, "a"), _entry(3.0, 2, "b")]
	var rig := _rig(entries)
	var director: WaveDirector = rig[2]
	var labels: Array = []
	director.wave_started.connect(func(entry: WaveEntry) -> void: labels.append(entry.label))
	(rig[0] as StageClock).step(100.0)
	assert_eq(labels, ["a", "b"])


func test_a_wave_at_time_zero_still_fires() -> void:
	var entries: Array[WaveEntry] = [_entry(0.0, 2, "opener")]
	var rig := _rig(entries)
	var labels: Array = []
	(rig[2] as WaveDirector).wave_started.connect(func(entry: WaveEntry) -> void: labels.append(entry.label))
	(rig[0] as StageClock).step(0.1)
	assert_eq(labels, ["opener"])


func test_mark_times_are_sorted_for_the_hud() -> void:
	var entries: Array[WaveEntry] = [_entry(3.0, 1, "b"), _entry(1.0, 1, "a")]
	var director: WaveDirector = _rig(entries)[2]
	assert_eq(director.mark_times(), PackedFloat32Array([1.0, 3.0]))
