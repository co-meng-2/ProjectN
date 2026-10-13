class_name Timeline
extends RefCounted
## Pure schedule math: a duration plus sorted mark times (for example wave starts).

var duration: float
var marks: PackedFloat32Array


func _init(p_duration: float, p_marks: PackedFloat32Array = PackedFloat32Array()) -> void:
	duration = p_duration
	marks = p_marks.duplicate()
	marks.sort()


func progress(time: float) -> float:
	if duration <= 0.0:
		return 1.0
	return clampf(time / duration, 0.0, 1.0)


## Time of the first mark strictly after `time`, or -1.0 when none remain.
func next_after(time: float) -> float:
	for mark in marks:
		if mark > time:
			return mark
	return -1.0


## Indices of marks with from_time < mark <= to_time, in ascending order.
func crossed(from_time: float, to_time: float) -> PackedInt32Array:
	var indices := PackedInt32Array()
	for i in marks.size():
		if marks[i] > from_time and marks[i] <= to_time:
			indices.append(i)
	return indices
