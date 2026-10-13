class_name GradeTable
extends Resource
## Prices of the ability shop. `promote_gold` is indexed by the grade being left (NONE..UNIQUE);
## LEGENDARY -> REVEALED costs stone, and every reroll costs stone.

const PROMOTE_STEPS: int = 5

@export var promote_gold: PackedInt32Array = PackedInt32Array()
@export var reveal_stone: int = 0
@export var reroll_stone: int = 0


## null when the grade cannot be promoted (REVEALED) or the table is too short.
func promote_price(grade: Grade.Value) -> Price:
	if grade == Grade.Value.REVEALED:
		return null
	if grade == Grade.Value.LEGENDARY:
		return Price.new(Wallet.Kind.STONE, reveal_stone)
	if grade >= promote_gold.size():
		return null
	return Price.new(Wallet.Kind.GOLD, promote_gold[grade])


## null when the grade cannot be rerolled (NONE, REVEALED).
func reroll_price(grade: Grade.Value) -> Price:
	if not Grade.is_rollable(grade):
		return null
	return Price.new(Wallet.Kind.STONE, reroll_stone)


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if promote_gold.size() != PROMOTE_STEPS:
		errors.append("GradeTable: promote_gold needs %d entries (NONE..UNIQUE), has %d" % [PROMOTE_STEPS, promote_gold.size()])
	for cost in promote_gold:
		if cost < 0:
			errors.append("GradeTable: negative promote cost %d" % cost)
			break
	if reveal_stone < 0 or reroll_stone < 0:
		errors.append("GradeTable: reveal_stone and reroll_stone must be >= 0")
	return errors
