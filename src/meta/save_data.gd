class_name SaveData
extends RefCounted
## Everything that persists between sessions. Serialised by SaveStore; migrated by SaveMigrations.

## The only persistent currency.
var gem: int = 0
var settings: SettingsData = SettingsData.new()


func to_dict() -> Dictionary:
	return {
		"version": SaveMigrations.CURRENT_VERSION,
		"gem": gem,
		"settings": settings.to_dict(),
	}


## `source` must already be migrated. Missing fields keep defaults; returns null when a present field is invalid.
static func from_dict(source: Dictionary) -> SaveData:
	var result := SaveData.new()
	if source.has("gem"):
		var gem_value: Variant = SaveMigrations.read_int(source["gem"])
		if gem_value == null or gem_value < 0:
			return null
		result.gem = gem_value
	if source.has("settings"):
		var raw_settings: Variant = source["settings"]
		if typeof(raw_settings) != TYPE_DICTIONARY:
			return null
		var parsed := SettingsData.from_dict(raw_settings)
		if parsed == null:
			return null
		result.settings = parsed
	return result
