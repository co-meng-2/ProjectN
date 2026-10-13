extends TestCase


func test_normal_progression_and_signal() -> void:
	var flow := StageFlow.new()
	var seen: Array = []
	flow.state_changed.connect(func(state: StageFlow.State) -> void: seen.append(state))
	assert_true(flow.transition_to(StageFlow.State.RUNNING))
	assert_true(flow.transition_to(StageFlow.State.CLEARED))
	assert_eq(seen, [StageFlow.State.RUNNING, StageFlow.State.CLEARED])
	assert_true(flow.is_terminal())


func test_illegal_transitions_are_refused() -> void:
	var flow := StageFlow.new()
	assert_false(flow.transition_to(StageFlow.State.CLEARED), "cannot clear before running")
	flow.transition_to(StageFlow.State.RUNNING)
	flow.transition_to(StageFlow.State.FAILED)
	assert_false(flow.transition_to(StageFlow.State.RUNNING), "terminal states are final")
	assert_false(flow.transition_to(StageFlow.State.CLEARED))
	assert_eq(flow.state, StageFlow.State.FAILED)


func test_boss_state_sits_between_running_and_the_end() -> void:
	var flow := StageFlow.new()
	flow.transition_to(StageFlow.State.RUNNING)
	assert_true(flow.transition_to(StageFlow.State.BOSS))
	assert_true(flow.transition_to(StageFlow.State.CLEARED))
