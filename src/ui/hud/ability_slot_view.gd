class_name AbilitySlotView
extends Control
## One HUD slot: the ability's icon inside a grade-coloured frame and the cost of its next promotion.
## A view only: clicks become request signals; the app decides what happens.

signal promote_requested(index: int)
signal reroll_requested(index: int)

const EMPTY_FRAME_COLOR: Color = Color(0.3, 0.3, 0.33)
const BACKGROUND_COLOR: Color = Color(0.08, 0.08, 0.1, 0.85)
const FRAME_WIDTH: float = 3.0
const DIM_ALPHA: float = 0.35
const REVEALED_TINT: Color = Color(1.0, 0.95, 0.55)
const FLASH_COLOR: Color = Color(1.0, 0.35, 0.35)
const FLASH_SECONDS: float = 0.25

var slot_index: int = 0

var _frame_color: Color = EMPTY_FRAME_COLOR
var _dimmed: bool = false

@onready var _icon: TextureRect = %Icon
@onready var _cost: Label = %Cost


## `price` is the next promotion's price (null = nothing left to promote); `affordable` whether the
## wallet covers it; `reroll_price` only feeds the tooltip.
func show_slot(slot: AbilitySlot, price: Price, affordable: bool, reroll_price: Price = null) -> void:
	var filled := slot != null and not slot.is_empty()
	_frame_color = Grade.color(slot.grade) if filled else EMPTY_FRAME_COLOR
	_icon.texture = slot.def.icon_for(slot.grade) if filled else null
	var tint_revealed := filled and slot.grade == Grade.Value.REVEALED and slot.def.revealed_icon == null
	_icon.modulate = REVEALED_TINT if tint_revealed else Color.WHITE
	_dimmed = price != null and not affordable
	_icon.self_modulate = Color(1.0, 1.0, 1.0, DIM_ALPHA if _dimmed else 1.0)
	_cost.modulate = Color(1.0, 1.0, 1.0, DIM_ALPHA if _dimmed else 1.0)  # visible even on an empty slot
	_cost.text = cost_text(price)
	tooltip_text = _tooltip(slot, price, reroll_price)
	queue_redraw()


static func cost_text(price: Price) -> String:
	if price == null:
		return "MAX"
	return "%d%s" % [price.amount, "g" if price.kind == Wallet.Kind.GOLD else "s"]


func is_dimmed() -> bool:
	return _dimmed


func is_flashing() -> bool:
	return modulate != Color.WHITE


## Brief red tint for a refused action.
func flash() -> void:
	modulate = FLASH_COLOR
	create_tween().tween_property(self, "modulate", Color.WHITE, FLASH_SECONDS)


func _gui_input(event: InputEvent) -> void:
	var click := event as InputEventMouseButton
	if click == null or not click.pressed or click.button_index != MOUSE_BUTTON_LEFT:
		return
	if click.ctrl_pressed:
		reroll_requested.emit(slot_index)
	else:
		promote_requested.emit(slot_index)
	accept_event()


func _draw() -> void:
	var rect := Rect2(Vector2.ZERO, size)
	draw_rect(rect, BACKGROUND_COLOR)
	var frame := _frame_color
	if _dimmed:
		frame.a = DIM_ALPHA
	draw_rect(rect, frame, false, FRAME_WIDTH)


func _tooltip(slot: AbilitySlot, price: Price, reroll_price: Price) -> String:
	if slot == null or slot.is_empty():
		return "Empty slot\nClick: buy a Common ability (%s)" % cost_text(price)
	var text := "%s (%s)" % [slot.def.display_name, Grade.display_name(slot.grade)]
	if price == null:
		text += "\nFully revealed"
	else:
		text += "\nClick: promote (%s)" % cost_text(price)
	if reroll_price != null:
		text += "\nCtrl+click: reroll (%s)" % cost_text(reroll_price)
	return text
