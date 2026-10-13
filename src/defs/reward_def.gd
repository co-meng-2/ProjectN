class_name RewardDef
extends Resource
## What a successful stage event pays into the run's wallet. Failure pays nothing.

@export var gold: int = 0
@export var stone: int = 0
@export var tickets: int = 0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if gold < 0 or stone < 0 or tickets < 0:
		errors.append("RewardDef: gold, stone and tickets must be >= 0")
	elif gold == 0 and stone == 0 and tickets == 0:
		errors.append("RewardDef: pays nothing (gold, stone and tickets are all 0)")
	return errors
