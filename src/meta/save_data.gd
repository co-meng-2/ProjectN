class_name SaveData
extends RefCounted
## Everything that persists between sessions. Serialised by SaveStore; migrated by SaveMigrations.

## The only persistent currency.
var gem: int = 0
## Ids of stages the player has cleared. Unlock state is derived from this (see StageProgress); ids that are no
## longer in the catalog are kept so progress survives content changes.
var cleared_stages: Array[StringName] = []
## Purchased skill levels by node id. Ids no longer in the skill tree are kept so investments survive content changes.
var skill_levels: Dictionary[StringName, int] = {}
var settings: SettingsData = SettingsData.new()


func to_dict() -> Dictionary:
	return {
		"version": SaveMigrations.CURRENT_VERSION,
		"gem": gem,
		"cleared_stages": _cleared_stage_strings(),
		"skill_levels": _skill_level_strings(),
		"settings": settings.to_dict(),
	}


func mark_cleared(id: StringName) -> void:
	if not cleared_stages.has(id):
		cleared_stages.append(id)


func level_of(id: StringName) -> int:
	return skill_levels.get(id, 0)


## A level <= 0 removes the entry.
func set_level(id: StringName, level: int) -> void:
	if level <= 0:
		skill_levels.erase(id)
	else:
		skill_levels[id] = level


## Deducts gem all-or-nothing; false (and no change) when the amount is negative or exceeds the balance.
func spend_gem(amount: int) -> bool:
	if amount < 0 or gem < amount:
		return false
	gem -= amount
	return true


func _skill_level_strings() -> Dictionary:
	var result: Dictionary = {}
	for id in skill_levels:
		result[String(id)] = skill_levels[id]
	return result


func _cleared_stage_strings() -> Array:
	var result: Array = []
	for id in cleared_stages:
		result.append(String(id))
	return result


## `source` must already be migrated. Missing fields keep defaults; returns null when a present field is invalid.
static func from_dict(source: Dictionary) -> SaveData:
	var result := SaveData.new()
	if source.has("gem"):
		var gem_value: Variant = SaveMigrations.read_int(source["gem"])
		if gem_value == null or gem_value < 0:
			return null
		result.gem = gem_value
	if source.has("cleared_stages"):
		var raw_ids: Variant = source["cleared_stages"]
		if typeof(raw_ids) != TYPE_ARRAY:
			return null
		for raw_id in raw_ids:
			if typeof(raw_id) != TYPE_STRING and typeof(raw_id) != TYPE_STRING_NAME:
				return null
			result.mark_cleared(StringName(raw_id))
	if source.has("skill_levels"):
		var raw_levels: Variant = source["skill_levels"]
		if typeof(raw_levels) != TYPE_DICTIONARY:
			return null
		for raw_id in raw_levels:
			if typeof(raw_id) != TYPE_STRING and typeof(raw_id) != TYPE_STRING_NAME:
				return null
			var level: Variant = SaveMigrations.read_int(raw_levels[raw_id])
			if level == null or level < 0:
				return null
			result.set_level(StringName(raw_id), level)
	if source.has("settings"):
		var raw_settings: Variant = source["settings"]
		if typeof(raw_settings) != TYPE_DICTIONARY:
			return null
		var parsed := SettingsData.from_dict(raw_settings)
		if parsed == null:
			return null
		result.settings = parsed
	return result
