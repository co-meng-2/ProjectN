extends TestCase


func test_shipped_steps_cover_every_version_below_current() -> void:
	assert_eq(SaveMigrations.steps().size(), SaveMigrations.CURRENT_VERSION - 1, "one step per past version")


func test_current_version_is_returned_unchanged() -> void:
	var source := {"version": SaveMigrations.CURRENT_VERSION, "gem": 4}
	var migrated := SaveMigrations.migrate(source)
	assert_eq(migrated.get("gem"), 4)
	assert_eq(migrated.get("version"), SaveMigrations.CURRENT_VERSION)


func test_old_save_is_upgraded_through_every_step() -> void:
	var steps: Array[Callable] = [
		func(d: Dictionary) -> Dictionary:
			d["gem"] = d.get("coins", 0)
			d.erase("coins")
			return d,
		func(d: Dictionary) -> Dictionary:
			d["extra"] = true
			return d,
	]
	var migrated := SaveMigrations.migrate_with({"version": 1, "coins": 7}, steps, 3)
	assert_eq(migrated.get("gem"), 7)
	assert_true(migrated.get("extra", false))
	assert_false(migrated.has("coins"))
	assert_eq(migrated.get("version"), 3)


func test_future_version_is_rejected() -> void:
	assert_true(SaveMigrations.migrate({"version": SaveMigrations.CURRENT_VERSION + 1}).is_empty())


func test_missing_or_invalid_version_is_rejected() -> void:
	assert_true(SaveMigrations.migrate({}).is_empty())
	assert_true(SaveMigrations.migrate({"version": "1"}).is_empty())
	assert_true(SaveMigrations.migrate({"version": 0}).is_empty())
	assert_true(SaveMigrations.migrate({"version": 1.5}).is_empty())


func test_json_style_float_version_is_accepted() -> void:
	assert_false(SaveMigrations.migrate({"version": 1.0}).is_empty())


func test_migration_does_not_mutate_its_input() -> void:
	var source := {"version": 1, "gem": 1}
	SaveMigrations.migrate(source)
	assert_eq(source, {"version": 1, "gem": 1})


func test_read_int_accepts_whole_numbers_only() -> void:
	assert_eq(SaveMigrations.read_int(5), 5)
	assert_eq(SaveMigrations.read_int(5.0), 5)
	assert_null(SaveMigrations.read_int(2.5))
	assert_null(SaveMigrations.read_int("5"))
	assert_null(SaveMigrations.read_int(null))
	assert_null(SaveMigrations.read_int(1.0e30))
