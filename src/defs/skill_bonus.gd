class_name SkillBonus
extends RefCounted
## One permanent bonus carried on a PlayerProfile: apply `effect` to the player's ASC at `level`.

var effect: GameplayEffect
var level: int = 1


func _init(bonus_effect: GameplayEffect = null, bonus_level: int = 1) -> void:
	effect = bonus_effect
	level = bonus_level
