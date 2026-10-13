extends TestCase


func test_same_seed_gives_same_sequence() -> void:
	var a := RunRng.new(42)
	var b := RunRng.new(42)
	for i in 5:
		assert_eq(a.randf(), b.randf(), "draw %d" % i)


func test_pick_returns_null_for_empty_array() -> void:
	assert_null(RunRng.new(1).pick([]))


func test_pick_returns_an_element() -> void:
	var items := ["a", "b", "c"]
	assert_true(items.has(RunRng.new(1).pick(items)))


func test_weighted_index_never_picks_zero_weight() -> void:
	var rng := RunRng.new(7)
	var weights: Array[float] = [0.0, 0.0, 5.0, 0.0]
	for i in 30:
		assert_eq(rng.weighted_index(weights), 2)


func test_weighted_index_is_minus_one_without_positive_weight() -> void:
	var weights: Array[float] = [0.0, -1.0]
	assert_eq(RunRng.new(1).weighted_index(weights), -1)
	var empty: Array[float] = []
	assert_eq(RunRng.new(1).weighted_index(empty), -1)


func test_weighted_returns_matching_item() -> void:
	var weights: Array[float] = [0.0, 1.0]
	assert_eq(RunRng.new(3).weighted(["x", "y"], weights), "y")


func test_chance_extremes() -> void:
	var rng := RunRng.new(9)
	for i in 20:
		assert_false(rng.chance(0.0))
		assert_true(rng.chance(1.0))


func test_offset_in_circle_stays_within_radius() -> void:
	var rng := RunRng.new(5)
	for i in 50:
		assert_true(rng.offset_in_circle(10.0).length() <= 10.0001)
