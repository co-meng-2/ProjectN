extends TestCase

const DIR := "user://test_save_store"
const PATH := "user://test_save_store/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func test_missing_file_gives_defaults_and_no_backup() -> void:
	var data := SaveStore.load_or_default(PATH)
	assert_eq(data.gem, 0)
	assert_false(FileAccess.file_exists(PATH + ".bak"))


func test_write_then_load_round_trips() -> void:
	var data := SaveData.new()
	data.gem = 12
	data.settings.master_volume = 0.4
	assert_true(SaveStore.write(PATH, data))
	var loaded := SaveStore.load_or_default(PATH)
	assert_eq(loaded.gem, 12)
	assert_almost_eq(loaded.settings.master_volume, 0.4)


func test_write_leaves_no_temp_file_and_overwrites_an_existing_save() -> void:
	var first := SaveData.new()
	first.gem = 1
	var second := SaveData.new()
	second.gem = 2
	assert_true(SaveStore.write(PATH, first))
	assert_true(SaveStore.write(PATH, second))
	assert_false(FileAccess.file_exists(PATH + ".tmp"))
	assert_eq(SaveStore.load_or_default(PATH).gem, 2)


func test_write_to_a_missing_directory_fails_without_crashing() -> void:
	assert_false(SaveStore.write("user://test_save_store/missing_dir/save.json", SaveData.new()))


func test_corrupt_json_is_moved_to_backup_and_defaults_are_used() -> void:
	SaveFiles.write_text(PATH, "{ not json")
	var data := SaveStore.load_or_default(PATH)
	assert_eq(data.gem, 0)
	assert_false(FileAccess.file_exists(PATH), "the unreadable file is moved away")
	assert_eq(FileAccess.get_file_as_string(PATH + ".bak"), "{ not json", "the player's bytes are kept")


func test_valid_json_that_is_not_an_object_is_treated_as_corrupt() -> void:
	for text in ["[]", "42", "null", "\"hi\""]:
		SaveFiles.reset(DIR)
		SaveFiles.write_text(PATH, text)
		assert_eq(SaveStore.load_or_default(PATH).gem, 0, text)
		assert_true(FileAccess.file_exists(PATH + ".bak"), "backup for %s" % text)


func test_empty_file_is_treated_as_corrupt() -> void:
	SaveFiles.write_text(PATH, "")
	assert_eq(SaveStore.load_or_default(PATH).gem, 0)
	assert_true(FileAccess.file_exists(PATH + ".bak"))


func test_wrong_field_type_is_treated_as_corrupt() -> void:
	SaveFiles.write_text(PATH, '{"version": 1, "gem": "lots"}')
	assert_eq(SaveStore.load_or_default(PATH).gem, 0)
	assert_true(FileAccess.file_exists(PATH + ".bak"))


func test_future_version_is_treated_as_corrupt_and_preserved() -> void:
	SaveFiles.write_text(PATH, '{"version": 999, "gem": 5}')
	assert_eq(SaveStore.load_or_default(PATH).gem, 0)
	assert_true(FileAccess.get_file_as_string(PATH + ".bak").contains("999"))


func test_a_second_corrupt_file_replaces_an_existing_backup() -> void:
	SaveFiles.write_text(PATH + ".bak", "old backup")
	SaveFiles.write_text(PATH, "garbage")
	assert_eq(SaveStore.load_or_default(PATH).gem, 0)
	assert_eq(FileAccess.get_file_as_string(PATH + ".bak"), "garbage")


func test_gem_stored_as_a_float_in_the_file_loads() -> void:
	SaveFiles.write_text(PATH, '{"version": 1, "gem": 5.0}')
	assert_eq(SaveStore.load_or_default(PATH).gem, 5)
	assert_false(FileAccess.file_exists(PATH + ".bak"))
