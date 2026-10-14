class_name SkillTreeDef
extends Resource
## The ordered skill-tree nodes. Order is display order and dependency order: a node may only require nodes listed
## before it, so the tree can never contain a cycle.

@export var nodes: Array[SkillNodeDef] = []


## Index of the node with this id, or -1.
func index_of(id: StringName) -> int:
	for index in nodes.size():
		if nodes[index] != null and nodes[index].id == id:
			return index
	return -1


func node_by_id(id: StringName) -> SkillNodeDef:
	var index := index_of(id)
	return nodes[index] if index >= 0 else null


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if nodes.is_empty():
		errors.append("SkillTreeDef: no nodes")
	var earlier: Dictionary[StringName, bool] = {}
	for node in nodes:
		if node == null:
			errors.append("SkillTreeDef: null node")
			continue
		if earlier.has(node.id):
			errors.append("SkillTreeDef: duplicate node id '%s'" % node.id)
		for required in node.requires:
			if not earlier.has(required):
				errors.append("SkillTreeDef: node '%s' requires '%s', which is not listed before it" % [node.id, required])
		errors.append_array(node.validate())
		earlier[node.id] = true
	return errors
