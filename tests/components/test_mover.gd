extends TestCase


func _rig(speed: float) -> Array:
	var stats := StatBlock.new()
	stats.move_speed = speed
	var actor := ActorFixture.bare_actor(self, stats)
	var body := CharacterBody2D.new()
	actor.add_child(body)
	var mover := Mover.new()
	actor.add_child(mover)
	mover.bind(body, ActorFixture.asc_of(actor))
	return [body, mover]


func test_velocity_follows_direction_and_asc_speed() -> void:
	var rig := _rig(100.0)
	var body: CharacterBody2D = rig[0]
	var mover: Mover = rig[1]
	mover.set_direction(Vector2.RIGHT)
	mover.step(0.1)
	assert_eq(body.velocity, Vector2(100, 0))
	mover.set_direction(Vector2.ZERO)
	mover.step(0.1)
	assert_eq(body.velocity, Vector2.ZERO)


func test_diagonal_input_is_not_faster() -> void:
	var rig := _rig(100.0)
	var mover: Mover = rig[1]
	mover.set_direction(Vector2(1, 1))
	mover.step(0.1)
	assert_almost_eq((rig[0] as CharacterBody2D).velocity.length(), 100.0, 0.01)


func test_dash_overrides_velocity_then_finishes() -> void:
	var rig := _rig(100.0)
	var body: CharacterBody2D = rig[0]
	var mover: Mover = rig[1]
	var finished: Array = []
	mover.dash_finished.connect(func() -> void: finished.append(true))
	mover.set_direction(Vector2.UP)
	mover.dash(Vector2.RIGHT, 400.0, 0.2)
	assert_true(mover.is_dashing())
	mover.step(0.1)
	assert_eq(body.velocity, Vector2(400, 0))
	mover.step(0.15)
	assert_false(mover.is_dashing())
	assert_eq(finished.size(), 1)
	mover.step(0.1)
	assert_eq(body.velocity, Vector2(0, -100), "back to walking")


func test_dash_without_direction_uses_last_direction() -> void:
	var rig := _rig(100.0)
	var body: CharacterBody2D = rig[0]
	var mover: Mover = rig[1]
	mover.set_direction(Vector2.LEFT)
	mover.set_direction(Vector2.ZERO)
	mover.dash(Vector2.ZERO, 300.0, 0.2)
	mover.step(0.05)
	assert_eq(body.velocity, Vector2(-300, 0))
