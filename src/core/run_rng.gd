class_name RunRng
extends RefCounted
## Seeded random source for one stage run so spawns, drops and rerolls are reproducible.

var seed_value: int
var _rng := RandomNumberGenerator.new()


func _init(p_seed: int = 0) -> void:
	seed_value = p_seed if p_seed != 0 else int(Time.get_ticks_usec())
	_rng.seed = seed_value


func randf() -> float:
	return _rng.randf()


func randf_range(from: float, to: float) -> float:
	return _rng.randf_range(from, to)


func randi_range(from: int, to: int) -> int:
	return _rng.randi_range(from, to)


func chance(probability: float) -> bool:
	return _rng.randf() < probability


func pick(items: Array) -> Variant:
	if items.is_empty():
		return null
	return items[_rng.randi_range(0, items.size() - 1)]


## Index chosen proportionally to `weights`; -1 when no weight is positive.
func weighted_index(weights: Array[float]) -> int:
	var total := 0.0
	for weight in weights:
		total += maxf(weight, 0.0)
	if total <= 0.0:
		return -1
	var roll := _rng.randf() * total
	var last_positive := -1
	for i in weights.size():
		if weights[i] <= 0.0:
			continue
		last_positive = i
		roll -= weights[i]
		if roll < 0.0:
			return i
	return last_positive


func weighted(items: Array, weights: Array[float]) -> Variant:
	var index := weighted_index(weights)
	return items[index] if index >= 0 and index < items.size() else null


## Uniformly distributed point inside a circle around the origin.
func offset_in_circle(radius: float) -> Vector2:
	return Vector2.from_angle(_rng.randf() * TAU) * radius * sqrt(_rng.randf())
