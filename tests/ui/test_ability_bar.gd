extends TestCase

const BAR_SCENE: PackedScene = preload("res://src/ui/hud/ability_bar.tscn")


func _click(ctrl: bool, button: MouseButton = MOUSE_BUTTON_LEFT, pressed: bool = true) -> InputEventMouseButton:
	var event := InputEventMouseButton.new()
	event.button_index = button
	event.pressed = pressed
	event.ctrl_pressed = ctrl
	return event


func _rig(gold: int = 120, stone: int = 5) -> Dictionary:
	var def := DefFactory.vitality()
	var defs: Array[AbilityDef] = [def]
	var loadout := Loadout.new(3)
	var wallet := Wallet.new()
	wallet.add(Wallet.Kind.GOLD, gold)
	wallet.add(Wallet.Kind.STONE, stone)
	var service := GradeService.new(loadout, AbilityPool.new(defs), DefFactory.grade_table(), wallet, RunRng.new(2))
	var bar := adopt(BAR_SCENE.instantiate()) as AbilityBar
	bar.bind(loadout, wallet, service)
	return {"bar": bar, "loadout": loadout, "wallet": wallet, "service": service, "def": def, "defs": defs}


func _cost_text(view: AbilitySlotView) -> String:
	return (view.get_node("%Cost") as Label).text


func _icon(view: AbilitySlotView) -> TextureRect:
	return view.get_node("%Icon") as TextureRect


func test_creates_one_view_per_slot_showing_the_next_cost() -> void:
	var rig := _rig()
	var bar: AbilityBar = rig["bar"]
	var loadout: Loadout = rig["loadout"]
	assert_eq(bar.get_child_count(), 3)
	assert_null(bar.slot_view(3))
	for i in 3:
		assert_eq(bar.slot_view(i).slot_index, i)
		assert_eq(_cost_text(bar.slot_view(i)), "100g")
		assert_null(_icon(bar.slot_view(i)).texture)
	loadout.set_slot(1, rig["def"], Grade.Value.LEGENDARY)
	assert_eq(_cost_text(bar.slot_view(1)), "10s")
	assert_eq(_icon(bar.slot_view(1)).texture, (rig["def"] as AbilityDef).icon)
	loadout.set_slot(1, rig["def"], Grade.Value.REVEALED)
	assert_eq(_cost_text(bar.slot_view(1)), "MAX")
	assert_eq(_cost_text(bar.slot_view(0)), "100g", "other slots untouched")


func test_click_emits_promote_and_ctrl_click_emits_reroll() -> void:
	var bar: AbilityBar = _rig()["bar"]
	var promotes: Array = []
	var rerolls: Array = []
	bar.promote_requested.connect(func(index: int) -> void: promotes.append(index))
	bar.reroll_requested.connect(func(index: int) -> void: rerolls.append(index))
	bar.slot_view(1)._gui_input(_click(false))
	bar.slot_view(2)._gui_input(_click(true))
	bar.slot_view(0)._gui_input(_click(false, MOUSE_BUTTON_RIGHT))
	bar.slot_view(0)._gui_input(_click(true, MOUSE_BUTTON_LEFT, false))
	bar.slot_view(0)._gui_input(InputEventKey.new())
	assert_eq(promotes, [1])
	assert_eq(rerolls, [2])


func test_the_bar_only_emits_requests() -> void:
	var rig := _rig()
	var bar: AbilityBar = rig["bar"]
	bar.slot_view(0)._gui_input(_click(false))
	bar.slot_view(0)._gui_input(_click(true))
	assert_eq((rig["wallet"] as Wallet).gold, 120, "no spending from the view")
	assert_true((rig["loadout"] as Loadout).slot(0).is_empty(), "no slot change from the view")


func test_unaffordable_slots_are_dimmed_and_follow_the_wallet() -> void:
	var rig := _rig(120, 0)
	var bar: AbilityBar = rig["bar"]
	var wallet: Wallet = rig["wallet"]
	var loadout: Loadout = rig["loadout"]
	assert_false(bar.slot_view(0).is_dimmed(), "120 gold covers 100")
	wallet.try_spend(Wallet.Kind.GOLD, 100)
	assert_true(bar.slot_view(0).is_dimmed(), "20 gold left")
	assert_true(bar.slot_view(2).is_dimmed(), "every empty slot")
	assert_true((bar.slot_view(2).get_node("%Cost") as Label).modulate.a < 1.0, "empty slot visibly dimmed")
	wallet.add(Wallet.Kind.GOLD, 100)
	assert_eq((bar.slot_view(2).get_node("%Cost") as Label).modulate.a, 1.0, "affordable slot not dimmed")
	wallet.try_spend(Wallet.Kind.GOLD, 100)
	wallet.add(Wallet.Kind.GOLD, 100)
	assert_false(bar.slot_view(0).is_dimmed())
	loadout.set_slot(0, rig["def"], Grade.Value.LEGENDARY)
	assert_true(bar.slot_view(0).is_dimmed(), "10 stone needed, 0 owned")
	loadout.set_slot(0, rig["def"], Grade.Value.REVEALED)
	assert_false(bar.slot_view(0).is_dimmed(), "nothing to buy is not unaffordable")
	assert_eq(bar.slot_view(0).tooltip_text.find("promote"), -1, "no promote line when fully revealed")
	assert_true(bar.slot_view(0).tooltip_text.contains("Fully revealed"))


