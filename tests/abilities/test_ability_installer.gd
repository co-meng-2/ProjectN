extends TestCase

const PROBE_SCENE: PackedScene = preload("res://tests/support/probe_ability.tscn")

var _player: Player
var _loadout: Loadout
var _installer: AbilityInstaller


func _rig(slot_count: int = 3, bind_now: bool = true) -> void:
	_player = ActorFixture.player(self)
	_loadout = Loadout.new(slot_count)
	_installer = adopt(AbilityInstaller.new()) as AbilityInstaller
	if bind_now:
		_installer.bind(_loadout, _player)


func _attr(attribute: String) -> float:
	return _player.asc.get_attribute(attribute).current_value


func _swift() -> AbilityDef:
	var per_level := PackedFloat32Array([0.1, 0.2, 0.3, 0.4, 0.5, 0.6])
	return DefFactory.ability(&"swift", DefFactory.passive_effect("move_speed", GameplayEffectModifier.Operation.PERCENT_ADD, per_level))


func _probe_def() -> AbilityDef:
	return DefFactory.ability(&"probe", null, PROBE_SCENE)


func _hybrid() -> AbilityDef:
	return DefFactory.ability(&"hybrid", DefFactory.vitality().passive_effect, PROBE_SCENE)


func _runtime_abilities() -> Array[ActorAbility]:
	var found: Array[ActorAbility] = []
	for child in _player.asc.get_children():
		if child is ActorAbility and not (child is ArrowAbility) and not (child is DashAbility):
			found.append(child)
	return found


func test_setting_a_slot_applies_the_passive_at_the_grade_level() -> void:
	_rig()
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.COMMON)
	assert_almost_eq(_attr("max_health"), 120.0)
	assert_not_null(_installer.installed_effect(0))
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.RARE)
	assert_almost_eq(_attr("max_health"), 140.0, 0.001, "replaced, not stacked")
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.REVEALED)
	assert_almost_eq(_attr("max_health"), 340.0)


func test_binding_installs_slots_that_were_filled_before() -> void:
	_rig(3, false)
	_loadout.set_slot(1, DefFactory.vitality(), Grade.Value.EPIC)
	assert_almost_eq(_attr("max_health"), 100.0, 0.001, "nothing until bound")
	_installer.bind(_loadout, _player)
	assert_almost_eq(_attr("max_health"), 170.0)


func test_changing_the_ability_removes_the_old_passive() -> void:
	_rig()
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.COMMON)
	_loadout.set_slot(0, _swift(), Grade.Value.COMMON)
	assert_almost_eq(_attr("max_health"), 100.0)
	assert_almost_eq(_attr("move_speed"), 110.0)


func test_two_slots_with_the_same_passive_stack() -> void:
	_rig()
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.COMMON)
	_loadout.set_slot(1, DefFactory.vitality(), Grade.Value.RARE)
	assert_almost_eq(_attr("max_health"), 160.0, 0.001, "20 + 40")
	_loadout.set_slot(1, null, Grade.Value.NONE)
	assert_almost_eq(_attr("max_health"), 120.0)


func test_ability_scenes_are_granted_and_replaced() -> void:
	_rig()
	_loadout.set_slot(0, _probe_def(), Grade.Value.COMMON)
	var first := _installer.installed_ability(0) as ProbeAbility
	assert_not_null(first)
	assert_eq(first.owner_asc, _player.asc)
	assert_almost_eq(first.ability_level, 1.0)
	assert_true(first.context == _player.ability_context)
	_loadout.set_slot(0, _probe_def(), Grade.Value.RARE)
	var second := _installer.installed_ability(0) as ProbeAbility
	assert_true(second != first, "a fresh instance")
	assert_almost_eq(second.ability_level, 2.0)
	assert_eq(_runtime_abilities().size(), 1, "the old one left the ASC at once")
	await next_frame()
	assert_false(is_instance_valid(first))
	_player.asc.try_activate_abilities_by_tag(&"Ability.Probe")
	await next_frame()
	assert_eq(second.activations, 1)


func test_clearing_a_slot_uninstalls_everything() -> void:
	_rig()
	_loadout.set_slot(0, _hybrid(), Grade.Value.EPIC)
	assert_almost_eq(_attr("max_health"), 170.0)
	assert_not_null(_installer.installed_ability(0))
	_loadout.set_slot(0, null, Grade.Value.NONE)
	assert_almost_eq(_attr("max_health"), 100.0)
	assert_null(_installer.installed_ability(0))
	assert_null(_installer.installed_effect(0))
	assert_true(_runtime_abilities().is_empty())


