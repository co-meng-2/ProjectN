extends TestCase


func test_add_increases_balance_and_emits_changed() -> void:
	var wallet := Wallet.new()
	var emitted: Array = []
	wallet.changed.connect(func(gold: int, stone: int) -> void: emitted.append([gold, stone]))
	wallet.add(Wallet.Kind.GOLD, 5)
	wallet.add(Wallet.Kind.STONE, 2)
	assert_eq(wallet.gold, 5)
	assert_eq(wallet.stone, 2)
	assert_eq(emitted, [[5, 0], [5, 2]])


func test_add_ignores_non_positive_amounts() -> void:
	var wallet := Wallet.new()
	wallet.add(Wallet.Kind.GOLD, 0)
	wallet.add(Wallet.Kind.GOLD, -3)
	assert_eq(wallet.gold, 0)


func test_try_spend_succeeds_when_affordable() -> void:
	var wallet := Wallet.new()
	wallet.add(Wallet.Kind.GOLD, 10)
	assert_true(wallet.try_spend(Wallet.Kind.GOLD, 4))
	assert_eq(wallet.balance(Wallet.Kind.GOLD), 6)


func test_try_spend_fails_without_changing_balance() -> void:
	var wallet := Wallet.new()
	wallet.add(Wallet.Kind.STONE, 1)
	assert_false(wallet.try_spend(Wallet.Kind.STONE, 2))
	assert_false(wallet.try_spend(Wallet.Kind.STONE, -1))
	assert_eq(wallet.stone, 1)
