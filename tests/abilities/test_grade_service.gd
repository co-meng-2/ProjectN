extends TestCase

var _loadout: Loadout
var _wallet: Wallet
var _service: GradeService


func _build(defs: Array[AbilityDef], gold: int, stone: int, run_seed: int = 3) -> GradeService:
	_loadout = Loadout.new(2)
	_wallet = Wallet.new()
	_wallet.add(Wallet.Kind.GOLD, gold)
	_wallet.add(Wallet.Kind.STONE, stone)
	_service = GradeService.new(_loadout, AbilityPool.new(defs), DefFactory.grade_table(), _wallet, RunRng.new(run_seed))
	return _service


func _single() -> Array[AbilityDef]:
	var defs: Array[AbilityDef] = [DefFactory.ability(&"only")]
	return defs


func _pair() -> Array[AbilityDef]:
	var defs: Array[AbilityDef] = [DefFactory.ability(&"first"), DefFactory.ability(&"second")]
	return defs


func test_buying_an_empty_slot_costs_100_gold_and_rolls_a_common() -> void:
	var service := _build(_single(), 250, 0)
	var result := service.promote(0)
	assert_eq(result.outcome, GradeResult.Outcome.OK)
	assert_eq(result.grade, Grade.Value.COMMON)
	assert_eq(result.def.id, &"only")
	assert_eq(_loadout.slot(0).def.id, &"only")
	assert_eq(_loadout.slot(0).grade, Grade.Value.COMMON)
	assert_eq(_wallet.gold, 150)
	assert_true(_loadout.slot(1).is_empty(), "other slot untouched")


func test_promotion_climbs_with_rising_gold_costs() -> void:
	var service := _build(_single(), 1500, 0)
	for expected_grade in [Grade.Value.COMMON, Grade.Value.RARE, Grade.Value.EPIC, Grade.Value.UNIQUE, Grade.Value.LEGENDARY]:
		assert_eq(service.promote(0).outcome, GradeResult.Outcome.OK)
		assert_eq(_loadout.slot(0).grade, expected_grade)
	assert_eq(_wallet.gold, 0, "100 + 150 + 250 + 400 + 600")


func test_promote_cost_is_the_price_of_the_next_step() -> void:
	var defs := _single()
	var service := _build(defs, 0, 0)
	assert_eq(service.promote_cost(0).amount, 100)
	_loadout.set_slot(0, defs[0], Grade.Value.EPIC)
	assert_eq(service.promote_cost(0).amount, 400)
	assert_eq(service.promote_cost(0).kind, Wallet.Kind.GOLD)
	_loadout.set_slot(0, defs[0], Grade.Value.LEGENDARY)
	assert_eq(service.promote_cost(0).kind, Wallet.Kind.STONE)
	assert_eq(service.reroll_cost(0).amount, 2)


func test_promote_without_enough_gold_changes_nothing() -> void:
	var service := _build(_single(), 99, 5)
	var changes: Array = []
	_loadout.slot_changed.connect(func(index: int, _slot: AbilitySlot) -> void: changes.append(index))
	var result := service.promote(0)
	assert_eq(result.outcome, GradeResult.Outcome.NOT_ENOUGH)
	assert_null(result.def)
	assert_true(_loadout.slot(0).is_empty())
	assert_eq(_wallet.gold, 99)
	assert_eq(_wallet.stone, 5)
	assert_true(changes.is_empty())


func test_promote_into_an_empty_pool_spends_nothing() -> void:
	var defs: Array[AbilityDef] = [DefFactory.ability(&"rare_only", null, null, Grade.Value.RARE)]
	var service := _build(defs, 500, 0)
	var result := service.promote(0)
	assert_eq(result.outcome, GradeResult.Outcome.EMPTY_POOL, "nothing can be COMMON")
	assert_eq(_wallet.gold, 500)
	assert_true(_loadout.slot(0).is_empty())


func test_reveal_keeps_the_same_ability_and_costs_stone() -> void:
	var defs := _pair()
	var service := _build(defs, 0, 25)
	_loadout.set_slot(0, defs[1], Grade.Value.LEGENDARY)
	for i in 10:
		var result := service.promote(0)
		if i == 0:
			assert_eq(result.outcome, GradeResult.Outcome.OK)
		else:
			assert_eq(result.outcome, GradeResult.Outcome.NOT_APPLICABLE, "revealed is the top")
	assert_eq(_loadout.slot(0).def, defs[1], "same ability, never rolled")
	assert_eq(_loadout.slot(0).grade, Grade.Value.REVEALED)
	assert_eq(_wallet.stone, 15, "charged exactly once")
	assert_null(service.promote_cost(0))


