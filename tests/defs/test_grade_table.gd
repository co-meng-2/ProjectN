extends TestCase


func test_factory_table_validates() -> void:
	assert_no_errors(DefFactory.grade_table().validate(), "table")


func test_promote_price_follows_the_ladder() -> void:
	var table := DefFactory.grade_table()
	var first := table.promote_price(Grade.Value.NONE)
	assert_eq(first.kind, Wallet.Kind.GOLD)
	assert_eq(first.amount, 100)
	assert_eq(table.promote_price(Grade.Value.COMMON).amount, 150)
	assert_eq(table.promote_price(Grade.Value.UNIQUE).amount, 600)
	var reveal := table.promote_price(Grade.Value.LEGENDARY)
	assert_eq(reveal.kind, Wallet.Kind.STONE)
	assert_eq(reveal.amount, 10)
	assert_null(table.promote_price(Grade.Value.REVEALED), "revealed is the top")


func test_reroll_price_is_stone_except_on_empty_and_revealed() -> void:
	var table := DefFactory.grade_table()
	assert_null(table.reroll_price(Grade.Value.NONE))
	assert_null(table.reroll_price(Grade.Value.REVEALED))
	var price := table.reroll_price(Grade.Value.EPIC)
	assert_eq(price.kind, Wallet.Kind.STONE)
	assert_eq(price.amount, 2)


func test_validate_reports_wrong_size_and_negative_costs_without_crashing() -> void:
	var table := GradeTable.new()
	assert_eq(table.validate().size(), 1, "empty promote_gold")
	assert_null(table.promote_price(Grade.Value.RARE), "short table must not crash")
	table.promote_gold = PackedInt32Array([100, 150, -1, 400, 600])
	table.reveal_stone = -5
	assert_eq(table.validate().size(), 2)


func test_price_is_affordable_compares_the_right_currency() -> void:
	var wallet := Wallet.new()
	wallet.add(Wallet.Kind.GOLD, 100)
	assert_true(Price.new(Wallet.Kind.GOLD, 100).is_affordable(wallet))
	assert_false(Price.new(Wallet.Kind.GOLD, 101).is_affordable(wallet))
	assert_false(Price.new(Wallet.Kind.STONE, 1).is_affordable(wallet))
