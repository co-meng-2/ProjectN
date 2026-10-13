class_name Wallet
extends RefCounted
## Per-run currency balances. Gem is persistent meta currency and lives elsewhere (Phase 4).

signal changed(gold: int, stone: int)

enum Kind { GOLD, STONE }

var gold: int = 0
var stone: int = 0


func balance(kind: Kind) -> int:
	return gold if kind == Kind.GOLD else stone


func add(kind: Kind, amount: int) -> void:
	if amount <= 0:
		return
	_apply(kind, balance(kind) + amount)


func try_spend(kind: Kind, amount: int) -> bool:
	if amount < 0 or balance(kind) < amount:
		return false
	_apply(kind, balance(kind) - amount)
	return true


func _apply(kind: Kind, value: int) -> void:
	if kind == Kind.GOLD:
		gold = value
	else:
		stone = value
	changed.emit(gold, stone)
