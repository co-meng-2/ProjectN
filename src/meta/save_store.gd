class_name SaveStore
extends RefCounted
## Reads and writes the save file. Never loses the player's data: writes are atomic and unreadable files are
## moved aside rather than overwritten.


## Always returns a usable SaveData: defaults when the file is absent or unreadable.
static func load_or_default(path: String) -> SaveData:
	if not FileAccess.file_exists(path):
		return SaveData.new()
	var json := JSON.new()
	if json.parse(FileAccess.get_file_as_string(path)) == OK and typeof(json.data) == TYPE_DICTIONARY:
		var migrated := SaveMigrations.migrate(json.data)
		if not migrated.is_empty():
			var data := SaveData.from_dict(migrated)
			if data != null:
				return data
	_quarantine(path)
	return SaveData.new()


## Writes `<path>.tmp` then renames it over `path`, so a crash cannot leave a half-written save.
static func write(path: String, data: SaveData) -> bool:
	var temp_path := path + ".tmp"
	var file := FileAccess.open(temp_path, FileAccess.WRITE)
	if file == null:
		push_error("SaveStore: cannot open '%s' for writing (error %d)" % [temp_path, FileAccess.get_open_error()])
		return false
	file.store_string(JSON.stringify(data.to_dict(), "\t"))
	file.close()
	var error := DirAccess.rename_absolute(temp_path, path)
	if error != OK:
		push_error("SaveStore: cannot move '%s' over '%s' (error %d)" % [temp_path, path, error])
		return false
	return true


static func _quarantine(path: String) -> void:
	var backup := path + ".bak"
	if FileAccess.file_exists(backup):
		DirAccess.remove_absolute(backup)
	var error := DirAccess.rename_absolute(path, backup)
	push_warning("SaveStore: '%s' is unreadable; kept as '%s' and starting from defaults (rename error %d)" % [path, backup, error])
