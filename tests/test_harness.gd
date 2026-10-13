extends TestCase


func test_assert_eq_records_a_failure_on_mismatch() -> void:
	var probe := TestCase.new()
	probe.assert_eq(1, 2, "mismatch")
	assert_eq(probe.failures.size(), 1)


func test_assert_eq_passes_on_match() -> void:
	var probe := TestCase.new()
	probe.assert_eq("a", "a")
	assert_true(probe.failures.is_empty())


func test_assert_no_errors_reports_messages() -> void:
	var probe := TestCase.new()
	probe.assert_no_errors(PackedStringArray(["boom"]), "defs")
	assert_eq(probe.failures.size(), 1)
	assert_true(probe.failures[0].contains("boom"))


func test_adopt_frees_node_on_release() -> void:
	var node := adopt(Node.new())
	release_owned_nodes()
	assert_false(is_instance_valid(node))


func test_async_tests_can_await_frames() -> void:
	await next_frame()
	assert_true(true)
