class_name ActorVisual
extends AnimatedSprite2D
## Plays an actor's animations from a VisualDef (whose SpriteFrames are shared between actors).

signal one_shot_finished(animation_name: StringName)

const HIT_FLASH_COLOR: Color = Color(1.0, 0.45, 0.45)
const HIT_FLASH_SECONDS: float = 0.15

var _base_tint: Color = Color.WHITE
var _moving: bool = false
var _one_shot_active: bool = false


func _ready() -> void:
	animation_finished.connect(_on_animation_finished)


func apply(def: VisualDef, scale_factor: float = 1.0, tint: Color = Color.WHITE) -> void:
	sprite_frames = def.sprite_frames()
	position = def.offset
	scale = Vector2.ONE * scale_factor
	_base_tint = tint
	modulate = tint
	_play_locomotion()


func set_moving(moving: bool) -> void:
	_moving = moving
	if not _one_shot_active:
		_play_locomotion()


func face(direction_x: float) -> void:
	if absf(direction_x) > 0.01:
		flip_h = direction_x < 0.0


## Plays a non-looping animation, then returns to idle/walk (DEATH stays on its last frame).
func play_once(animation_name: StringName) -> void:
	if sprite_frames == null or not sprite_frames.has_animation(animation_name):
		return
	_one_shot_active = true
	play(animation_name)


func flash_hit() -> void:
	modulate = HIT_FLASH_COLOR
	create_tween().tween_property(self, "modulate", _base_tint, HIT_FLASH_SECONDS)


func _play_locomotion() -> void:
	if sprite_frames == null:
		return
	var wanted := AnimNames.WALK if _moving else AnimNames.IDLE
	if not sprite_frames.has_animation(wanted):
		wanted = AnimNames.IDLE
	if animation != wanted or not is_playing():
		play(wanted)


func _on_animation_finished() -> void:
	var finished := animation
	one_shot_finished.emit(finished)
	if finished == AnimNames.DEATH:
		return
	_one_shot_active = false
	_play_locomotion()
