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
