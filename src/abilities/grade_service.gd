class_name GradeService
extends RefCounted
## The shop rules: promote (NONE->COMMON buys; COMMON..UNIQUE re-roll from the next grade's pool;
## LEGENDARY->REVEALED keeps the ability) and reroll (same grade, a different ability preferred).
## Pure logic over Wallet, RunRng, Loadout, AbilityPool and GradeTable. Every action is atomic:
## check applicability, check the price, roll, spend, then write the slot. A refused action changes nothing.
## While the wallet holds a promotion ticket, every applicable action costs exactly one ticket.

## Emitted after every attempt, successful or not, so a view can react (flash a refused slot).
signal resolved(index: int, result: GradeResult)

var _loadout: Loadout
var _pool: AbilityPool
var _table: GradeTable
var _wallet: Wallet
var _rng: RunRng


func _init(loadout: Loadout, pool: AbilityPool, table: GradeTable, wallet: Wallet, rng: RunRng) -> void:
	_loadout = loadout
	_pool = pool
	_table = table
	_wallet = wallet
	_rng = rng


## Price of the next promotion of slot `index`; null when none is possible.
func promote_cost(index: int) -> Price:
	var slot := _loadout.slot(index)
	if slot == null:
		return null
	return _ticket_first(_table.promote_price(slot.grade))


## Price of rerolling slot `index`; null when the slot is empty or revealed.
func reroll_cost(index: int) -> Price:
	var slot := _loadout.slot(index)
	if slot == null or slot.is_empty():
		return null
	return _ticket_first(_table.reroll_price(slot.grade))


func promote(index: int) -> GradeResult:
	return _resolve(index, _promote(index))


func reroll(index: int) -> GradeResult:
	return _resolve(index, _reroll(index))


func _promote(index: int) -> GradeResult:
	var slot := _loadout.slot(index)
	var price := promote_cost(index)
	if slot == null or price == null:
		return GradeResult.failed(GradeResult.Outcome.NOT_APPLICABLE)
	if not price.is_affordable(_wallet):
		return GradeResult.failed(GradeResult.Outcome.NOT_ENOUGH)
	var target := Grade.next(slot.grade)
	var def: AbilityDef = null
	if target == Grade.Value.REVEALED:
		def = slot.def  # the enhanced form of the same ability: deterministic, never rolled
	else:
		def = _pool.roll(target, _rng)
	if def == null:
		return GradeResult.failed(GradeResult.Outcome.EMPTY_POOL)
	return _commit(index, price, def, target)


func _reroll(index: int) -> GradeResult:
	var slot := _loadout.slot(index)
	var price := reroll_cost(index)
	if slot == null or price == null:
		return GradeResult.failed(GradeResult.Outcome.NOT_APPLICABLE)
	if not price.is_affordable(_wallet):
		return GradeResult.failed(GradeResult.Outcome.NOT_ENOUGH)
	var def := _pool.roll(slot.grade, _rng, slot.def)
	if def == null:
		return GradeResult.failed(GradeResult.Outcome.EMPTY_POOL)
	return _commit(index, price, def, slot.grade)


func _commit(index: int, price: Price, def: AbilityDef, grade: Grade.Value) -> GradeResult:
	if not _wallet.try_spend(price.kind, price.amount):
		return GradeResult.failed(GradeResult.Outcome.NOT_ENOUGH)
	_loadout.set_slot(index, def, grade)
	return GradeResult.ok(def, grade)


func _resolve(index: int, result: GradeResult) -> GradeResult:
	resolved.emit(index, result)
	return result


## Applicability comes first (a null price stays null); then a held ticket replaces the table price.
func _ticket_first(price: Price) -> Price:
	if price == null or _wallet.tickets < 1:
		return price
	return Price.new(Wallet.Kind.TICKET, 1)
