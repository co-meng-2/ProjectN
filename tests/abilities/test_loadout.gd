extends TestCase


func test_loadout_starts_with_empty_slots() -> void:
	var loadout := Loadout.new(4)
	assert_eq(loadout.size(), 4)
	for i in 4:
		assert_true(loadout.slot(i).is_empty(), "slot %d" % i)
		assert_eq(loadout.slot(i).grade, Grade.Value.NONE)
	assert_null(loadout.slot(4))
	assert_null(loadout.slot(-1))


func test_set_slot_writes_and_emits_once() -> void:
	var loadout := Loadout.new(2)
	var changes: Array = []
	loadout.slot_changed.connect(func(index: int, slot: AbilitySlot) -> void: changes.append([index, slot]))
	var def := DefFactory.ability(&"a")
	assert_true(loadout.set_slot(1, def, Grade.Value.RARE))
	assert_eq(changes.size(), 1)
	assert_eq(changes[0][0], 1)
	assert_eq((changes[0][1] as AbilitySlot).def, def)
	assert_eq(loadout.slot(1).grade, Grade.Value.RARE)
	assert_true(loadout.slot(0).is_empty(), "other slot untouched")


func test_rewriting_the_same_content_still_emits() -> void:
	var loadout := Loadout.new(1)
	var def := DefFactory.ability(&"a")
	loadout.set_slot(0, def, Grade.Value.RARE)
	var changes: Array = []
	loadout.slot_changed.connect(func(index: int, _slot: AbilitySlot) -> void: changes.append(index))
	loadout.set_slot(0, def, Grade.Value.RARE)
	assert_eq(changes, [0], "a reroll onto the same ability must reinstall it")


func test_clearing_a_slot_forces_grade_none() -> void:
	var loadout := Loadout.new(1)
	loadout.set_slot(0, DefFactory.ability(&"a"), Grade.Value.EPIC)
	loadout.set_slot(0, null, Grade.Value.EPIC)
	assert_true(loadout.slot(0).is_empty())
	assert_eq(loadout.slot(0).grade, Grade.Value.NONE)


func test_out_of_range_set_is_refused_without_emitting() -> void:
	var loadout := Loadout.new(1)
	var changes: Array = []
	loadout.slot_changed.connect(func(_index: int, _slot: AbilitySlot) -> void: changes.append(true))
	assert_false(loadout.set_slot(1, DefFactory.ability(&"a"), Grade.Value.COMMON))
	assert_false(loadout.set_slot(-1, DefFactory.ability(&"a"), Grade.Value.COMMON))
	assert_true(changes.is_empty())
