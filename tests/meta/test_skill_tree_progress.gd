extends TestCase

## a: 2 levels, costs 10/20. b: 1 level, cost 5, requires a.


func _setup(gem: int, levels: Dictionary = {}) -> Array:
	var tree := SkillTreeDef.new()
	tree.nodes = [DefFactory.skill_node(&"a"), DefFactory.skill_node(&"b", [&"a"], 1, PackedInt32Array([5]))]
	var save := SaveData.new()
	save.gem = gem
	for id: String in levels:
		save.set_level(StringName(id), levels[id])
	return [SkillTreeProgress.new(tree, save), save]


func test_a_node_without_requirements_is_available() -> void:
	var progress: SkillTreeProgress = _setup(0)[0]
	assert_eq(progress.status(0), SkillTreeProgress.Status.AVAILABLE)


func test_a_node_is_locked_until_every_requirement_has_a_level() -> void:
	var progress: SkillTreeProgress = _setup(0)[0]
	assert_eq(progress.status(1), SkillTreeProgress.Status.LOCKED)
	var unlocked: SkillTreeProgress = _setup(0, {"a": 1})[0]
	assert_eq(unlocked.status(1), SkillTreeProgress.Status.AVAILABLE)


func test_a_node_at_max_level_is_maxed() -> void:
	var progress: SkillTreeProgress = _setup(0, {"a": 2})[0]
	assert_eq(progress.status(0), SkillTreeProgress.Status.MAXED)
	assert_eq(progress.next_cost(0), -1)


func test_next_cost_follows_the_current_level() -> void:
	assert_eq((_setup(0)[0] as SkillTreeProgress).next_cost(0), 10)
	assert_eq((_setup(0, {"a": 1})[0] as SkillTreeProgress).next_cost(0), 20)


func test_buy_deducts_gem_and_raises_the_level() -> void:
	var setup := _setup(25)
	var progress: SkillTreeProgress = setup[0]
	var save: SaveData = setup[1]
	assert_true(progress.buy(0))
	assert_eq(save.gem, 15)
	assert_eq(save.level_of(&"a"), 1)
	assert_false(progress.buy(0), "level 2 costs 20 but only 15 is left")
	assert_eq(save.gem, 15)
	assert_eq(save.level_of(&"a"), 1)


func test_buy_fails_without_enough_gem_and_changes_nothing() -> void:
	var setup := _setup(9)
	var progress: SkillTreeProgress = setup[0]
	var save: SaveData = setup[1]
	assert_false(progress.can_buy(0))
	assert_false(progress.buy(0))
	assert_eq(save.gem, 9)
	assert_eq(save.level_of(&"a"), 0)


func test_buy_fails_on_a_locked_node_even_with_plenty_of_gem() -> void:
	var setup := _setup(1000)
	var progress: SkillTreeProgress = setup[0]
	var save: SaveData = setup[1]
	assert_false(progress.buy(1))
	assert_eq(save.gem, 1000)
	assert_eq(save.level_of(&"b"), 0)


func test_buy_fails_on_a_maxed_node() -> void:
	var setup := _setup(1000, {"a": 2})
	var progress: SkillTreeProgress = setup[0]
	var save: SaveData = setup[1]
	assert_false(progress.buy(0))
	assert_eq(save.gem, 1000)
	assert_eq(save.level_of(&"a"), 2)


func test_buying_the_requirement_opens_the_child() -> void:
	var setup := _setup(30)
	var progress: SkillTreeProgress = setup[0]
	progress.buy(0)
	assert_eq(progress.status(1), SkillTreeProgress.Status.AVAILABLE)
	assert_true(progress.buy(1))
	assert_eq((setup[1] as SaveData).gem, 15)


func test_an_out_of_range_index_is_locked_and_cannot_be_bought() -> void:
	var progress: SkillTreeProgress = _setup(1000)[0]
	assert_eq(progress.status(5), SkillTreeProgress.Status.LOCKED)
	assert_eq(progress.status(-1), SkillTreeProgress.Status.LOCKED)
	assert_eq(progress.next_cost(5), -1)
	assert_false(progress.buy(5))


func test_a_saved_level_above_max_reads_as_maxed() -> void:
	var progress: SkillTreeProgress = _setup(0, {"a": 9})[0]
	assert_eq(progress.status(0), SkillTreeProgress.Status.MAXED)
