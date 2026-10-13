extends TestCase


func test_expires_once_when_time_runs_out() -> void:
	var lifetime := adopt(Lifetime.new()) as Lifetime
	var expired: Array = []
	lifetime.expired.connect(func() -> void: expired.append(true))
	lifetime.restart(1.0)
	lifetime.step(0.5)
	assert_eq(expired.size(), 0)
	lifetime.step(0.6)
	lifetime.step(5.0)
	assert_eq(expired.size(), 1)


func test_restart_rearms_the_countdown() -> void:
	var lifetime := adopt(Lifetime.new()) as Lifetime
	var expired: Array = []
	lifetime.expired.connect(func() -> void: expired.append(true))
	lifetime.restart(0.1)
	lifetime.step(0.2)
	lifetime.restart(0.1)
	lifetime.step(0.2)
	assert_eq(expired.size(), 2)
