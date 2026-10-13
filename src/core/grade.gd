class_name Grade
extends RefCounted
## The quality ladder of an ability. REVEALED is the enhanced form of the same LEGENDARY ability,
## so only COMMON..LEGENDARY are rolled from pools. `level` is the GAS ability_level and the curve X.

enum Value { NONE, COMMON, RARE, EPIC, UNIQUE, LEGENDARY, REVEALED }

const DISPLAY_NAMES: PackedStringArray = ["Empty", "Common", "Rare", "Epic", "Unique", "Legendary", "Revealed"]
const COLORS: Array[Color] = [
	Color(0.35, 0.35, 0.38),  # NONE
	Color(0.80, 0.80, 0.80),  # COMMON
	Color(0.30, 0.60, 1.00),  # RARE
	Color(0.70, 0.35, 1.00),  # EPIC
	Color(1.00, 0.55, 0.20),  # UNIQUE
	Color(1.00, 0.85, 0.25),  # LEGENDARY
	Color(0.55, 1.00, 0.95),  # REVEALED
]


static func next(grade: Value) -> Value:
	if grade == Value.REVEALED:
		return Value.REVEALED
	return (grade + 1) as Value


static func is_rollable(grade: Value) -> bool:
	return grade != Value.NONE and grade != Value.REVEALED


static func level(grade: Value) -> float:
	return float(grade)


static func display_name(grade: Value) -> String:
	return DISPLAY_NAMES[grade]


static func color(grade: Value) -> Color:
	return COLORS[grade]
