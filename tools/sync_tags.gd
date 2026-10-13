extends SceneTree
## Rebuilds godot_gas/tag_registry.tres and godot_gas/gameplay_tags.gd from TAGS.
## Run: Godot --headless --path . -s res://tools/sync_tags.gd

const REGISTRY_PATH: String = "res://godot_gas/tag_registry.tres"
const TAGS: PackedStringArray = [
	"Ability.Arrow",
	"Ability.Dash",
	"Cooldown.Arrow",
	"Cooldown.Dash",
	"Damage.Amount",
	"State.Invulnerable",
]


func _init() -> void:
	var registry := load(REGISTRY_PATH) as GameplayTagRegistry
	if registry == null:
		push_error("sync_tags: cannot load %s" % REGISTRY_PATH)
		quit(1)
		return
	for existing in registry.tags.duplicate():
		registry.remove_tag(existing)
	for tag in TAGS:
		var result := registry.add_tag(tag)
		if result.begins_with("Error"):
			push_error("sync_tags: %s" % result)
			quit(1)
			return
	quit(0)
