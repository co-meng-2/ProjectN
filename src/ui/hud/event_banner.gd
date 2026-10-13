class_name EventBanner
extends VBoxContainer
## The running event's name, progress and remaining time; after it ends, "<name>: Success +rewards" or
## "<name>: Failed" (or "<name>: Interrupted" when aborted) for `result_seconds`. A view: it reads the event and never changes it.

@export var result_seconds: float = 2.5
@export var success_color: Color = Color(0.55, 1.0, 0.5)
@export var failure_color: Color = Color(1.0, 0.45, 0.4)

var _event: StageEvent
var _tracking: bool = false
var _limit: float = 0.0
var _result_left: float = 0.0
var _title := Label.new()
var _progress := Label.new()
var _timer := Label.new()


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	for label in [_title, _progress, _timer]:
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(label)
	visible = false


func show_event(def: EventDef, event: StageEvent) -> void:
	_event = event
	_limit = def.time_limit
	_tracking = true
	_result_left = 0.0
	_title.text = def.display_name
	_title.modulate = Color.WHITE
	_progress.text = ""
	_timer.text = ""
	visible = true
	step(0.0)


func show_progress(text: String) -> void:
	_progress.text = text


func show_result(def: EventDef, success: bool) -> void:
	_event = null
	_tracking = false
	if success:
		_title.text = "%s: Success %s" % [def.display_name, reward_text(def.reward)]
		_title.modulate = success_color
	else:
		_title.text = "%s: Failed" % def.display_name
		_title.modulate = failure_color
	_progress.text = ""
	_timer.text = ""
	_result_left = result_seconds
	visible = true


func show_aborted(def: EventDef) -> void:
	_event = null
	_tracking = false
	_title.text = "%s: Interrupted" % def.display_name
	_title.modulate = Color.WHITE
	_progress.text = ""
	_timer.text = ""
	_result_left = result_seconds
	visible = true


static func reward_text(reward: RewardDef) -> String:
	if reward == null:
		return ""
	var parts := PackedStringArray()
	if reward.gold > 0:
		parts.append("+%d gold" % reward.gold)
	if reward.stone > 0:
		parts.append("+%d stone" % reward.stone)
	if reward.tickets > 0:
		parts.append("+%d %s" % [reward.tickets, "ticket" if reward.tickets == 1 else "tickets"])
	return " ".join(parts)


func title_text() -> String:
	return _title.text


func progress_text() -> String:
	return _progress.text


func timer_text() -> String:
	return _timer.text


func step(delta: float) -> void:
	if _tracking:
		if is_instance_valid(_event):
			var left := _event.remaining_time()
			if not _event.is_running() and left <= 0.0:
				left = _limit  # event_started fires before begin() sets the timer
			_timer.text = "%ds" % ceili(left)
		return
	if _result_left > 0.0:
		_result_left -= delta
		if _result_left <= 0.0:
			visible = false


func _process(delta: float) -> void:
	step(delta)
