class_name ProbePattern
extends BossPattern
## Test double: counts starts, completes at once unless `hold` (then `release()` ends it), can refuse to
## start, and records aborts and stepped time.

@export var hold: bool = false
@export var refuse: bool = false

var starts: int = 0
var aborts: int = 0
var stepped: float = 0.0


func release() -> void:
	_complete()


func _begin() -> bool:
	if refuse:
		return false
	starts += 1
	if not hold:
		_complete()
	return true


func _advance(delta: float) -> void:
	stepped += delta


func _abort() -> void:
	aborts += 1