func test_reveal_without_enough_stone_is_refused() -> void:
	var defs := _single()
	var service := _build(defs, 1000, 9)
	_loadout.set_slot(0, defs[0], Grade.Value.LEGENDARY)
	assert_eq(service.promote(0).outcome, GradeResult.Outcome.NOT_ENOUGH)
	assert_eq(_loadout.slot(0).grade, Grade.Value.LEGENDARY)
	assert_eq(_wallet.gold, 1000, "gold is not an alternative currency")


func test_promotion_rerolls_from_the_target_grade_pool() -> void:
	var defs := _pair()
	var service := _build(defs, 100000, 0)
	var seen: Dictionary[StringName, bool] = {}
	for i in 16:
		_loadout.set_slot(0, defs[0], Grade.Value.COMMON)
		assert_eq(service.promote(0).outcome, GradeResult.Outcome.OK)
		assert_eq(_loadout.slot(0).grade, Grade.Value.RARE)
		seen[_loadout.slot(0).def.id] = true
	assert_true(seen.has(&"second"), "promotion may change the ability (16 seeded tries)")


func test_reroll_costs_stone_and_prefers_a_different_ability() -> void:
	var defs := _pair()
	var service := _build(defs, 0, 10)
	_loadout.set_slot(0, defs[0], Grade.Value.EPIC)
	var expected := [defs[1], defs[0], defs[1]]
	for i in 3:
		var result := service.reroll(0)
		assert_eq(result.outcome, GradeResult.Outcome.OK)
		assert_eq(_loadout.slot(0).grade, Grade.Value.EPIC, "same grade")
		assert_eq(_loadout.slot(0).def, expected[i], "alternates between the two")
	assert_eq(_wallet.stone, 4)


func test_reroll_on_a_single_ability_pool_keeps_the_ability_but_still_charges() -> void:
	var defs := _single()
	var service := _build(defs, 0, 4)
	_loadout.set_slot(0, defs[0], Grade.Value.RARE)
	var changes: Array = []
	_loadout.slot_changed.connect(func(index: int, _slot: AbilitySlot) -> void: changes.append(index))
	assert_eq(service.reroll(0).outcome, GradeResult.Outcome.OK)
	assert_eq(_loadout.slot(0).def, defs[0])
	assert_eq(_wallet.stone, 2)
	assert_eq(changes, [0], "re-emitted so the installer reinstalls a fresh instance")


func test_reroll_is_refused_on_empty_and_revealed_slots_and_without_stone() -> void:
	var defs := _single()
	var service := _build(defs, 0, 1)
	assert_eq(service.reroll(0).outcome, GradeResult.Outcome.NOT_APPLICABLE, "empty slot")
	assert_null(service.reroll_cost(0))
	_loadout.set_slot(0, defs[0], Grade.Value.REVEALED)
	assert_eq(service.reroll(0).outcome, GradeResult.Outcome.NOT_APPLICABLE, "revealed")
	_loadout.set_slot(0, defs[0], Grade.Value.RARE)
	assert_eq(service.reroll(0).outcome, GradeResult.Outcome.NOT_ENOUGH, "1 stone < 2")
	assert_eq(_wallet.stone, 1)
	assert_eq(_loadout.slot(0).grade, Grade.Value.RARE)


func test_out_of_range_slot_is_not_applicable() -> void:
	var service := _build(_single(), 1000, 100)
	assert_eq(service.promote(7).outcome, GradeResult.Outcome.NOT_APPLICABLE)
	assert_eq(service.reroll(-1).outcome, GradeResult.Outcome.NOT_APPLICABLE)
	assert_null(service.promote_cost(7))
	assert_null(service.reroll_cost(-1))
	assert_eq(_wallet.gold, 1000)


func test_resolved_is_emitted_for_every_attempt() -> void:
	var service := _build(_single(), 100, 0)
	var outcomes: Array = []
	service.resolved.connect(func(index: int, result: GradeResult) -> void: outcomes.append([index, result.outcome]))
	service.promote(0)
	service.promote(0)
	service.reroll(1)
	assert_eq(outcomes, [[0, GradeResult.Outcome.OK], [0, GradeResult.Outcome.NOT_ENOUGH], [1, GradeResult.Outcome.NOT_APPLICABLE]])
