class_name ProbeEvent
extends StageEvent
## Test double: counts begin/cleanup, finishes on demand, optionally ignores its time limit (`endless`),
## exposes objectives, and reports `reject_reason` from validate() so def validation can be tested.

@export var endless: bool = false
@export var reject_reason: String = ""

var begins: int = 0
var cleanups: int = 0
var objective_nodes: Array[Node2D] = []


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if reject_reason != "":
		errors.append(reject_reason)
	return errors


## Read by index into an untyped variable: a freed node must not be assigned to a typed loop variable.
func objectives() -> Array[Node2D]:
	var found: Array[Node2D] = []
	for i in objective_nodes.size():
		var node: Variant = objective_nodes[i]
		if is_instance_valid(node):
			found.append(node)
	return found


func finish(success: bool) -> void:
	if success:
		_succeed()
	else:
		_fail()


func _on_begin() -> void:
	begins += 1
	report_progress("Probe %s" % context.def.id)


func _on_time_up() -> void:
	if not endless:
		super()


func _cleanup() -> void:
	cleanups += 1
