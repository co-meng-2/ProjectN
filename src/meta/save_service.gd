extends Node
## Autoload "SaveService". Holds the session's SaveData and persists it. Screens request changes here and
## never keep save state of their own. No class_name: a global class must not share an autoload's name.

signal changed

const SAVE_PATH: String = "user://save.json"

var data: SaveData = SaveData.new()
## Overridable so tests can point at a scratch file; set before the node enters the tree.
var path: String = SAVE_PATH


func _ready() -> void:
	reload()


func reload() -> void:
	data = SaveStore.load_or_default(path)
	AudioSettings.apply(data.settings)
	changed.emit()


func add_gem(amount: int) -> void:
	if amount < 0:
		push_error("SaveService: add_gem(%d) ignored, amount must be >= 0" % amount)
		return
	data.gem += amount
	changed.emit()


func save() -> bool:
	return SaveStore.write(path, data)
