extends TestCase


func _area(area: Area2D, layer: int, mask: int) -> Area2D:
	area.collision_layer = layer
	area.collision_mask = mask
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 10.0
	shape.shape = circle
	area.add_child(shape)
	return adopt(area) as Area2D


func _pair() -> Array:
	var hitbox := _area(Hitbox.new(), 0, PhysicsLayers.ENEMY_HURT) as Hitbox
	var hurtbox := _area(Hurtbox.new(), PhysicsLayers.ENEMY_HURT, 0) as Hurtbox
	return [hitbox, hurtbox]


func test_hitbox_reports_overlapping_hurtbox() -> void:
	var pair := _pair()
	var hitbox: Hitbox = pair[0]
	var hurtbox: Hurtbox = pair[1]
	var entered: Array = []
	hitbox.hurtbox_entered.connect(func(found: Hurtbox) -> void: entered.append(found))
	await next_physics_frame()
	await next_physics_frame()
	assert_eq(entered, [hurtbox])
	assert_eq(hitbox.overlapping_hurtboxes(), [hurtbox])


func test_once_per_target_ignores_reentry_until_reset() -> void:
	var pair := _pair()
	var hitbox: Hitbox = pair[0]
	var hurtbox: Hurtbox = pair[1]
	var entered: Array = []
	hitbox.hurtbox_entered.connect(func(found: Hurtbox) -> void: entered.append(found))
	await next_physics_frame()
	await next_physics_frame()
	hurtbox.position = Vector2(1000, 0)
	await next_physics_frame()
	await next_physics_frame()
	hurtbox.position = Vector2.ZERO
	await next_physics_frame()
	await next_physics_frame()
	assert_eq(entered.size(), 1, "re-entry ignored")
	hurtbox.position = Vector2(1000, 0)
	await next_physics_frame()
	await next_physics_frame()
	hitbox.reset()
	hurtbox.position = Vector2.ZERO
	await next_physics_frame()
	await next_physics_frame()
	assert_eq(entered.size(), 2, "re-entry counted after reset")


func test_non_hurtbox_areas_are_ignored() -> void:
	var hitbox := _area(Hitbox.new(), 0, PhysicsLayers.ENEMY_HURT) as Hitbox
	_area(Area2D.new(), PhysicsLayers.ENEMY_HURT, 0)
	await next_physics_frame()
	await next_physics_frame()
	assert_true(hitbox.overlapping_hurtboxes().is_empty())
