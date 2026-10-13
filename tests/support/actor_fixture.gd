class_name ActorFixture
extends RefCounted
## Builds a bare actor (Node2D + AbilitySystemComponent child) for GAS-level tests.

const ACTOR_ASC: PackedScene = preload("res://src/gas/actor_asc.tscn")


## The ASC must be inside the tree before overrides are applied (attribute sets are copied in _ready).
static func bare_actor(test: TestCase, stats: StatBlock = null) -> Node2D:
	var actor := test.adopt(Node2D.new()) as Node2D
	var asc := ACTOR_ASC.instantiate() as AbilitySystemComponent
	asc.name = "AbilitySystemComponent"
	actor.add_child(asc)
	if stats != null:
		asc.initialize_attribute_overrides(stats.to_overrides())
	return actor


static func asc_of(actor: Node) -> AbilitySystemComponent:
	return actor.get_node("AbilitySystemComponent") as AbilitySystemComponent
