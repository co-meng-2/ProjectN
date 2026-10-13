extends TestCase


func test_profile_defaults_to_six_empty_slots() -> void:
	var profile := DefFactory.profile()
	assert_eq(profile.slot_count, 6)
	assert_true(profile.abilities.is_empty())
	assert_no_errors(profile.validate(), "default profile")


func test_profile_rejects_zero_slots_null_and_duplicate_abilities() -> void:
	var profile := DefFactory.profile()
	profile.slot_count = 0
	assert_eq(profile.validate().size(), 1, "slot_count")
	profile.slot_count = 6
	profile.abilities.append(null)
	assert_eq(profile.validate().size(), 1, "null ability")
	profile.abilities.clear()
	profile.abilities.append(DefFactory.vitality())
	profile.abilities.append(DefFactory.vitality())
	var errors := profile.validate()
	assert_eq(errors.size(), 1, ", ".join(errors))
	assert_true(errors[0].contains("vitality"), "duplicate id is named")


func test_profile_collects_nested_ability_errors() -> void:
	var profile := DefFactory.profile()
	var broken := DefFactory.vitality()
	broken.icon = null
	profile.abilities.append(broken)
	assert_eq(profile.validate().size(), 1)
