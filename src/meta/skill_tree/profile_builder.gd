class_name ProfileBuilder
extends RefCounted
## Derives the PlayerProfile for a run from the base profile and the purchased skill levels. Pure: the base profile is
## never modified, and Stage only ever sees the result.


## Levels above a node's max_level are clamped; ids that are not in the tree are ignored (they stay in the save).
## Fields are copied explicitly: when PlayerProfile gains a field, copy it here too.
static func build(base: PlayerProfile, tree: SkillTreeDef, levels: Dictionary[StringName, int]) -> PlayerProfile:
	var result := PlayerProfile.new()
	result.stats = base.stats
	result.visual = base.visual
	result.slot_count = base.slot_count
	result.abilities = base.abilities.duplicate()
	result.skill_bonuses = base.skill_bonuses.duplicate()
	for node in tree.nodes:
		if node == null:
			continue
		var level := mini(levels.get(node.id, 0), node.max_level)
		if level < 1:
			continue
		if node.effect != null:
			result.skill_bonuses.append(SkillBonus.new(node.effect, level))
		result.slot_count += node.slot_bonus * level
	return result
