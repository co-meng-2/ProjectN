extends TestCase


func _entry(kind: Wallet.Kind, chance: float, low: int, high: int) -> DropEntry:
	var entry := DropEntry.new()
	entry.kind = kind
	entry.chance = chance
	entry.min_amount = low
	entry.max_amount = high
	entry.icon = DefFactory.texture(8, 8)
	return entry


func test_certain_entry_always_drops_within_range() -> void:
	var table := DropTable.new()
	table.entries.append(_entry(Wallet.Kind.GOLD, 1.0, 2, 4))
	var rng := RunRng.new(11)
	for i in 20:
		var rolls := table.roll(rng)
		assert_eq(rolls.size(), 1)
		assert_eq(rolls[0].kind, Wallet.Kind.GOLD)
		assert_true(rolls[0].amount >= 2 and rolls[0].amount <= 4)


func test_impossible_entry_never_drops() -> void:
	var table := DropTable.new()
	table.entries.append(_entry(Wallet.Kind.STONE, 0.0, 1, 1))
	assert_true(table.roll(RunRng.new(1)).is_empty())


func test_validate_flags_inverted_range_and_bad_chance() -> void:
	var table := DropTable.new()
	table.entries.append(_entry(Wallet.Kind.GOLD, 1.5, 5, 2))
	assert_eq(table.validate().size(), 2)
