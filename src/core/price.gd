class_name Price
extends RefCounted
## What one shop action costs: an amount of a single currency.

var kind: Wallet.Kind
var amount: int


func _init(p_kind: Wallet.Kind, p_amount: int) -> void:
	kind = p_kind
	amount = p_amount


func is_affordable(wallet: Wallet) -> bool:
	return wallet.balance(kind) >= amount
