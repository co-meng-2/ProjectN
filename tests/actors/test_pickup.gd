extends TestCase

const PICKUP_SCENE: PackedScene = preload("res://src/actors/pickup/pickup.tscn")


func _roll(kind: Wallet.Kind, amount: int) -> DropRoll:
	var roll := DropRoll.new()
	roll.kind = kind
	roll.amount = amount
	roll.icon = DefFactory.texture(32, 32)
	return roll


func _pickup(position: Vector2) -> Pickup:
	var pickup := adopt(PICKUP_SCENE.instantiate()) as Pickup
	pickup.global_position = position
	pickup.setup(_roll(Wallet.Kind.STONE, 4))
	return pickup


func test_setup_copies_kind_amount_and_fits_the_icon() -> void:
	var pickup := _pickup(Vector2.ZERO)
	assert_eq(pickup.kind, Wallet.Kind.STONE)
	assert_eq(pickup.amount, 4)
	assert_almost_eq((pickup.get_node("Sprite2D") as Sprite2D).scale.x, 0.5)


func test_pickup_without_target_stays_put() -> void:
	var pickup := _pickup(Vector2(30, 0))
	pickup.step(1.0)
	assert_eq(pickup.global_position, Vector2(30, 0))


func test_attracted_pickup_flies_to_the_target_and_reports_once() -> void:
	var target := adopt(Node2D.new()) as Node2D
	var pickup := _pickup(Vector2(100, 0))
	var reached: Array = []
	pickup.reached.connect(func(found: Pickup) -> void: reached.append(found))
	pickup.attract_to(target)
	for i in 40:
		pickup.step(0.05)
	assert_eq(reached, [pickup])
	assert_true(pickup.is_queued_for_deletion())


func test_collector_attracts_and_reports_collected_amounts() -> void:
	var anchor := adopt(Node2D.new()) as Node2D
	var collector := LootCollector.new()
	collector.collision_layer = 0
	collector.collision_mask = PhysicsLayers.PICKUP
	collector.monitorable = false
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 60.0
	shape.shape = circle
	collector.add_child(shape)
	anchor.add_child(collector)
	collector.bind(anchor)
	var collected: Array = []
	collector.collected.connect(func(kind: Wallet.Kind, amount: int) -> void: collected.append([kind, amount]))
	_pickup(Vector2(30, 0))
	await wait_seconds(0.6)
	assert_eq(collected, [[Wallet.Kind.STONE, 4]])


func test_pickup_leaving_and_reentering_the_magnet_is_collected_once() -> void:
	var anchor := adopt(Node2D.new()) as Node2D
	var collector := LootCollector.new()
	collector.collision_layer = 0
	collector.collision_mask = PhysicsLayers.PICKUP
	collector.monitorable = false
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 60.0
	shape.shape = circle
	collector.add_child(shape)
	anchor.add_child(collector)
	collector.bind(anchor)
	var collected: Array = []
	collector.collected.connect(func(kind: Wallet.Kind, amount: int) -> void: collected.append([kind, amount]))
	var pickup := _pickup(Vector2(50, 0))
	await next_physics_frame()
	await next_physics_frame()
	pickup.global_position = Vector2(400, 0)
	await next_physics_frame()
	await next_physics_frame()
	pickup.global_position = Vector2(50, 0)
	await wait_seconds(0.6)
	assert_eq(collected, [[Wallet.Kind.STONE, 4]])
