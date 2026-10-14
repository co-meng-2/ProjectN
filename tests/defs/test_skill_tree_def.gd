extends TestCase


func _tree(nodes: Array[SkillNodeDef]) -> SkillTreeDef:
	var tree := SkillTreeDef.new()
	tree.nodes = nodes
	return tree


func test_a_valid_tree_has_no_errors() -> void:
	var tree := _tree([DefFactory.skill_node(&"a"), DefFactory.skill_node(&"b", [&"a"])])
	assert_no_errors(tree.validate(), "a then b requiring a")


func test_an_empty_tree_is_an_error() -> void:
	assert_eq(SkillTreeDef.new().validate().size(), 1)


func test_a_null_node_is_an_error() -> void:
	var tree := _tree([DefFactory.skill_node(&"a")])
	tree.nodes.append(null)
	assert_eq(tree.validate().size(), 1)


func test_duplicate_ids_are_an_error() -> void:
	assert_eq(_tree([DefFactory.skill_node(&"a"), DefFactory.skill_node(&"a")]).validate().size(), 1)


func test_requiring_an_unknown_node_is_an_error() -> void:
	assert_eq(_tree([DefFactory.skill_node(&"a", [&"ghost"])]).validate().size(), 1)


func test_requiring_a_node_listed_later_is_an_error() -> void:
	var tree := _tree([DefFactory.skill_node(&"a", [&"b"]), DefFactory.skill_node(&"b")])
	assert_eq(tree.validate().size(), 1)


func test_a_broken_nested_node_is_reported() -> void:
	var broken := DefFactory.skill_node(&"b", [&"a"])
	broken.max_level = 0
	assert_true(_tree([DefFactory.skill_node(&"a"), broken]).validate().size() >= 1)


func test_index_of_and_node_by_id() -> void:
	var tree := _tree([DefFactory.skill_node(&"a"), DefFactory.skill_node(&"b")])
	assert_eq(tree.index_of(&"b"), 1)
	assert_eq(tree.index_of(&"missing"), -1)
	assert_eq(tree.node_by_id(&"a").id, &"a")
	assert_null(tree.node_by_id(&"missing"))
