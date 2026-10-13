extends TestCase


func test_next_climbs_the_ladder_and_stops_at_revealed() -> void:
	assert_eq(Grade.next(Grade.Value.NONE), Grade.Value.COMMON)
	assert_eq(Grade.next(Grade.Value.UNIQUE), Grade.Value.LEGENDARY)
	assert_eq(Grade.next(Grade.Value.LEGENDARY), Grade.Value.REVEALED)
	assert_eq(Grade.next(Grade.Value.REVEALED), Grade.Value.REVEALED)


func test_level_maps_common_to_one_and_revealed_to_six() -> void:
	assert_almost_eq(Grade.level(Grade.Value.NONE), 0.0)
	assert_almost_eq(Grade.level(Grade.Value.COMMON), 1.0)
	assert_almost_eq(Grade.level(Grade.Value.EPIC), 3.0)
	assert_almost_eq(Grade.level(Grade.Value.REVEALED), 6.0)


func test_is_rollable_excludes_none_and_revealed() -> void:
	assert_false(Grade.is_rollable(Grade.Value.NONE))
	assert_true(Grade.is_rollable(Grade.Value.COMMON))
	assert_true(Grade.is_rollable(Grade.Value.LEGENDARY))
	assert_false(Grade.is_rollable(Grade.Value.REVEALED))


func test_every_grade_has_a_name_and_a_distinct_color() -> void:
	var colors: Array = []
	for grade in Grade.Value.values():
		assert_false(Grade.display_name(grade).is_empty(), "name for grade %d" % grade)
		assert_false(colors.has(Grade.color(grade)), "colour reused by %s" % Grade.display_name(grade))
		colors.append(Grade.color(grade))
	assert_eq(colors.size(), 7)
