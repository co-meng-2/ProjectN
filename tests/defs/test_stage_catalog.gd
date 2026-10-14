extends TestCase


func _stage(id: StringName) -> StageDef:
	var stage := DefFactory.stage()
	stage.id = id
	return stage


func _catalog(ids: Array[StringName]) -> StageCatalog:
	var catalog := StageCatalog.new()
	for id in ids:
		catalog.stages.append(_stage(id))
	return catalog


func test_a_valid_catalog_has_no_errors() -> void:
	assert_no_errors(_catalog([&"a", &"b"]).validate(), "two distinct valid stages")


func test_an_empty_catalog_is_an_error() -> void:
	assert_eq(StageCatalog.new().validate().size(), 1)


func test_a_null_entry_is_an_error() -> void:
	var catalog := _catalog([&"a"])
	catalog.stages.append(null)
	assert_eq(catalog.validate().size(), 1)


func test_duplicate_ids_are_an_error() -> void:
	assert_eq(_catalog([&"a", &"a"]).validate().size(), 1)


func test_a_broken_nested_stage_is_reported() -> void:
	var catalog := _catalog([&"a", &"b"])
	catalog.stages[1].duration = 0.0
	assert_true(catalog.validate().size() >= 1)


func test_index_of_finds_a_stage_or_returns_minus_one() -> void:
	var catalog := _catalog([&"a", &"b"])
	assert_eq(catalog.index_of(&"b"), 1)
	assert_eq(catalog.index_of(&"missing"), -1)
