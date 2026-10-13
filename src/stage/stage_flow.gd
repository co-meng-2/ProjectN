class_name StageFlow
extends RefCounted
## The stage's lifecycle as an explicit state machine; owns transitions only.

signal state_changed(state: State)

enum State { PREP, RUNNING, BOSS, CLEARED, FAILED }

const ALLOWED: Dictionary = {
	State.PREP: [State.RUNNING],
	State.RUNNING: [State.BOSS, State.CLEARED, State.FAILED],
	State.BOSS: [State.CLEARED, State.FAILED],
}

var state: State = State.PREP


func transition_to(to: State) -> bool:
	if not ALLOWED.get(state, []).has(to):
		return false
	state = to
	state_changed.emit(state)
	return true


func is_terminal() -> bool:
	return state == State.CLEARED or state == State.FAILED
