extends TestCase


func _scene() -> PackedScene:
	return DefFactory.packed(Node2D.new())


func test_reward_must_pay_something_and_nothing_negative() -> void:
	assert_eq(RewardDef.new().validate().size(), 1, "all zero pays nothing")
	assert_no_errors(DefFactory.reward(0, 0, 1).validate(), "a single ticket is a reward")
	assert_eq(DefFactory.reward(150, -1, 1).validate().size(), 1, "negative stone")


func test_valid_event_def_reports_no_errors() -> void:
	assert_no_errors(DefFactory.event(_scene(), 25.0).validate(), "event")


func test_event_def_needs_id_name_scene_limit_and_reward() -> void:
	var errors := EventDef.new().validate()
	assert_eq(errors.size(), 5, ", ".join(errors))


func test_reward_errors_are_reported_with_the_event() -> void:
	var def := DefFactory.event(_scene())
	def.reward = DefFactory.reward(-5, 0, 0)
	var errors := def.validate()
	assert_eq(errors.size(), 1)
	assert_true(errors[0].contains("probe"), "names the event")


func test_controller_root_must_be_a_node2d() -> void:
	var def := DefFactory.event(DefFactory.packed(Node.new()))
	assert_eq(def.validate().size(), 1, "a plain Node cannot be placed in the world")


func test_controller_root_validate_errors_are_reported() -> void:
	var root := Node2D.new()
	root.set_script(load("res://tests/support/invalid_event_probe.gd"))
	var errors := DefFactory.event(DefFactory.packed(root)).validate()
	assert_eq(errors.size(), 1, ", ".join(errors))
	assert_true(errors[0].contains("probe controller is invalid"))


func test_controller_root_may_reject_itself() -> void:
	var probe := (load("res://tests/support/probe_event.tscn") as PackedScene).instantiate() as ProbeEvent
	probe.reject_reason = "probe says no"
	var errors := DefFactory.event(DefFactory.packed(probe)).validate()
	assert_eq(errors.size(), 1)
	assert_true(errors[0].contains("probe says no"))
