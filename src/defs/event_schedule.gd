class_name EventSchedule
extends Resource
## When a stage's events happen. Times are whole seconds rolled once per run: a second is allowed when it
## lies inside [window_start, window_end] (and inside the stage) and is more than `wave_clearance` from every
## wave time. Events are placed in order; each is drawn uniformly from the allowed seconds at least
## `min_gap` after the previous event that still leave room to place the remaining events greedily, so a
## schedule whose greedy earliest placement fits always rolls `count` times.

@export var count: int = 0
@export var window_start: float = 0.0
@export var window_end: float = 0.0
## Between two event starts; must cover the longest time limit so at most one event runs at a time.
@export var min_gap: float = 0.0
@export var wave_clearance: float = 0.0
@export var pool: Array[EventDef] = []


func allowed_seconds(duration: float, wave_times: PackedFloat32Array) -> PackedInt32Array:
	var allowed := PackedInt32Array()
	for second in range(ceili(window_start), floori(window_end) + 1):
		if second <= 0 or float(second) >= duration:
			continue
		if _near_a_wave(float(second), wave_times):
			continue
		allowed.append(second)
	return allowed


## `count` ascending times, or an empty array when the schedule cannot be placed.
func roll_times(duration: float, wave_times: PackedFloat32Array, rng: RunRng) -> PackedFloat32Array:
	var times := PackedFloat32Array()
	if count <= 0:
		return times
	var allowed := allowed_seconds(duration, wave_times)
	var previous := -INF
	for placed in count:
		var remaining := count - placed - 1
		var candidates := PackedInt32Array()
		for second in allowed:
			if float(second) >= previous + min_gap and _fits_after(allowed, second, remaining):
				candidates.append(second)
		if candidates.is_empty():
			return PackedFloat32Array()
		previous = float(candidates[rng.randi_range(0, candidates.size() - 1)])
		times.append(previous)
	return times


## The greedy earliest placement; shorter than `count` when the schedule is infeasible.
func earliest_times(duration: float, wave_times: PackedFloat32Array) -> PackedFloat32Array:
	var times := PackedFloat32Array()
	var allowed := allowed_seconds(duration, wave_times)
	var cursor := -INF
	for i in maxi(count, 0):
		var next := _first_at_or_after(allowed, cursor + min_gap)
		if next < 0:
			break
		times.append(float(next))
		cursor = float(next)
	return times


## Defs for `amount` events drawn from the pool, never the same def twice in a row when the pool has
## alternatives. Null entries are skipped (validation reports them).
func roll_defs(amount: int, rng: RunRng) -> Array[EventDef]:
	var picks: Array[EventDef] = []
	var candidates: Array[EventDef] = []
	for def in pool:
		if def != null:
			candidates.append(def)
	if candidates.is_empty():
		return picks
	var previous: EventDef = null
	for i in amount:
		var options: Array[EventDef] = []
		for def in candidates:
			if def != previous:
				options.append(def)
		if options.is_empty():
			options = candidates
		previous = rng.pick(options) as EventDef
		picks.append(previous)
	return picks


func longest_time_limit() -> float:
	var longest := 0.0
	for def in pool:
		if def != null:
			longest = maxf(longest, def.time_limit)
	return longest


func validate_against(duration: float, wave_times: PackedFloat32Array) -> PackedStringArray:
	var errors := PackedStringArray()
	if count < 0:
		errors.append("EventSchedule: count must be >= 0")
	if window_start <= 0.0 or window_end >= duration or window_start > window_end:
		errors.append("EventSchedule: window [%s, %s] must lie inside (0, stage duration %s)" % [window_start, window_end, duration])
	if wave_clearance < 0.0:
		errors.append("EventSchedule: wave_clearance must be >= 0")
	if window_end + longest_time_limit() > duration:
		errors.append("EventSchedule: window_end %s + longest time_limit %s exceeds the stage duration %s (an event must be able to finish before time is up)" % [window_end, longest_time_limit(), duration])
	var usable := 0
	for def in pool:
		if def == null:
			errors.append("EventSchedule: null event in the pool")
			continue
		usable += 1
		errors.append_array(def.validate())
	if count > 0 and usable == 0:
		errors.append("EventSchedule: count is %d but the pool is empty" % count)
	if min_gap < longest_time_limit():
		errors.append("EventSchedule: min_gap %s is shorter than the longest time_limit %s (two events could overlap)" % [min_gap, longest_time_limit()])
	elif count > 0 and earliest_times(duration, wave_times).size() < count:
		errors.append("EventSchedule: cannot place %d events in the window with min_gap %s and wave_clearance %s" % [count, min_gap, wave_clearance])
	return errors


func _near_a_wave(second: float, wave_times: PackedFloat32Array) -> bool:
	for wave_time in wave_times:
		if absf(second - wave_time) <= wave_clearance:
			return true
	return false


## The first allowed second at or after `from`, or -1.
func _first_at_or_after(allowed: PackedInt32Array, from: float) -> int:
	for second in allowed:
		if float(second) >= from:
			return second
	return -1


## Whether `remaining` more events fit greedily after one placed at `start`.
func _fits_after(allowed: PackedInt32Array, start: int, remaining: int) -> bool:
	var cursor := float(start)
	for i in remaining:
		var next := _first_at_or_after(allowed, cursor + min_gap)
		if next < 0:
			return false
		cursor = float(next)
	return true
