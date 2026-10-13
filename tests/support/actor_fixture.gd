class_name ActorFixture
extends RefCounted
## Builds a bare actor (Node2D + AbilitySystemComponent child) for GAS-level tests.

const ACTOR_ASC: PackedScene = preload("res://src/gas/actor_asc.tscn")
const PLAYER_SCENE: PackedScene = preload("res://src/actors/player/player.tscn")


## The ASC must be inside the tree before overrides are applied (attribute sets are copied in _ready).
static func bare_actor(test: TestCase, stats: StatBlock = null) -> Node2D:
	var actor := test.adopt(Node2D.new()) as Node2D
	var asc := ACTOR_ASC.instantiate() as AbilitySystemComponent
	asc.name = "AbilitySystemComponent"
	actor.add_child(asc)
	if stats != null:
		asc.initialize_attribute_overrides(stats.to_overrides())
	return actor


## Children of `parent` whose script is `script` (e.g. Telegraph, AreaStrike) and that are not queued for
## deletion, in child order.
static func live_children(parent: Node, script: Script) -> Array:
	var result: Array = []
	for child in parent.get_children():
		if child.get_script() == script and not child.is_queued_for_deletion():
			result.append(child)
	return result


static func asc_of(actor: Node) -> AbilitySystemComponent:
	return actor.get_node("AbilitySystemComponent") as AbilitySystemComponent


## A set-up Player (real scene) inside the test tree, with a fresh TargetProvider and projectile layer
## reachable through `player.ability_context.targets` / `.projectile_layer`.
## `silence_arrow` (default true) turns the scene's ArrowAbility `auto_fire` off, so its per-physics-frame
## retry cannot fire projectiles or start the Cooldown.Arrow tag behind a test; pass false to test auto-fire.
static func player(test: TestCase, wallet: Wallet = Wallet.new(), silence_arrow: bool = true) -> Player:
	var layer := test.adopt(Node2D.new()) as Node2D
	var targets := test.adopt(TargetProvider.new()) as TargetProvider
	var player := test.adopt(PLAYER_SCENE.instantiate()) as Player
	player.setup(DefFactory.profile(), targets, layer, wallet)
	targets.player = player
	if silence_arrow:
		(asc_of(player).get_node("ArrowAbility") as ArrowAbility).auto_fire = false
	return player


## An enemy-group actor with a real Hurtbox (default layer ENEMY_HURT, radius 10) at `position`,
## so projectiles and sweeps can damage it. Read its health through `asc_of(result)`.
static func hurt_target(test: TestCase, position: Vector2, layer: int = PhysicsLayers.ENEMY_HURT) -> Node2D:
	var actor := bare_actor(test, StatBlock.new())
	actor.global_position = position
	actor.add_to_group(Groups.ENEMIES)
	var hurtbox := Hurtbox.new()
	hurtbox.collision_layer = layer
	hurtbox.collision_mask = 0
	hurtbox.monitoring = false
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 10.0
	shape.shape = circle
	hurtbox.add_child(shape)
	actor.add_child(hurtbox)
	hurtbox.bind(asc_of(actor), actor)
	return actor


const KILL_EFFECT: GameplayEffect = preload("res://data/effects/damage_basic.tres")


## Lethal damage from a throwaway attacker, through the real damage pipeline.
static func kill(test: TestCase, target_asc: AbilitySystemComponent) -> void:
	var attacker := bare_actor(test, StatBlock.new())
	var spec := EffectBuilder.damage(attacker, attacker, KILL_EFFECT, 99999.0)
	asc_of(attacker).apply_effect_spec_to_target(spec, target_asc)


## An EventContext inside the test tree: an events layer, an enemy layer with a bound EnemyFactory, a
## TargetProvider, a building stand-in at `building_at`, `player` (or a plain Node2D at building_at + (0, 150)),
## RunRng(7), a 2400x1600 arena centred on the origin and a 300 px spawn ring.
static func event_context(test: TestCase, def: EventDef, player: Node2D = null, building_at: Vector2 = Vector2.ZERO) -> EventContext:
	var layer := test.adopt(Node2D.new()) as Node2D
	var enemies := test.adopt(Node2D.new()) as Node2D
	var targets := test.adopt(TargetProvider.new()) as TargetProvider
	var building := test.adopt(Node2D.new()) as Node2D
	building.global_position = building_at
	if player == null:
		player = test.adopt(Node2D.new()) as Node2D
		player.global_position = building_at + Vector2(0, 150)
	targets.building = building
	targets.player = player
	var rng := RunRng.new(7)
	var factory := test.adopt(EnemyFactory.new()) as EnemyFactory
	factory.bind(enemies, targets, rng)
	return EventContext.new(layer, factory, targets, player, building, rng, Rect2(-1200, -800, 2400, 1600), 300.0, def)


## Records an event's signals. Returned keys (all Arrays, appended to in emission order):
##   "succeeded" - one `true` per `succeeded` emission
##   "failed"    - one `true` per `failed` emission
##   "progress"  - the String of each `progress_changed` emission
## Call before `event.begin(...)`: begin may report progress immediately.
static func watch_event(event: StageEvent) -> Dictionary:
	var record := {"succeeded": [], "failed": [], "progress": []}
	event.succeeded.connect(func() -> void: (record["succeeded"] as Array).append(true))
	event.failed.connect(func() -> void: (record["failed"] as Array).append(true))
	event.progress_changed.connect(func(text: String) -> void: (record["progress"] as Array).append(text))
	return record


## A set-up Boss from `def` (its scene must be boss.tscn) at `position`, chasing `player`, sharing the
## player's TargetProvider and projectile layer. No brain is attached: tests activate patterns by tag.
static func boss(test: TestCase, player: Player, def: BossDef, position: Vector2, summon: Callable = Callable()) -> Boss:
	var result := test.adopt(def.scene.instantiate()) as Boss
	result.global_position = position
	result.setup(def, player, player.ability_context.targets, player.ability_context.projectile_layer, summon)
	return result
