extends TestCase

const PROBE: PackedScene = preload("res://tests/support/probe_event.tscn")


func _banner() -> EventBanner:
	var banner := adopt(EventBanner.new()) as EventBanner
	banner.set_process(false)
	return banner


func _running_probe(def: EventDef) -> ProbeEvent:
	var event := adopt(PROBE.instantiate()) as ProbeEvent
	event.set_physics_process(false)
	event.begin(ActorFixture.event_context(self, def))
	return event


func test_hidden_until_an_event_starts() -> void:
	assert_false(_banner().visible)


func test_shows_name_progress_and_remaining_time_of_the_running_event() -> void:
	var banner := _banner()
	var def := DefFactory.event(PROBE, 25.0, &"statues")
	var event := _running_probe(def)
	banner.show_event(def, event)
	assert_true(banner.visible)
	assert_eq(banner.title_text(), "Statues")
	assert_eq(banner.timer_text(), "25s")
	banner.show_progress("Statues 1/4")
	assert_eq(banner.progress_text(), "Statues 1/4")
	event.step(5.2)
	banner.step(0.016)
	assert_eq(banner.timer_text(), "20s", "ceil of 19.8")


func test_success_shows_the_rewards_then_hides() -> void:
	var banner := _banner()
	var def := DefFactory.event(PROBE, 25.0, &"meteors")
	banner.show_event(def, _running_probe(def))
	banner.show_result(def, true)
	assert_eq(banner.title_text(), "Meteors: Success +150 gold +2 stone +1 ticket")
	assert_eq(banner.timer_text(), "")
	banner.step(1.0)
	assert_true(banner.visible)
	banner.step(2.0)
	assert_false(banner.visible, "hidden after result_seconds (2.5 s)")


func test_failure_shows_failed_without_rewards() -> void:
	var banner := _banner()
	var def := DefFactory.event(PROBE, 25.0, &"elites")
	banner.show_result(def, false)
	assert_true(banner.visible)
	assert_eq(banner.title_text(), "Elites: Failed")


func test_reward_text_omits_zero_amounts_and_pluralises_tickets() -> void:
	assert_eq(EventBanner.reward_text(DefFactory.reward(0, 3, 2)), "+3 stone +2 tickets")
	assert_eq(EventBanner.reward_text(DefFactory.reward(200, 0, 0)), "+200 gold")


func test_a_freed_event_does_not_break_the_banner() -> void:
	var banner := _banner()
	var def := DefFactory.event(PROBE, 25.0)
	var event := _running_probe(def)
	banner.show_event(def, event)
	event.free()
	banner.step(0.1)
	assert_eq(banner.timer_text(), "25s", "keeps the last value")
