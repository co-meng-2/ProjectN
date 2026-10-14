extends TestCase

const TREE_PATH := "res://data/skill_tree/skill_tree.tres"

## node id -> [attribute, value at level 1, value at max level] on a StatBlock.new() actor
## (max_health 100, move_speed 100, attack 1.0).
const EFFECT_NODES: Dictionary = {
	"vitality": ["max_health", 110.0, 135.0],
	"swiftness": ["move_speed", 110.0, 130.0],
	"might": ["attack", 1.1, 1.3],
}


func _tree() -> SkillTreeDef:
	var tree := load(TREE_PATH) as SkillTreeDef
	assert_not_null(tree)
	return tree


func test_the_shipped_tree_validates_and_has_the_expected_order() -> void:
	var tree := _tree()
	assert_no_errors(tree.validate(), "skill_tree")
	var ids: Array[StringName] = []
	for node in tree.nodes:
		ids.append(node.id)
	assert_eq(ids, [&"vitality", &"swiftness", &"might", &"extra_slot"])


func test_the_shipped_requirements() -> void:
	var tree := _tree()
	assert_eq(tree.node_by_id(&"vitality").requires.size(), 0)
	assert_eq(tree.node_by_id(&"swiftness").requires.size(), 0)
	assert_eq(tree.node_by_id(&"might").requires, [&"vitality"])
	assert_eq(tree.node_by_id(&"extra_slot").requires, [&"might"])


func test_effect_nodes_scale_with_the_purchased_level() -> void:
	var tree := _tree()
	for id: String in EFFECT_NODES:
		var expected: Array = EFFECT_NODES[id]
		var node := tree.node_by_id(StringName(id))
		var actor := ActorFixture.bare_actor(self, StatBlock.new())
		var asc := ActorFixture.asc_of(actor)
		var handle := asc.apply_gameplay_effect(node.effect, asc, 1.0)
		assert_not_null(handle, id)
		assert_almost_eq(asc.get_attribute(expected[0]).current_value, expected[1], 0.001, "%s at level 1" % id)
		asc.remove_active_effect(handle)
		asc.apply_gameplay_effect(node.effect, asc, float(node.max_level))
		assert_almost_eq(asc.get_attribute(expected[0]).current_value, expected[2], 0.001, "%s at max level" % id)


func test_extra_slot_is_a_pure_slot_bonus() -> void:
	var node := _tree().node_by_id(&"extra_slot")
	assert_null(node.effect)
	assert_eq(node.slot_bonus, 1)
	assert_eq(node.max_level, 1)


func test_costs_grow_with_each_level() -> void:
	for node in _tree().nodes:
		for index in range(1, node.costs.size()):
			assert_true(node.costs[index] > node.costs[index - 1], "%s level %d" % [node.id, index + 1])
