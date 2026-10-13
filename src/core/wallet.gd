class_name Wallet
extends RefCounted
## Per-run balances: gold, stone and promotion tickets (paid by stage events; one ticket pays any shop action).
## Gem is persistent meta currency and lives elsewhere (Phase 4).

signal changed(gold: int, stone: int, tickets: int)

enum Kind { GOLD, STONE, TICKET }

var gold: int = 0
var stone: int = 0
var tickets: int = 0


func balance(kind: Kind) -> int:
	match kind:
		Kind.GOLD:
			return gold
		Kind.STONE:
			return stone
	return tickets


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
	match kind:
		Kind.GOLD:
			gold = value
		Kind.STONE:
			stone = value
		Kind.TICKET:
			tickets = value
	changed.emit(gold, stone, tickets)
