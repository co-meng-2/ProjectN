class_name AbilityInstaller
extends Node
## Mirrors a Loadout onto the player's ASC: a slot's passive_effect is held as an active-effect handle,
## its ability_scene as a granted ActorAbility. Changing a slot always uninstalls the old content first,
## so nothing stacks or leaks; two slots holding the same ability stack on purpose.

var _loadout: Loadout
var _player: Player
var _effects: Dictionary[int, ActiveGameplayEffect] = {}
var _abilities: Dictionary[int, ActorAbility] = {}


func bind(loadout: Loadout, player: Player) -> void:
	if _loadout != null and _loadout.slot_changed.is_connected(_on_slot_changed):
		_loadout.slot_changed.disconnect(_on_slot_changed)
	uninstall_all()
	_loadout = loadout
	_player = player
	loadout.slot_changed.connect(_on_slot_changed)
	for index in loadout.size():
		_install(index, loadout.slot(index))


## One error per def whose ability_scene root is not an ActorAbility (the installer could not grant it).
static func validate_defs(defs: Array[AbilityDef]) -> PackedStringArray:
	var errors := PackedStringArray()
	for def in defs:
		if def == null or def.ability_scene == null:
			continue
		var instance := def.ability_scene.instantiate()
		if not instance is ActorAbility:
			errors.append("ability '%s': ability_scene root is not an ActorAbility" % def.id)
		instance.free()
	return errors


func installed_effect(index: int) -> ActiveGameplayEffect:
	return _effects.get(index)


func installed_ability(index: int) -> ActorAbility:
	return _abilities.get(index)


func uninstall_all() -> void:
	for index in _effects.keys().duplicate():
		_uninstall(index)
	for index in _abilities.keys().duplicate():
		_uninstall(index)


func _on_slot_changed(index: int, slot: AbilitySlot) -> void:
	_uninstall(index)
	_install(index, slot)


func _install(index: int, slot: AbilitySlot) -> void:
	if slot == null or slot.is_empty() or not is_instance_valid(_player):
		return
	var level := Grade.level(slot.grade)
	if slot.def.passive_effect != null:
		var handle := _player.asc.apply_gameplay_effect(slot.def.passive_effect, _player.asc, level)
		if handle != null:
			_effects[index] = handle
	if slot.def.ability_scene != null:
		var ability := slot.def.ability_scene.instantiate() as ActorAbility
		if ability == null:
			push_error("AbilityInstaller: '%s' ability_scene root is not an ActorAbility" % slot.def.id)
			return
		ability.ability_level = level
		_player.grant_ability(ability)
		_abilities[index] = ability


func _uninstall(index: int) -> void:
	if _effects.has(index):
		var handle: ActiveGameplayEffect = _effects[index]
		_effects.erase(index)
		if is_instance_valid(_player):
			_player.asc.remove_active_effect(handle)
	if _abilities.has(index):
		var ability: ActorAbility = _abilities[index]
		_abilities.erase(index)
		if is_instance_valid(_player) and is_instance_valid(ability):
			_player.revoke_ability(ability)
