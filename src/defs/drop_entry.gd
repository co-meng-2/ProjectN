class_name DropEntry
extends Resource
## One possible drop of a DropTable.

@export var kind: Wallet.Kind = Wallet.Kind.GOLD
@export var icon: Texture2D
@export_range(0.0, 1.0) var chance: float = 1.0
@export var min_amount: int = 1
@export var max_amount: int = 1


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if chance < 0.0 or chance > 1.0:
		errors.append("DropEntry: chance must be within [0, 1]")
	if min_amount < 1 or max_amount < min_amount:
		errors.append("DropEntry: need 1 <= min_amount <= max_amount")
	return errors
