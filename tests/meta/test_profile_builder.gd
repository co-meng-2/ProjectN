extends TestCase


func _tree() -> SkillTreeDef:
	var slot := DefFactory.skill_node(&"slot", [&"a"], 2, PackedInt32Array([10, 20]))
	slot.effect = null
	slot.slot_bonus = 1
	var tree := SkillTreeDef.new()
	tree.nodes = [DefFactory.skill_node(&"a"), slot]
	return tree


func _levels(values: Dictionary) -> Dictionary[StringName, int]:
	var result: Dictionary[StringName, int] = {}
	for id: String in values:
		result[StringName(id)] = values[id]
	return result


func test_without_purchases_the_profile_equals_the_base() -> void:
	var base := DefFactory.profile()
	var built := ProfileBuilder.build(base, _tree(), _levels({}))
	assert_true(built.stats == base.stats)
	assert_true(built.visual == base.visual)
	assert_eq(built.slot_count, base.slot_count)
	assert_eq(built.abilities.size(), base.abilities.size())
	assert_eq(built.skill_bonuses.size(), 0)


func test_the_base_profile_is_never_mutated() -> void:
	var base := DefFactory.profile()
	var slots_before := base.slot_count
	ProfileBuilder.build(base, _tree(), _levels({"a": 2, "slot": 2}))
	assert_eq(base.slot_count, slots_before)
	assert_eq(base.skill_bonuses.size(), 0)


func test_each_bought_effect_node_becomes_a_bonus_at_its_level() -> void:
	var tree := _tree()
	var built := ProfileBuilder.build(DefFactory.profile(), tree, _levels({"a": 2}))
	assert_eq(built.skill_bonuses.size(), 1)
	assert_true(built.skill_bonuses[0].effect == tree.nodes[0].effect)
	assert_eq(built.skill_bonuses[0].level, 2)


func test_slot_bonuses_add_up_per_level() -> void:
	var base := DefFactory.profile()
	var built := ProfileBuilder.build(base, _tree(), _levels({"slot": 2}))
	assert_eq(built.slot_count, base.slot_count + 2)
	assert_eq(built.skill_bonuses.size(), 0, "a slot-only node carries no effect")


func test_a_saved_level_above_max_is_clamped() -> void:
	var built := ProfileBuilder.build(DefFactory.profile(), _tree(), _levels({"a": 99}))
	assert_eq(built.skill_bonuses[0].level, 2)


func test_an_id_not_in_the_tree_is_ignored() -> void:
	var built := ProfileBuilder.build(DefFactory.profile(), _tree(), _levels({"ghost": 3}))
	assert_eq(built.skill_bonuses.size(), 0)


func test_a_built_profile_validates() -> void:
	var built := ProfileBuilder.build(DefFactory.profile(), _tree(), _levels({"a": 1, "slot": 1}))
	assert_no_errors(built.validate(), "built profile")
