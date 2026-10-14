class_name SaveFiles
extends RefCounted
## Test helper: a clean scratch directory under user:// for save-file tests.


static func reset(directory: String) -> void:
	DirAccess.make_dir_recursive_absolute(directory)
	var dir := DirAccess.open(directory)
	if dir == null:
		return
	for file_name in dir.get_files():
		dir.remove(file_name)


static func write_text(path: String, text: String) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string(text)
	file.close()
