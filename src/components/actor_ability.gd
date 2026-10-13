@abstract
class_name ActorAbility
extends GameplayAbility
## Base for abilities that act through their owning actor. The actor injects an AbilityContext.

## The ability asks its actor to show an animation; the actor decides how.
signal animation_requested(animation: StringName, facing: Vector2)

var context: AbilityContext


func bind_context(p_context: AbilityContext) -> void:
	context = p_context
