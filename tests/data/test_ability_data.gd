extends TestCase

## path -> [attribute, value at level 1, value at level 6] on a StatBlock.new() actor
## (max_health 100, move_speed 100, attack 1.0, gold_gain 1.0).
const PASSIVES: Dictionary = {
	"res://data/abilities/prospector.tres": ["gold_gain", 1.25, 3.5],
	"res://data/abilities/swift_boots.tres": ["move_speed", 110.0, 185.0],
	"res://data/abilities/vitality.tres": ["max_health", 120.0, 340.0],
	"res://data/abilities/might.tres": ["attack", 1.1, 2.0],
}


func _def(path: String) -> AbilityDef:
	var def := load(path) as AbilityDef
	assert_not_null(def, path)
	return def


func test_passive_defs_load_and_validate() -> void:
	for path: String in PASSIVES:
		var def := _def(path)
		assert_no_errors(def.validate(), path)
		assert_not_null(def.passive_effect, path)
		assert_null(def.ability_scene, "%s is a pure passive" % path)


func test_passives_scale_with_the_grade_level() -> void:
	for path: String in PASSIVES:
		var expected: Array = PASSIVES[path]
		var attribute: String = expected[0]
		var def := _def(path)
		var actor := ActorFixture.bare_actor(self, StatBlock.new())
		var asc := ActorFixture.asc_of(actor)
		var base: float = asc.get_attribute(attribute).current_value
		var handle := asc.apply_gameplay_effect(def.passive_effect, asc, Grade.level(Grade.Value.COMMON))
		assert_not_null(handle, path)
		assert_almost_eq(asc.get_attribute(attribute).current_value, expected[1], 0.001, "%s at level 1" % path)
		asc.remove_active_effect(handle)
		assert_almost_eq(asc.get_attribute(attribute).current_value, base, 0.001, "%s removed" % path)
		asc.apply_gameplay_effect(def.passive_effect, asc, Grade.level(Grade.Value.REVEALED))
		assert_almost_eq(asc.get_attribute(attribute).current_value, expected[2], 0.001, "%s at level 6" % path)


func test_passive_curves_are_strictly_increasing_over_levels_one_to_six() -> void:
	for path: String in PASSIVES:
		var modifier: GameplayEffectModifier = _def(path).passive_effect.modifiers[0]
		var previous := -INF
		for level in range(1, 7):
			var value := modifier.calculate_magnitude(float(level))
			assert_true(value > previous, "%s level %d (%f <= %f)" % [path, level, value, previous])
			previous = value


func test_volley_def_validates_and_carries_the_scene() -> void:
	var def := _def("res://data/abilities/volley.tres")
	assert_no_errors(def.validate(), "volley")
	assert_null(def.passive_effect)
	var root := def.ability_scene.instantiate()
	assert_true(root is VolleyAbility)
	root.free()


func test_blade_sweep_def_validates_and_carries_the_scene() -> void:
	var def := _def("res://data/abilities/blade_sweep.tres")
	assert_no_errors(def.validate(), "blade_sweep")
	var root := def.ability_scene.instantiate()
	assert_true(root is BladeSweepAbility)
	root.free()
