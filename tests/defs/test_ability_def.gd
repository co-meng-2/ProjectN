extends TestCase

const PROBE_SCENE: PackedScene = preload("res://tests/support/probe_ability.tscn")


func _packed(node: Node) -> PackedScene:
	var scene := PackedScene.new()
	scene.pack(node)
	node.free()
	return scene


func test_valid_passive_scene_and_hybrid_defs_report_no_errors() -> void:
	assert_no_errors(DefFactory.vitality().validate(), "passive")
	assert_no_errors(DefFactory.ability(&"probe", null, PROBE_SCENE).validate(), "scene")
	assert_no_errors(DefFactory.ability(&"hybrid", DefFactory.vitality().passive_effect, PROBE_SCENE).validate(), "hybrid")


func test_def_needs_id_name_icon_and_some_content() -> void:
	var errors := AbilityDef.new().validate()
	assert_eq(errors.size(), 4, ", ".join(errors))


func test_min_grade_must_be_rollable() -> void:
	var def := DefFactory.vitality()
	def.min_grade = Grade.Value.NONE
	assert_eq(def.validate().size(), 1)
	def.min_grade = Grade.Value.REVEALED
	assert_eq(def.validate().size(), 1)


func test_passive_must_be_infinite_with_curved_nonzero_modifiers() -> void:
	var def := DefFactory.vitality()
	def.passive_effect.policy = GameplayEffect.DurationPolicy.DURATION
	assert_eq(def.validate().size(), 1, "policy")
	def = DefFactory.vitality()
	var modifier: GameplayEffectModifier = def.passive_effect.modifiers[0]
	modifier.magnitude = 0.0
	assert_eq(def.validate().size(), 1, "zero magnitude would zero every grade")
	modifier.magnitude = 1.0
	modifier.scaling_curve = null
	assert_eq(def.validate().size(), 1, "missing curve")
	modifier.scaling_curve = DefFactory.curve(PackedFloat32Array([1.0, 2.0]))
	modifier.attribute_name = ""
	assert_eq(def.validate().size(), 1, "missing attribute")
	modifier.attribute_name = "max_health"
	def.passive_effect.modifiers.append(null)
	assert_eq(def.validate().size(), 1, "null modifier")


func test_passive_needs_modifiers_and_free_stacking() -> void:
	var def := DefFactory.vitality()
	def.passive_effect.modifiers.clear()
	assert_eq(def.validate().size(), 1, "a passive without modifiers is a no-op")
	def = DefFactory.vitality()
	def.passive_effect.stacking_policy = GameplayEffect.StackingPolicy.REFRESH_DURATION
	assert_eq(def.validate().size(), 1, "shared handle across slots")


func test_scene_root_must_be_a_gameplay_ability_and_may_reject_itself() -> void:
	var def := DefFactory.ability(&"bad", null, _packed(Node2D.new()))
	assert_eq(def.validate().size(), 1, "root is not an ability")
	var probe := ProbeAbility.new()
	probe.reject_reason = "probe says no"
	def = DefFactory.ability(&"rejecting", null, _packed(probe))
	var errors := def.validate()
	assert_eq(errors.size(), 1)
	assert_true(errors[0].contains("probe says no"))


func test_icon_for_uses_the_revealed_icon_only_when_revealed_and_present() -> void:
	var def := DefFactory.vitality()
	assert_eq(def.icon_for(Grade.Value.REVEALED), def.icon, "no revealed icon: fall back")
	def.revealed_icon = DefFactory.texture(16, 16)
	assert_eq(def.icon_for(Grade.Value.LEGENDARY), def.icon)
	assert_eq(def.icon_for(Grade.Value.REVEALED), def.revealed_icon)
