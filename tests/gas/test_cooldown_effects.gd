extends TestCase


func test_cooldown_effects_grant_their_tags_for_a_duration() -> void:
	var dash := load("res://data/effects/cooldown_dash.tres") as GameplayEffect
	var arrow := load("res://data/effects/cooldown_arrow.tres") as GameplayEffect
	assert_eq(dash.policy, GameplayEffect.DurationPolicy.DURATION)
	assert_true(dash.granted_tags.has(GameplayTags.Cooldown_Dash))
	assert_eq(arrow.policy, GameplayEffect.DurationPolicy.DURATION)
	assert_true(arrow.granted_tags.has(GameplayTags.Cooldown_Arrow))


func test_cooldown_tag_is_present_then_expires() -> void:
	var effect := (load("res://data/effects/cooldown_dash.tres") as GameplayEffect).duplicate() as GameplayEffect
	effect.duration = 0.1
	var actor := ActorFixture.bare_actor(self)
	var asc := ActorFixture.asc_of(actor)
	asc.apply_gameplay_effect(effect)
	assert_true(asc.has_tag(GameplayTags.Cooldown_Dash))
	await wait_seconds(0.3)
	assert_false(asc.has_tag(GameplayTags.Cooldown_Dash))