func test_failed_actions_flash_the_slot_and_successful_ones_do_not() -> void:
	var rig := _rig(100, 0)
	var bar: AbilityBar = rig["bar"]
	var service: GradeService = rig["service"]
	assert_eq(service.promote(0).outcome, GradeResult.Outcome.OK)
	assert_false(bar.slot_view(0).is_flashing())
	assert_eq(service.promote(1).outcome, GradeResult.Outcome.NOT_ENOUGH)
	assert_true(bar.slot_view(1).is_flashing())
	assert_false(bar.slot_view(0).is_flashing(), "only the refused slot")
	await wait_seconds(0.4)
	assert_false(bar.slot_view(1).is_flashing(), "the flash fades")


func test_revealed_slot_uses_the_revealed_icon_or_a_shine_tint() -> void:
	var rig := _rig()
	var bar: AbilityBar = rig["bar"]
	var def: AbilityDef = rig["def"]
	var loadout: Loadout = rig["loadout"]
	loadout.set_slot(0, def, Grade.Value.LEGENDARY)
	assert_eq(_icon(bar.slot_view(0)).modulate, Color.WHITE, "not revealed: no tint")
	loadout.set_slot(0, def, Grade.Value.REVEALED)
	assert_eq(_icon(bar.slot_view(0)).texture, def.icon)
	assert_true(_icon(bar.slot_view(0)).modulate != Color.WHITE, "tinted: no revealed icon")
	def.revealed_icon = DefFactory.texture(16, 16)
	loadout.set_slot(0, def, Grade.Value.REVEALED)
	assert_eq(_icon(bar.slot_view(0)).texture, def.revealed_icon)
	assert_eq(_icon(bar.slot_view(0)).modulate, Color.WHITE)


func test_rebinding_replaces_the_views_without_duplicate_listeners() -> void:
	var rig := _rig(100, 0)
	var bar: AbilityBar = rig["bar"]
	var old_loadout: Loadout = rig["loadout"]
	var old_service: GradeService = rig["service"]
	var wallet: Wallet = rig["wallet"]
	var second := Loadout.new(2)
	var second_service := GradeService.new(second, AbilityPool.new(rig["defs"]), DefFactory.grade_table(), wallet, RunRng.new(3))
	bar.bind(second, wallet, second_service)
	await next_frame()
	assert_eq(bar.get_child_count(), 2)
	var promotes: Array = []
	bar.promote_requested.connect(func(index: int) -> void: promotes.append(index))
	bar.slot_view(0)._gui_input(_click(false))
	assert_eq(promotes, [0])
	# The old models still change, but the rebound views must not react to them.
	old_loadout.set_slot(0, rig["def"], Grade.Value.LEGENDARY)
	assert_eq(old_service.promote(0).outcome, GradeResult.Outcome.NOT_ENOUGH, "10 stone needed, 0 owned")
	for i in 2:
		assert_eq(_cost_text(bar.slot_view(i)), "100g", "new views ignore the old loadout")
		assert_null(_icon(bar.slot_view(i)).texture)
		assert_false(bar.slot_view(i).is_flashing(), "new views ignore the old service")
	assert_eq(bar.get_child_count(), 2)


func test_a_held_ticket_prices_every_applicable_slot_at_one_ticket() -> void:
	var rig := _rig(0, 0)
	var bar: AbilityBar = rig["bar"]
	var wallet: Wallet = rig["wallet"]
	var loadout: Loadout = rig["loadout"]
	loadout.set_slot(1, rig["def"], Grade.Value.REVEALED)
	assert_eq(_cost_text(bar.slot_view(0)), "100g")
	assert_true(bar.slot_view(0).is_dimmed(), "no gold")
	wallet.add(Wallet.Kind.TICKET, 1)
	assert_eq(_cost_text(bar.slot_view(0)), "1T")
	assert_eq(_cost_text(bar.slot_view(2)), "1T")
	assert_false(bar.slot_view(0).is_dimmed(), "the ticket covers it")
	assert_eq(_cost_text(bar.slot_view(1)), "MAX", "nothing to buy on a revealed slot")
	assert_true(bar.slot_view(0).tooltip_text.contains("1T"))
	wallet.try_spend(Wallet.Kind.TICKET, 1)
	assert_eq(_cost_text(bar.slot_view(0)), "100g", "back to gold once the ticket is gone")
