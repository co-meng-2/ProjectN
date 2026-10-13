class_name Health
extends Node
## Domain view of an actor's vitals. Observes the ASC and never stores hit points itself.
## Bind it AFTER the actor's stats were applied so the initial overrides do not count as damage.

signal changed(current: float, maximum: float)
signal damaged(amount: float, instigator: Node)
signal died

var is_dead: bool = false
var _asc: AbilitySystemComponent


func bind(asc: AbilitySystemComponent) -> void:
	_asc = asc
	_asc.attribute_changed.connect(_on_attribute_changed)
	changed.emit(current(), maximum())


func current() -> float:
	return _asc.get_attribute("health").current_value if _asc != null else 0.0


func maximum() -> float:
	return _asc.get_attribute("max_health").current_value if _asc != null else 0.0


func _on_attribute_changed(attribute_name: String, old_value: float, new_value: float, spec: GameplayEffectSpec) -> void:
	if attribute_name != "health" and attribute_name != "max_health":
		return
	if attribute_name == "health" and new_value < old_value:
		var instigator: Node = null
		if spec != null and spec.context != null:
			instigator = spec.context.instigator
		damaged.emit(old_value - new_value, instigator)
	changed.emit(current(), maximum())
	if attribute_name == "health" and new_value <= 0.0 and not is_dead:
		is_dead = true
		died.emit()
