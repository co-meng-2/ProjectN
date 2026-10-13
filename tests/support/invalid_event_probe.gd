extends Node2D
## Test controller whose own validate() always reports one error (for EventDef.validate delegation).


func validate() -> PackedStringArray:
	return PackedStringArray(["probe controller is invalid"])
