class_name SpawnGeometry
extends RefCounted
## Pure helpers for choosing spawn positions.


static func ring_point(center: Vector2, radius: float, angle: float) -> Vector2:
	return center + Vector2.from_angle(angle) * radius


static func clamp_to_rect(point: Vector2, bounds: Rect2, margin: float = 0.0) -> Vector2:
	var inner := bounds.grow(-margin)
	return Vector2(
		clampf(point.x, inner.position.x, inner.end.x),
		clampf(point.y, inner.position.y, inner.end.y)
	)
