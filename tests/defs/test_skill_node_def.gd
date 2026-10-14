extends TestCase


func test_a_valid_node_has_no_errors() -> void:
	assert_no_errors(DefFactory.skill_node(&"a").validate(), "effect node")


func test_a_slot_only_node_is_valid() -> void:
	var node := DefFactory.skill_node(&"slot", [], 1, PackedInt32Array([50]))
	node.effect = null
	node.slot_bonus = 1
	assert_no_errors(node.validate(), "slot node")


func test_empty_id_or_name_is_an_error() -> void:
	var node := DefFactory.skill_node(&"a")
	node.id = &""
	assert_eq(node.validate().size(), 1, "id")
	var unnamed := DefFactory.skill_node(&"a")
	unnamed.display_name = ""
	assert_eq(unnamed.validate().size(), 1, "name")


func test_max_level_below_one_is_an_error() -> void:
	var node := DefFactory.skill_node(&"a", [], 0, PackedInt32Array())
	assert_eq(node.validate().size(), 1)


func test_costs_must_match_max_level() -> void:
	var node := DefFactory.skill_node(&"a", [], 3, PackedInt32Array([10, 20]))
	assert_eq(node.validate().size(), 1)


func test_a_non_positive_cost_is_an_error() -> void:
	var node := DefFactory.skill_node(&"a", [], 2, PackedInt32Array([10, 0]))
	assert_eq(node.validate().size(), 1)


func test_a_node_must_do_something() -> void:
	var node := DefFactory.skill_node(&"a")
	node.effect = null
	assert_eq(node.validate().size(), 1)


func test_a_negative_slot_bonus_is_an_error() -> void:
	var node := DefFactory.skill_node(&"a")
	node.slot_bonus = -1
	assert_eq(node.validate().size(), 1)


func test_a_node_cannot_require_itself() -> void:
	assert_eq(DefFactory.skill_node(&"a", [&"a"]).validate().size(), 1)


func test_effect_problems_are_reported_through_the_shared_check() -> void:
	var node := DefFactory.skill_node(&"a")
	node.effect.policy = GameplayEffect.DurationPolicy.DURATION
	assert_eq(node.validate().size(), 1, "not INFINITE")
	var zero := DefFactory.skill_node(&"a")
	zero.effect.modifiers[0].magnitude = 0.0
	assert_eq(zero.validate().size(), 1, "magnitude 0")