func test_many_changes_leave_no_leftovers() -> void:
	_rig()
	var rng := RunRng.new(9)
	var defs: Array[AbilityDef] = [DefFactory.vitality(), _swift(), _hybrid(), _probe_def()]
	var counts: Array[int] = [0, 0]  # lambdas capture ints by value, so count through an array
	_player.asc.active_effect_added.connect(func(_effect: ActiveGameplayEffect) -> void: counts[0] += 1)
	_player.asc.active_effect_removed.connect(func(_effect: ActiveGameplayEffect) -> void: counts[1] += 1)
	for i in 40:
		var grade := rng.randi_range(1, 6) as Grade.Value
		_loadout.set_slot(rng.randi_range(0, 2), rng.pick(defs) as AbilityDef, grade)
	for i in 3:
		_loadout.set_slot(i, null, Grade.Value.NONE)
	await next_frame()
	assert_almost_eq(_attr("max_health"), 100.0)
	assert_almost_eq(_attr("move_speed"), 100.0)
	assert_eq(counts[0], counts[1], "every applied handle was removed")
	assert_true(counts[0] > 0, "effects were applied")
	assert_true(_runtime_abilities().is_empty())


func test_changing_a_slot_mid_activation_strips_its_tags() -> void:
	_rig()
	_loadout.set_slot(0, _probe_def(), Grade.Value.COMMON)
	var probe := _installer.installed_ability(0) as ProbeAbility
	probe.hold_seconds = 1.0
	probe.activation_owned_tags.append(GameplayTags.State_Invulnerable)
	_player.asc.try_activate_abilities_by_tag(&"Ability.Probe")
	await next_frame()
	assert_true(_player.asc.has_tag(GameplayTags.State_Invulnerable))
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.COMMON)
	assert_false(_player.asc.has_tag(GameplayTags.State_Invulnerable))
	assert_almost_eq(_attr("max_health"), 120.0)
	await wait_seconds(0.1)
	assert_false(is_instance_valid(probe))


func test_rebinding_does_not_double_install() -> void:
	_rig()
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.COMMON)
	_installer.bind(_loadout, _player)
	assert_almost_eq(_attr("max_health"), 120.0, 0.001, "reinstalled once, not stacked")
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.RARE)
	assert_almost_eq(_attr("max_health"), 140.0, 0.001, "one listener")


func test_uninstall_survives_a_cleaned_up_asc() -> void:
	_rig()
	_loadout.set_slot(0, _hybrid(), Grade.Value.COMMON)
	_player.asc.cleanup()
	_loadout.set_slot(0, null, Grade.Value.NONE)
	assert_null(_installer.installed_effect(0))
	assert_true(_runtime_abilities().is_empty())


func test_clearing_vitality_reclamps_health_to_the_lowered_max() -> void:
	_rig()
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.COMMON)
	_player.asc._apply_attribute_change("health", 20.0)  # no heal API; fill up to the raised max
	assert_almost_eq(_attr("health"), 120.0, 0.001, "precondition: health at the raised max")
	_loadout.set_slot(0, null, Grade.Value.NONE)
	assert_almost_eq(_attr("max_health"), 100.0)
	assert_almost_eq(_attr("health"), 100.0, 0.001, "clamped down with the max")


func test_raising_max_health_does_not_heal() -> void:
	_rig()
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.COMMON)
	assert_almost_eq(_attr("max_health"), 120.0)
	assert_almost_eq(_attr("health"), 100.0, 0.001)


func test_validate_defs_flags_a_scene_root_that_is_not_an_actor_ability() -> void:
	var root := Node.new()  # GameplayAbility is abstract; any non-ActorAbility root will do
	var plain := PackedScene.new()
	plain.pack(root)
	root.free()
	var bad := DefFactory.ability(&"bad_root", null, plain)
	var errors := AbilityInstaller.validate_defs([_swift(), _probe_def(), bad])
	assert_eq(errors.size(), 1)
	assert_true(errors[0].contains("bad_root"), errors[0])


func test_validate_defs_accepts_valid_scenes() -> void:
	assert_no_errors(AbilityInstaller.validate_defs([_swift(), _probe_def(), _hybrid()]))
