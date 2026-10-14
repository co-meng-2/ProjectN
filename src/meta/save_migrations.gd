class_name SaveMigrations
extends RefCounted
## Brings a loaded save dictionary up to the current schema one version at a time.
## To change the schema: bump CURRENT_VERSION and append one step to `steps()`.

const CURRENT_VERSION: int = 1
## JSON numbers are floats; anything beyond this is not a plausible whole count.
const MAX_SAFE_INT: float = 9.0e15


## steps()[i] upgrades a version (i + 1) dictionary to version (i + 2).
static func steps() -> Array[Callable]:
	return []


## Returns the migrated copy, or an empty dictionary when the version is missing, invalid or from the future.
static func migrate(source: Dictionary) -> Dictionary:
	return migrate_with(source, steps(), CURRENT_VERSION)


static func migrate_with(source: Dictionary, migration_steps: Array[Callable], current: int) -> Dictionary:
	var raw_version: Variant = read_int(source.get("version"))
	if raw_version == null:
		return {}
	var version: int = raw_version
	if version < 1 or version > current:
		return {}
	var result := source.duplicate(true)
	while version < current:
		result = migration_steps[version - 1].call(result)
		version += 1
	result["version"] = current
	return result


## A whole JSON number as an int, else null. JSON has no int type, so 5 is stored and read back as 5.0.
static func read_int(raw: Variant) -> Variant:
	if typeof(raw) == TYPE_INT:
		return raw
	if typeof(raw) == TYPE_FLOAT and absf(raw) < MAX_SAFE_INT and is_equal_approx(raw, roundf(raw)):
		return int(raw)
	return null
