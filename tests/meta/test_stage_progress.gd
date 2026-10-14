extends TestCase


func _progress(cleared: Array[StringName]) -> StageProgress:
	var catalog := StageCatalog.new()
	for id: StringName in [&"a", &"b", &"c"]:
		var stage := DefFactory.stage()
		stage.id = id
		catalog.stages.append(stage)
	var save := SaveData.new()
	for id in cleared:
		save.mark_cleared(id)
	return StageProgress.new(catalog, save)


func test_the_first_stage_is_always_available() -> void:
	var progress := _progress([])
	assert_eq(progress.status(0), StageProgress.Status.AVAILABLE)
	assert_true(progress.is_unlocked(0))


func test_later_stages_are_locked_until_the_previous_is_cleared() -> void:
	var progress := _progress([])
	assert_eq(progress.status(1), StageProgress.Status.LOCKED)
	assert_false(progress.is_unlocked(1))
	assert_eq(progress.status(2), StageProgress.Status.LOCKED)


func test_clearing_a_stage_unlocks_the_next_one_only() -> void:
	var progress := _progress([&"a"])
	assert_eq(progress.status(0), StageProgress.Status.CLEARED)
	assert_eq(progress.status(1), StageProgress.Status.AVAILABLE)
	assert_eq(progress.status(2), StageProgress.Status.LOCKED)


func test_a_cleared_stage_stays_playable() -> void:
	assert_true(_progress([&"a"]).is_unlocked(0))


func test_an_id_not_in_the_catalog_is_ignored() -> void:
	var progress := _progress([&"ghost"])
	assert_eq(progress.status(0), StageProgress.Status.AVAILABLE)
	assert_eq(progress.status(1), StageProgress.Status.LOCKED)


func test_an_out_of_range_index_is_locked() -> void:
	var progress := _progress([&"a", &"b", &"c"])
	assert_eq(progress.status(3), StageProgress.Status.LOCKED)
	assert_eq(progress.status(-1), StageProgress.Status.LOCKED)
	assert_false(progress.is_unlocked(3))
