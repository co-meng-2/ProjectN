class_name AbilityBar
extends HBoxContainer
## The row of ability slots. A view: it reads Loadout, Wallet and GradeService through signals and
## only emits requests; it never mutates them.

signal promote_requested(index: int)
signal reroll_requested(index: int)

const SLOT_SCENE: PackedScene = preload("res://src/ui/hud/ability_slot_view.tscn")

var _loadout: Loadout
var _wallet: Wallet
var _service: GradeService
var _views: Array[AbilitySlotView] = []


func bind(loadout: Loadout, wallet: Wallet, service: GradeService) -> void:
	_unbind()
	_loadout = loadout
	_wallet = wallet
	_service = service
	for index in loadout.size():
		var view := SLOT_SCENE.instantiate() as AbilitySlotView
		view.slot_index = index
		add_child(view)
		view.promote_requested.connect(promote_requested.emit)
		view.reroll_requested.connect(reroll_requested.emit)
		_views.append(view)
	loadout.slot_changed.connect(_on_slot_changed)
	wallet.changed.connect(_on_wallet_changed)
	service.resolved.connect(_on_resolved)
	refresh_all()


func slot_view(index: int) -> AbilitySlotView:
	if index < 0 or index >= _views.size():
		return null
	return _views[index]


func refresh_all() -> void:
	for index in _views.size():
		_refresh(index)


func _refresh(index: int) -> void:
	var price := _service.promote_cost(index)
	var affordable := price != null and price.is_affordable(_wallet)
	_views[index].show_slot(_loadout.slot(index), price, affordable, _service.reroll_cost(index))


func _on_slot_changed(index: int, _slot: AbilitySlot) -> void:
	if index >= 0 and index < _views.size():
		_refresh(index)


func _on_wallet_changed(_gold: int, _stone: int) -> void:
	refresh_all()


func _on_resolved(index: int, result: GradeResult) -> void:
	var view := slot_view(index)
	if view != null and result.outcome != GradeResult.Outcome.OK:
		view.flash()


func _unbind() -> void:
	if _loadout != null and _loadout.slot_changed.is_connected(_on_slot_changed):
		_loadout.slot_changed.disconnect(_on_slot_changed)
	if _wallet != null and _wallet.changed.is_connected(_on_wallet_changed):
		_wallet.changed.disconnect(_on_wallet_changed)
	if _service != null and _service.resolved.is_connected(_on_resolved):
		_service.resolved.disconnect(_on_resolved)
	for view in _views:
		remove_child(view)
		view.queue_free()
	_views.clear()
