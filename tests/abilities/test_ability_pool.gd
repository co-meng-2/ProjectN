extends TestCase


func _rig() -> Dictionary:
	var common := DefFactory.ability(&"common_one")
	var rare := DefFactory.ability(&"rare_one", null, null, Grade.Value.RARE)
	var epic := DefFactory.ability(&"epic_one", null, null, Grade.Value.EPIC)
	var defs: Array[AbilityDef] = [common, rare, epic]
	return {"pool": AbilityPool.new(defs), "common": common, "rare": rare, "epic": epic}


func test_defs_for_includes_everything_at_or_below_the_grade() -> void:
	var rig := _rig()
	var pool: AbilityPool = rig["pool"]
	var at_common := pool.defs_for(Grade.Value.COMMON)
	assert_eq(at_common.size(), 1)
	assert_true(at_common.has(rig["common"]))
	var at_rare := pool.defs_for(Grade.Value.RARE)
	assert_eq(at_rare.size(), 2)
	assert_true(at_rare.has(rig["rare"]))
	assert_false(at_rare.has(rig["epic"]))
	assert_eq(pool.defs_for(Grade.Value.LEGENDARY).size(), 3)


func test_none_and_revealed_have_no_pool() -> void:
	var pool: AbilityPool = _rig()["pool"]
	assert_true(pool.defs_for(Grade.Value.NONE).is_empty())
	assert_true(pool.defs_for(Grade.Value.REVEALED).is_empty())
	assert_null(pool.roll(Grade.Value.NONE, RunRng.new(1)))
	assert_null(pool.roll(Grade.Value.REVEALED, RunRng.new(1)))


func test_roll_is_seeded_and_only_returns_pool_members() -> void:
	var rig := _rig()
	var pool: AbilityPool = rig["pool"]
	var first: Array = []
	var second: Array = []
	var rng_a := RunRng.new(5)
	var rng_b := RunRng.new(5)
	for i in 20:
		first.append(pool.roll(Grade.Value.RARE, rng_a))
		second.append(pool.roll(Grade.Value.RARE, rng_b))
	assert_eq(first, second, "same seed, same sequence")
	for def in first:
		assert_true(def == rig["common"] or def == rig["rare"], "only RARE-pool members")
	assert_true(first.has(rig["common"]) and first.has(rig["rare"]), "both members appear over 20 rolls")


func test_roll_prefers_a_different_def_than_the_excluded_one() -> void:
	var rig := _rig()
	var pool: AbilityPool = rig["pool"]
	var rng := RunRng.new(8)
	for i in 20:
		assert_eq(pool.roll(Grade.Value.RARE, rng, rig["common"]), rig["rare"])


func test_roll_with_only_the_excluded_def_returns_it() -> void:
	var rig := _rig()
	var pool: AbilityPool = rig["pool"]
	assert_eq(pool.roll(Grade.Value.COMMON, RunRng.new(8), rig["common"]), rig["common"])


func test_empty_pool_rolls_null_and_nulls_are_skipped() -> void:
	var nothing: Array[AbilityDef] = []
	assert_null(AbilityPool.new(nothing).roll(Grade.Value.COMMON, RunRng.new(1)))
	var with_hole: Array[AbilityDef] = [null, DefFactory.ability(&"a")]
	var pool := AbilityPool.new(with_hole)
	assert_eq(pool.defs_for(Grade.Value.COMMON).size(), 1)
	assert_not_null(pool.roll(Grade.Value.COMMON, RunRng.new(1)))
