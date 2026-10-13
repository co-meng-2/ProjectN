class_name VitalsAttributeSet
extends AttributeSet
## Hit points. `health` is clamped to [0, max_health].

var health: AttributeData = AttributeData.new(100.0)
var max_health: AttributeData = AttributeData.new(100.0)


func pre_attribute_change(attribute_name: String, proposed_value: float) -> float:
	match attribute_name:
		"health":
			return clampf(proposed_value, 0.0, max_health.current_value)
		"max_health":
			return maxf(proposed_value, 1.0)
	return proposed_value
