class_name BossBrain
extends Node
## Decides when the boss attacks: after `pattern_gap` seconds of chasing it picks the next pattern
## (SEQUENCE = list order, wrapping; RANDOM = weighted, never the same twice in a row when there are
## alternatives), activates it through the ASC by its ability tag, waits for that ability to end, then
## starts the next gap. Plain code, not GAS: the patterns are the abilities.

signal pattern_started(ability: ActorAbility)

var _asc: AbilitySystemComponent
var _patterns: Array[ActorAbility] = []
var _weights: Array[float] = []
var _order: BossDef.Order = BossDef.Order.SEQUENCE
var _gap: float = 0.0
var _rng: RunRng
var _gap_left: float = 0.0
var _next_index: int = 0
var _last_index: int = -1
var _casting: ActorAbility
var _stopped: bool = false


## Adds a brain named "Brain" under a set-up boss and binds it to the boss's patterns and def.
static func attach_to(boss: Boss, rng: RunRng) -> BossBrain:
	var brain := BossBrain.new()
	brain.name = "Brain"
	boss.add_child(brain)
	brain.bind(boss.asc, boss.patterns(), boss.pattern_weights(), boss.def.order, boss.def.pattern_gap, rng)
	boss.died.connect(func(_boss: Boss) -> void: brain.stop())
	return brain


## Boss-layer check run by Stage.start (defs/ cannot name ActorAbility): every pattern root is an
## ActorAbility whose ability_tag is set and unique within the boss.
static func validate_def(def: BossDef) -> PackedStringArray:
	var errors := PackedStringArray()
	if def == null:
		return errors
	var seen: Dictionary[StringName, bool] = {}
	for pattern in def.patterns:
		if pattern == null or pattern.ability_scene == null:
			continue  # BossDef.validate reports these
		var root := pattern.ability_scene.instantiate()
		if root == null:
			continue
		if not (root is ActorAbility):
			errors.append("boss '%s': a pattern scene root is not an ActorAbility" % def.id)
		else:
			var tag := (root as ActorAbility).ability_tag
			if tag == &"" or tag == &"Ability.None":
				errors.append("boss '%s': pattern '%s' has no ability_tag" % [def.id, (root as ActorAbility).ability_name])
			elif seen.has(tag):
				errors.append("boss '%s': two patterns share the ability_tag '%s' (the brain activates by tag)" % [def.id, tag])
			seen[tag] = true
		root.free()
	return errors


func bind(asc: AbilitySystemComponent, patterns: Array[ActorAbility], weights: Array[float], order: BossDef.Order, pattern_gap: float, rng: RunRng) -> void:
	_asc = asc
	_patterns = patterns.duplicate()
	_weights = weights.duplicate()
	_order = order
	_gap = pattern_gap
	_rng = rng
	_gap_left = pattern_gap
	_next_index = 0
	_last_index = -1
	_casting = null
	_stopped = false
	for ability in _patterns:
		if not ability.ability_ended.is_connected(_on_pattern_ended):
			ability.ability_ended.connect(_on_pattern_ended)


func stop() -> void:
	_stopped = true


## The pattern the brain is waiting on, or null while it counts down a gap.
func casting() -> ActorAbility:
	return _casting


func step(delta: float) -> void:
	if _stopped or _casting != null or _patterns.is_empty():
		return
	_gap_left -= delta
	if _gap_left > 0.0:
		return
	_activate(_pick())


func _pick() -> int:
	var index := 0
	if _order == BossDef.Order.SEQUENCE:
		index = _next_index % _patterns.size()
		_next_index = index + 1
	else:
		var weights := _weights.duplicate()
		if _patterns.size() > 1 and _last_index >= 0:
			weights[_last_index] = 0.0
		index = maxi(_rng.weighted_index(weights), 0)
	_last_index = index
	return index


func _activate(index: int) -> void:
	var ability := _patterns[index]
	_casting = ability
	_gap_left = _gap
	pattern_started.emit(ability)
	# A pattern that finishes (or refuses) synchronously has already cleared `_casting` through ability_ended.
	var activated := _asc.try_activate_abilities_by_tag(ability.ability_tag)
	if not activated and _casting == ability and not ability.is_active:
		_casting = null


func _on_pattern_ended(_was_cancelled: bool) -> void:
	if _casting == null or _casting.is_active:
		return
	_casting = null
	_gap_left = _gap


func _physics_process(delta: float) -> void:
	step(delta)
