class_name SettingsData
extends RefCounted
## Player-adjustable options that persist across runs.

const DEFAULT_MASTER_VOLUME: float = 1.0

## Linear 0..1; the setter clamps.
var master_volume: float = DEFAULT_MASTER_VOLUME:
	set(value):
		master_volume = clampf(value, 0.0, 1.0)


func to_dict() -> Dictionary:
	return {"master_volume": master_volume}


## Missing keys keep their defaults; returns null when a present key has the wrong type.
static func from_dict(source: Dictionary) -> SettingsData:
	var result := SettingsData.new()
	if source.has("master_volume"):
		var volume: Variant = source["master_volume"]
		if typeof(volume) != TYPE_FLOAT and typeof(volume) != TYPE_INT:
			return null
		result.master_volume = float(volume)
	return result
